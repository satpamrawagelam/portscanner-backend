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

            var config = await _context.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig 
            { 
                MaxConcurrency = 50, PingTimeout = 1000, PortScanTimeout = 1000 
            };

            List<int> portsToScan = new();
            
            if (req.Manual_port.HasValue) 
            {
                portsToScan.Add(req.Manual_port.Value);
            } 
            else if (req.Pg_id.HasValue)
            {
                if (req.Pg_id == 0)
                {
                    portsToScan = await _context.PortMasters
                        .Select(p => p.Pm_portNumber).Distinct().ToListAsync();
                } 
                else 
                {
                    portsToScan = await _context.PortMasters
                        .Where(p => p.Pm_portGroup == req.Pg_id)
                        .Select(p => p.Pm_portNumber).ToListAsync();
                }
            }
            else
            {
                return BadRequest("PortGroup or Manual port required");
            }

            if (!portsToScan.Any()) return BadRequest("Tidak ada port yang ditemukan untuk discan.");

            var scanResults = await _scanService.ExecuteSubnetScanAsync(
                branch.Branch_cidr,
                portsToScan,
                config.MaxConcurrency,
                config.PingTimeout,
                config.PortScanTimeout
            );

            var portSeverities = await _context.PortMasters
                .ToDictionaryAsync(p => p.Pm_portNumber, p => p.Pm_severity);

            foreach (var host in scanResults)
            {
                foreach (var port in host.Ports)
                {
                    port.Severity = portSeverities.ContainsKey(port.Port) 
                        ? portSeverities[port.Port] 
                        : "Info";
                }
            }

            if (scanResults.Any())
            {
                await _scanService.BulkSaveResultsAsync(
                    branch.Branch_id, 
                    scanResults, 
                    req.Title, 
                    "Manual Scan", 
                    null
                );
            }

            return Ok(new
            {
                branchId = branch.Branch_id,
                branchName = branch.Branch_name,
                branchCidr = branch.Branch_cidr,
                summary = new 
                { 
                    totalHosts = scanResults.Count, 
                    aliveHosts = scanResults.Count(r => r.IsHostAlive),
                    deadHosts = scanResults.Count(r => !r.IsHostAlive),
                    totalOpenPorts = scanResults.Sum(r => r.Ports.Count(p => p.Status))
                },
                results = scanResults 
            });
        }
    }
}