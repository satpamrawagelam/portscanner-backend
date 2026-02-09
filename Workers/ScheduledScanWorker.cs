using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Services;
using portscanner_backend.Models;
using portscanner_backend.Models.Dto;

namespace portscanner_backend.Workers
{
    public class ScheduledScanWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScheduledScanWorker> _logger;

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
                    _logger.LogError(ex, "Error Fatal di Worker");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task CheckAndExecuteSchedules()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var scanService = scope.ServiceProvider.GetRequiredService<PortScanService>();

            var now = DateTime.UtcNow.AddHours(7);

            var dueSchedules = await context.ScanSchedules
                .Where(s => s.Sch_isActive && s.Sch_nextRun <= now)
                .ToListAsync();

            if (!dueSchedules.Any()) return;

            var portSeverities = await context.PortMasters
                .ToDictionaryAsync(p => p.Pm_portNumber, p => p.Pm_severity);

            var config = await context.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig 
            { 
                MaxConcurrency = 50, PingTimeout = 1000, PortScanTimeout = 1000 
            };

            foreach (var schedule in dueSchedules)
            {
                _logger.LogInformation($"[START] Eksekusi Jadwal: {schedule.Sch_title} (ID: {schedule.Sch_id})");

                try 
                {
                    var targetBranchIds = await context.ScanScheduleTargets
                        .Where(t => t.Tgt_schId == schedule.Sch_id)
                        .Select(t => t.Tgt_branchId)
                        .ToListAsync();

                    List<int> targetPorts = await ResolveTargetPorts(context, schedule);

                    if (targetBranchIds.Any() && targetPorts.Any())
                    {
                        await ProcessScheduleAsync(scanService, context, schedule, targetBranchIds, targetPorts, portSeverities, config);
                    }
                    else
                    {
                        _logger.LogWarning($"Jadwal {schedule.Sch_title} dilewati: Tidak ada Branch atau Port target.");
                    }

                    UpdateNextRun(schedule);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Gagal memproses jadwal {schedule.Sch_title}");
                }
            }

            await context.SaveChangesAsync();
        }

        private async Task<List<int>> ResolveTargetPorts(AppDbContext context, Models.ScanSchedule schedule)
        {
            if (schedule.Sch_portMode == "single" && schedule.Sch_targetManualPort.HasValue)
            {
                return new List<int> { schedule.Sch_targetManualPort.Value };
            }
            
            if (schedule.Sch_portMode == "group" && schedule.Sch_targetPortGroupId.HasValue)
            {
                if (schedule.Sch_targetPortGroupId.Value == 0)
                {
                    return await context.PortMasters.Select(pm => pm.Pm_portNumber).Distinct().ToListAsync();
                }
                
                return await context.PortMasters
                    .Where(pm => pm.Pm_portGroup == schedule.Sch_targetPortGroupId)
                    .Select(pm => pm.Pm_portNumber)
                    .ToListAsync();
            }
            
            if (schedule.Sch_portMode == "all")
            {
                return await context.PortMasters.Select(pm => pm.Pm_portNumber).Distinct().ToListAsync();
            }

            return new List<int>();
        }

        private async Task ProcessScheduleAsync(
            PortScanService scanService, 
            AppDbContext context,
            Models.ScanSchedule schedule, 
            List<int> branchIds, 
            List<int> ports,
            Dictionary<int, string> portSeverities,
            Models.AppConfig config)
        {
            var branches = await context.Branches.Where(b => branchIds.Contains(b.Branch_id)).ToListAsync();
            var tasks = new List<Task>();

            foreach (var branch in branches)
            {
                tasks.Add(Task.Run(async () => 
                {
                    try 
                    {
                        var scanResults = await scanService.ExecuteSubnetScanAsync(
                            branch.Branch_cidr,
                            ports,
                            config.MaxConcurrency,
                            config.PingTimeout,
                            config.PortScanTimeout
                        );

                        foreach (var host in scanResults)
                        {
                            foreach (var portResult in host.Ports)
                            {
                                portResult.Severity = portSeverities.ContainsKey(portResult.Port) 
                                    ? portSeverities[portResult.Port] 
                                    : "Medium";
                            }
                        }

                        if (scanResults.Any())
                        {
                            await scanService.BulkSaveResultsAsync(
                                branch.Branch_id, 
                                scanResults, 
                                schedule.Sch_title, 
                                "Scheduled Scan", 
                                schedule.Sch_id
                            ); 
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Gagal scan branch {branch.Branch_name} pada jadwal {schedule.Sch_title}");
                    }
                }));
            }

            await Task.WhenAll(tasks);
            _logger.LogInformation($"[DONE] Jadwal '{schedule.Sch_title}' selesai.");
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
    }
}