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

            if (req.BranchIds == null || !req.BranchIds.Any())
                return BadRequest("Branch wajib diisi.");

            var branches = await _context.Branches.Where(b => req.BranchIds.Contains(b.Branch_id)).ToListAsync();
            if (!branches.Any()) return BadRequest("Branch not found");

            var config = await _context.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig 
            { 
                MaxConcurrency = 50, PingTimeout = 1000, PortScanTimeout = 1000 
            };

            List<int> portsToScan = new();
            
            if (req.Manual_ports != null && req.Manual_ports.Any()) 
            {
                portsToScan.AddRange(req.Manual_ports);
            } 
            else if (req.Pg_id.HasValue)
            {
                if (req.Pg_id == 0)
                {
                    portsToScan = await _context.PortMasters
                        .Select(p => p.Pm_port_number).Distinct().ToListAsync();
                } 
                else 
                {
                    portsToScan = await _context.PortMasters
                        .Where(p => p.Pg_id == req.Pg_id)
                        .Select(p => p.Pm_port_number).Distinct().ToListAsync();
                }
            }
            else
            {
                return BadRequest("PortGroup or Manual port required");
            }

            if (!portsToScan.Any()) return BadRequest("Tidak ada port yang ditemukan untuk discan.");

            var portSeverities = await _context.PortMasters
                .GroupBy(p => p.Pm_port_number)
                .ToDictionaryAsync(
                    g => g.Key, 
                    g => g.First().Pm_severity
                );

            var sessionObj = await _scanService.CreateSessionAsync(req.Title, "Manual Scan");
            var allResults = new List<object>();
            var allChanges = new List<PortStatusChange>();

            foreach (var branch in branches)
            {
                var scanResults = await _scanService.ExecuteSubnetScanAsync(
                    branch.Branch_cidr,
                    portsToScan
                );

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
                    var branchChanges = await _scanService.BulkSaveResultsAsync(
                        branch.Branch_id, 
                        scanResults, 
                        sessionObj.Session_id,
                        sessionObj.ScanDate,
                        null
                    );

                    allChanges.AddRange(branchChanges);
                }

                allResults.Add(new
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

            if (allChanges.Any())
            {
                var openPorts = await _context.HostPorts
                    .Include(hp => hp.IpAddress)
                    .ThenInclude(ip => ip.Branch)
                    .Where(hp => req.BranchIds.Contains(hp.IpAddress.Ip_branchId) && hp.Status == true)
                    .AsNoTracking()
                    .ToListAsync();

                if (openPorts.Any())
                {
                    var msgBuilder = new System.Text.StringBuilder();
                    msgBuilder.AppendLine($"⚡ <b>[MANUAL SCAN ALERT] {req.Title}</b>");
                    msgBuilder.AppendLine("Daftar Host & Port Terbuka:\n");

                    var grouped = openPorts.GroupBy(hp => hp.IpAddress.Branch.Branch_name);
                    foreach (var g in grouped)
                    {
                        string branchHeader = $"🏢 <b>{g.Key}</b>";
                        if (msgBuilder.Length + branchHeader.Length > 3500)
                        {
                            await AlertController.SendAlertAsync(msgBuilder.ToString());
                            msgBuilder.Clear();
                        }
                        msgBuilder.AppendLine(branchHeader);

                        var ipGrouped = g.GroupBy(hp => hp.IpAddress.Ip_address);
                        foreach (var ig in ipGrouped)
                        {
                            string portList = string.Join(", ", ig.Select(hp => hp.Port_number).OrderBy(p => p));
                            string line = $"  🖥️ <code>{ig.Key}</code> : {portList}";

                            if (msgBuilder.Length + line.Length > 3500)
                            {
                                await AlertController.SendAlertAsync(msgBuilder.ToString());
                                msgBuilder.Clear();
                                msgBuilder.AppendLine($"🏢 <b>{g.Key} (Lanjutan)</b>");
                            }
                            msgBuilder.AppendLine(line);
                        }
                        msgBuilder.AppendLine();
                    }

                    if (msgBuilder.Length > 0 && msgBuilder.ToString().Trim() != "")
                    {
                        await AlertController.SendAlertAsync(msgBuilder.ToString());
                    }
                }
            }

            return Ok(allResults);
        }
    }
}