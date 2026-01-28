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

                // Cek jadwal setiap 1 menit
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task CheckAndExecuteSchedules()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow.AddHours(7); // WIB

            // 1. CARI JADWAL YANG SUDAH WAKTUNYA
            var dueSchedules = await context.ScanSchedules
                .Where(s => s.Sch_isActive && s.Sch_nextRun <= now)
                .ToListAsync();

            if (!dueSchedules.Any()) return;

            foreach (var schedule in dueSchedules)
            {
                _logger.LogInformation($"Mengeksekusi Jadwal: {schedule.Sch_title}");

                // 2. AMBIL LIST BRANCH TARGET
                var targetBranchIds = await context.ScanScheduleTargets
                    .Where(t => t.Tgt_schId == schedule.Sch_id)
                    .Select(t => t.Tgt_branchId)
                    .ToListAsync();

                // 3. AMBIL LIST PORT TARGET (Logic PortMaster)
                List<int> targetPorts = new List<int>();

                // A. Mode Single
                if (schedule.Sch_portMode == "single" && schedule.Sch_targetManualPort.HasValue)
                {
                    targetPorts.Add(schedule.Sch_targetManualPort.Value);
                }
                // B. Mode Group
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
                // C. Mode ALL PORTS (BARU)
                else if (schedule.Sch_portMode == "all")
                {
                    // Ambil SEMUA port unik yang ada di master
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
            // FASE 1: Discovery (Cari IP Hidup)
            var allIps = scanService.ExpandCidr(branch.Branch_cidr);
            
            // [CONFIG STATIC] PING SETTINGS (Bisa disesuaikan)
            var aliveHosts = await scanService.GetAliveHostsAsync(allIps, 100, 3000, 3);

            if (!aliveHosts.Any()) return;

            // FASE 2: Scan Port
            var hostTasks = new List<Task>();
            var results = new List<IpScanResultDto>();

            foreach (var ip in aliveHosts)
            {
                hostTasks.Add(Task.Run(async () =>
                {
                    // Minta tiket antrean (Global Limit)
                    await _globalSemaphore.WaitAsync();
                    try
                    {
                        var portResults = new List<PortScanResultDto>();
                        
                        foreach (var port in ports)
                        {
                            // [CONFIG STATIC] PORT SCAN TIMEOUT 1 Detik
                            bool isOpen = await scanService.ScanPortAsync(ip, port, 1000);
                            
                            portResults.Add(new PortScanResultDto 
                            { 
                                Port = port, 
                                Status = isOpen, // True atau False tetap dicatat
                                Severity = "Low" // Default logic (bisa dikembangkan ambil dari DB jika perlu)
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

            // FASE 3: Simpan Hasil
            if (results.Any())
            {
                // Panggil Service yang sama persis dengan Manual Scan
                await scanService.BulkSaveResultsAsync(branch.Branch_id, results, title, "Scheduled Scan");
            }
        }
    }
}