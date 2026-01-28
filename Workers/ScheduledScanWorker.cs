using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Services;
using portscanner_backend.Models.Dto;

namespace portscanner_backend.Workers
{
    public class ScheduledScanWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScheduledScanWorker> _logger;
    
        private static readonly SemaphoreSlim _globalSemaphore = new SemaphoreSlim(100);

        public ScheduledScanWorker(IServiceProvider serviceProvider, ILogger<ScheduledScanWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker Berjalan: Menunggu Jadwal...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndExecuteSchedules();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error di Worker");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task CheckAndExecuteSchedules()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow.AddHours(7);

            var dueSchedules = await context.ScanSchedules
                .Where(s => s.Sch_isActive && s.Sch_nextRun <= now)
                .ToListAsync();

            if (!dueSchedules.Any()) return;

            foreach (var schedule in dueSchedules)
            {
                _logger.LogInformation($"Mengeksekusi Jadwal: {schedule.Sch_title}");

                var targetBranchIds = await context.ScanScheduleTargets
                    .Where(t => t.Tgt_schId == schedule.Sch_id)
                    .Select(t => t.Tgt_branchId)
                    .ToListAsync();

                List<int> targetPorts = new List<int>();

                if (schedule.Sch_portMode == "single" && schedule.Sch_targetManualPort.HasValue)
                {
                    targetPorts.Add(schedule.Sch_targetManualPort.Value);
                }
                else if (schedule.Sch_portMode == "group" && schedule.Sch_targetPortGroupId.HasValue)
                {
                    if (schedule.Sch_targetPortGroupId.Value == 0)
                    {
                        targetPorts = await context.PortMasters
                            .Select(pm => pm.Pm_portNumber)
                            .Distinct()
                            .ToListAsync();
                    } else {
                        targetPorts = await context.PortMasters
                        .Where(pm => pm.Pm_portGroup == schedule.Sch_targetPortGroupId)
                        .Select(pm => pm.Pm_portNumber)
                        .ToListAsync();
                    }
                    
                }
                else if (schedule.Sch_portMode == "all")
                {
                    targetPorts = await context.PortMasters
                        .Select(pm => pm.Pm_portNumber)
                        .Distinct()
                        .ToListAsync();
                }

                _ = ExecuteParallelScanAsync(targetBranchIds, targetPorts, schedule.Sch_title);

                UpdateNextRun(schedule);
            }

            await context.SaveChangesAsync();
        }

        private void UpdateNextRun(Models.ScanSchedule schedule)
        {
            schedule.Sch_lastRun = DateTime.UtcNow.AddHours(7);

            if (schedule.Sch_frequency == "Hourly")
                schedule.Sch_nextRun = schedule.Sch_nextRun?.AddHours(1);
            else if (schedule.Sch_frequency == "Daily")
                schedule.Sch_nextRun = schedule.Sch_nextRun?.AddDays(1);
            else if (schedule.Sch_frequency == "Weekly")
                schedule.Sch_nextRun = schedule.Sch_nextRun?.AddDays(7);
            else if (schedule.Sch_frequency == "Once")
                schedule.Sch_isActive = false;
        }

        private async Task ExecuteParallelScanAsync(List<int> branchIds, List<int> ports, string title)
        {
            try 
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var scanService = scope.ServiceProvider.GetRequiredService<PortScanService>();
                
                var branches = await context.Branches.Where(b => branchIds.Contains(b.Branch_id)).ToListAsync();
                var branchTasks = new List<Task>();

                foreach (var branch in branches)
                {
                    branchTasks.Add(ProcessSingleBranchAsync(scanService, branch, ports, title));
                }

                await Task.WhenAll(branchTasks);
                _logger.LogInformation($"Jadwal '{title}' Selesai Sepenuhnya.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saat eksekusi background scan '{title}'");
            }
        }

        private async Task ProcessSingleBranchAsync(PortScanService scanService, Models.Branch branch, List<int> ports, string title)
        {
            var allIps = scanService.ExpandCidr(branch.Branch_cidr);
            
            var aliveHosts = await scanService.GetAliveHostsAsync(allIps, 100, 3000, 3);

            if (!aliveHosts.Any()) return;

            var hostTasks = new List<Task>();
            var results = new List<IpScanResultDto>();

            foreach (var ip in aliveHosts)
            {
                hostTasks.Add(Task.Run(async () =>
                {
                    await _globalSemaphore.WaitAsync();
                    try
                    {
                        var portResults = new List<PortScanResultDto>();
                        
                        foreach (var port in ports)
                        {
                            bool isOpen = await scanService.ScanPortAsync(ip, port, 1000);
                            
                            portResults.Add(new PortScanResultDto 
                            { 
                                Port = port, 
                                Status = isOpen,
                                Severity = "Low"
                            });
                        }

                        lock (results)
                        {
                            results.Add(new IpScanResultDto { Ip = ip, Ports = portResults });
                        }
                    }
                    finally
                    {
                        _globalSemaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(hostTasks);

            if (results.Any())
            {
                await scanService.BulkSaveResultsAsync(branch.Branch_id, results, title, "Scheduled Scan");
            }
        }
    }
}