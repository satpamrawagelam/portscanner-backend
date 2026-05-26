using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Services;
using portscanner_backend.Models;
using portscanner_backend.Models.Dto;
using portscanner_backend.Controllers;
using System.Collections.Concurrent;
using System.Text;

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

            var now = DateTime.UtcNow.AddHours(7);

            var dueSchedules = await context.ScanSchedules
                .Where(s => s.Sch_isActive && s.Sch_nextRun <= now)
                .ToListAsync();

            var temp = await context.ScanSchedules.ToListAsync();
            _logger.LogInformation("JUMLAH : "+ temp.Count.ToString());    

            if (!dueSchedules.Any()){
                _logger.LogInformation("TIDAK ADA JADWAL YANG HARUS DIJALANKAN (" + now.ToString("dd-MM-yyyy HH:mm:ss") + ")");
                return;
            } 

            var portSeverities = await context.PortMasters
                .Select(p => p.Pm_port_number)
                .Distinct()
                .ToDictionaryAsync(port => port, port => "Medium");

            var config = await context.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig
            {
                MaxConcurrency = 50,
                PingTimeout = 3000,
                PortScanTimeout = 1000
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
                        await ProcessScheduleParallelAsync(schedule, targetBranchIds, targetPorts, portSeverities, config);
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
            if (schedule.Sch_portMode == "single" || schedule.Sch_portMode == "Custom")
            {
                return await context.ScanSchedulePorts
                    .Include(sp => sp.PortMaster)
                    .Where(sp => sp.Sch_id == schedule.Sch_id)
                    .Select(sp => sp.PortMaster!.Pm_port_number)
                    .ToListAsync();
            }

            if (schedule.Sch_portMode == "group" && schedule.Sch_pgId.HasValue)
            {
                if (schedule.Sch_pgId.Value == 0)
                {
                    return await context.PortMasters.Select(pm => pm.Pm_port_number).Distinct().ToListAsync();
                }

                return await context.PortMasters
                    .Where(pm => pm.Pg_id == schedule.Sch_pgId)
                    .Select(pm => pm.Pm_port_number)
                    .Distinct()
                    .ToListAsync();
            }

            if (schedule.Sch_portMode == "all")
            {
                return await context.PortMasters.Select(pm => pm.Pm_port_number).Distinct().ToListAsync();
            }

            return new List<int>();
        }

        private async Task ProcessScheduleParallelAsync(
            Models.ScanSchedule schedule,
            List<int> branchIds,
            List<int> ports,
            Dictionary<int, string> portSeverities,
            Models.AppConfig config)
        {
            // Ambil data detail branch dulu (bisa pakai context luar karena cuma baca)
            // Atau lebih aman ambil di dalam scope masing-masing jika mau benar-benar terisolasi.
            // Disini kita ambil list CIDR-nya dulu biar tidak passing DbContext ke Task.
            
            // Kita butuh CIDR dan ID, jadi kita query dulu sebentar pakai Scope temporary atau context yang dipassing (aman karena await sequential)
            List<Models.Branch> branches;
            using (var scope = _serviceProvider.CreateScope())
            {
                var tmpContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                branches = await tmpContext.Branches
                    .Where(b => branchIds.Contains(b.Branch_id))
                    .AsNoTracking()
                    .ToListAsync();
            }

            // Create a single session for this schedule run
            int sessionId;
            DateTime scanDate;
            using (var scope = _serviceProvider.CreateScope())
            {
                var scopedScanService = scope.ServiceProvider.GetRequiredService<PortScanService>();
                var sessionObj = await scopedScanService.CreateSessionAsync(schedule.Sch_title, "Scheduled Scan");
                sessionId = sessionObj.Session_id;
                scanDate = sessionObj.ScanDate;
            }

            var tasks = new List<Task>();
            var allChanges = new ConcurrentBag<PortChangeAlertDto>();

            foreach (var branch in branches)
            {
                var currentBranch = branch;

                tasks.Add(Task.Run(async () =>
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var scopedScanService = scope.ServiceProvider.GetRequiredService<PortScanService>();
                        
                        try
                        {
                            _logger.LogInformation($"Scanning Branch {currentBranch.Branch_name}...");

                            var scanResults = await scopedScanService.ExecuteSubnetScanAsync(
                                currentBranch.Branch_cidr,
                                ports
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
                                var branchChanges = await scopedScanService.BulkSaveResultsAsync(
                                    currentBranch.Branch_id,
                                    scanResults,
                                    sessionId,
                                    scanDate,
                                    schedule.Sch_id
                                );

                                foreach (var chg in branchChanges)
                                {
                                    allChanges.Add(new PortChangeAlertDto
                                    {
                                        BranchName = currentBranch.Branch_name,
                                        IpAddress = chg.IpAddress,
                                        PortNumber = chg.PortNumber,
                                        IsNowOpen = chg.IsNowOpen
                                    });
                                }
                            }
                            
                            _logger.LogInformation($"Selesai Branch {currentBranch.Branch_name}.");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Gagal scan branch {currentBranch.Branch_name} pada jadwal {schedule.Sch_title}");
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            _logger.LogInformation($"[DONE] Semua task branch untuk jadwal '{schedule.Sch_title}' selesai.");

            // Logika Telegram Alert (Rekap vs Temuan Baru)
            try
            {
                var now = DateTime.UtcNow.AddHours(7);
                if (now.Hour == 0) 
                {
                    _logger.LogInformation($"INI RECAPPP");
                    using var scope = _serviceProvider.CreateScope();
                    var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var openPorts = await ctx.HostPorts
                        .Include(hp => hp.IpAddress)
                        .ThenInclude(ip => ip.Branch)
                        .Where(hp => branchIds.Contains(hp.IpAddress.Ip_branchId) && hp.Status == true)
                        .AsNoTracking()
                        .ToListAsync();

                    if (openPorts.Any())
                    {
                        var msgBuilder = new StringBuilder();
                        msgBuilder.AppendLine($"📅 <b>[DAILY RECAP] {schedule.Sch_title}</b>");
                        msgBuilder.AppendLine("Daftar Host & Port Terbuka:\n");

                        var grouped = openPorts.GroupBy(hp => hp.IpAddress.Branch.Branch_name);
                        foreach (var g in grouped)
                        {
                            string branchHeader = $"🏢 <b>{g.Key}</b>";
                            
                            // Cek apakah muat untuk nulis nama Cabang
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

                                // Cek apakah muat untuk nulis baris IP & Port ini
                                if (msgBuilder.Length + line.Length > 3500)
                                {
                                    // Kalau udah kepanjangan, kirim dulu!
                                    await AlertController.SendAlertAsync(msgBuilder.ToString());
                                    msgBuilder.Clear();
                                    
                                    // Karena ini pesan baru, kita kasih tau lagi ini cabang yang mana (Biar user gak bingung)
                                    msgBuilder.AppendLine($"🏢 <b>{g.Key} (Lanjutan)</b>");
                                }
                                msgBuilder.AppendLine(line);
                            }
                            msgBuilder.AppendLine(); // Kasih jarak enter antar cabang
                        }
                        
                        // Terakhir, kirim sisa teks yang belum sempat terkirim di dalam loop
                        if (msgBuilder.Length > 0 && msgBuilder.ToString().Trim() != "")
                        {
                            await AlertController.SendAlertAsync(msgBuilder.ToString());
                        }
                    }
                }
                else 
                {
                    if (allChanges.Any())
                    {
                        _logger.LogInformation($"INI ALERT PERUBAHANNN (RECAP FORMAT)");
                        using var scope = _serviceProvider.CreateScope();
                        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        var openPorts = await ctx.HostPorts
                            .Include(hp => hp.IpAddress)
                            .ThenInclude(ip => ip.Branch)
                            .Where(hp => branchIds.Contains(hp.IpAddress.Ip_branchId) && hp.Status == true)
                            .AsNoTracking()
                            .ToListAsync();

                        if (openPorts.Any())
                        {
                            var msgBuilder = new StringBuilder();
                            msgBuilder.AppendLine($"⚠️ <b>[ALERT PERUBAHAN] {schedule.Sch_title}</b>");
                            msgBuilder.AppendLine("Daftar Host & Port Terbuka saat ini:\n");

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
                                msgBuilder.AppendLine($"\nTerakhir dijalankan pada {schedule.Sch_lastRun}");
                                await AlertController.SendAlertAsync(msgBuilder.ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gagal mengirim notifikasi Telegram");
            }
        }

        private void UpdateNextRun(Models.ScanSchedule schedule)
        {
            schedule.Sch_lastRun = DateTime.UtcNow.AddHours(7);

            if (schedule.Sch_frequency == "5 Minutes")
                schedule.Sch_nextRun = schedule.Sch_nextRun?.AddMinutes(5);
            else if (schedule.Sch_frequency == "20 Minutes")
                schedule.Sch_nextRun = schedule.Sch_nextRun?.AddMinutes(20);
            else if (schedule.Sch_frequency == "Hourly")
                schedule.Sch_nextRun = schedule.Sch_nextRun?.AddHours(1);
            else if (schedule.Sch_frequency == "Daily")
                schedule.Sch_nextRun = schedule.Sch_nextRun?.AddDays(1);
            else if (schedule.Sch_frequency == "Weekly")
                schedule.Sch_nextRun = schedule.Sch_nextRun?.AddDays(7);
            else if (schedule.Sch_frequency == "Once")
                schedule.Sch_isActive = false;
        }
    }

    public class PortChangeAlertDto
    {
        public string BranchName { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int PortNumber { get; set; }
        public bool IsNowOpen { get; set; }
    }
}