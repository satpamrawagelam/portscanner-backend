using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Models;
using portscanner_backend.Models.Dto;
using portscanner_backend.Services;

namespace portscanner_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScanController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly PortScanService _scanService;

        public ScanController(AppDbContext context, PortScanService scanService)
        {
            _context = context;
            _scanService = scanService;
        }

        [HttpPost]
        public async Task<IActionResult> Scan([FromBody] ScanRequestDto req)
        {
            if (string.IsNullOrWhiteSpace(req.Title)) 
            return BadRequest("Judul Scan wajib diisi.");

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Branch_id == req.Branch_id);
            if (branch == null) return BadRequest("Branch not found");

            var config = await _context.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig();

            var ipList = _scanService.ExpandCidr(branch.Branch_cidr);
        
            var aliveHosts = await _scanService.GetAliveHostsAsync(
                ipList, 
                config.MaxConcurrency, 
                config.PingTimeout, 
                config.PingRetries 
            );

            List<int> portsToScan = new();
            if (req.Manual_port.HasValue) 
            {
                portsToScan.Add(req.Manual_port.Value);
            } else if (req.Pg_id.HasValue)
            {
                if (req.Pg_id == 0)
                {
                    portsToScan = await _context.PortMasters.Select(p => p.Pm_portNumber).Distinct().ToListAsync();
                } else
                {
                    portsToScan = await _context.PortMasters.Where(p => p.Pm_portGroup == req.Pg_id).Select(p => p.Pm_portNumber).ToListAsync();
                }
            }
            else
            {
                return BadRequest("PortGroup or Manual port required");
            }

            var resultList = new List<IpScanResultDto>();

            var portSeverities = await _context.PortMasters
            .ToDictionaryAsync(p => p.Pm_portNumber, p => p.Pm_severity);

            using var semaphore = new SemaphoreSlim(config.MaxConcurrency); 
            var scanTasks = new List<Task>();
            
            foreach (var ip in aliveHosts)
            {
                scanTasks.Add(Task.Run(async () => 
                {
                    await semaphore.WaitAsync(); 
                    try 
                    {
                        var ipResult = new IpScanResultDto { Ip = ip, Ports = new List<PortScanResultDto>() };
                        
                        foreach (var port in portsToScan)
                        {
                            bool isOpen = await _scanService.ScanPortAsync(ip, port, config.PortScanTimeout); 
                            
                            string severity = portSeverities.ContainsKey(port) ? portSeverities[port] : "Low";

                            ipResult.Ports.Add(new PortScanResultDto 
                            { 
                                Port = port, 
                                Status = isOpen,
                                Severity = severity
                            });
                        }
                        // Karena banyak thread mau nulis ke 'resultList' bersamaan, kita kunci (lock)
                        lock (resultList)
                        {
                            resultList.Add(ipResult);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(scanTasks);

            if (resultList.Any())
            {
                await _scanService.BulkSaveResultsAsync(branch.Branch_id, resultList, req.Title, "Manual Scan");
            }

            return Ok(new
            {
                branchId = branch.Branch_id,
                summary = new 
                { 
                    totalHosts = aliveHosts.Count, 
                    totalScanned = resultList.Count,
                },
                results = resultList
            });
        }
    }

}
