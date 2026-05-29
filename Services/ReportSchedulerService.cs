using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using portscanner_backend.Services;
using portscanner_backend.Models;
using portscanner_backend.Data;

namespace portscanner_backend.Services
{
    public class ReportSchedulerService : BackgroundService
    {
        private readonly ILogger<ReportSchedulerService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        // Schedule check interval: every 1 hour
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

        public ReportSchedulerService(ILogger<ReportSchedulerService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Report Scheduler Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Check if we should generate a Weekly Report
                    // Generate on Monday at 00:00 (or near it)
                    var now = DateTime.Now;

                    if (now.DayOfWeek == DayOfWeek.Monday && now.Hour == 0)
                    {
                        _logger.LogInformation("Triggering Weekly Report Generation...");
                        // Ambil 1 minggu penuh (Senin 00:00:00 s.d Minggu 23:59:59)
                        var prevWeekStart = now.Date.AddDays(-7);
                        var prevWeekEnd = now.Date.AddSeconds(-1);

                        await GenerateReportAsync("Weekly", prevWeekStart, prevWeekEnd);
                        // Sleep a bit more to avoid generating twice in the same hour
                        await Task.Delay(TimeSpan.FromHours(2), stoppingToken);
                        continue;
                    }

                    // Check for Monthly Report (e.g. 1st day of the month)
                    if (now.Day == 1 && now.Hour == 0)
                    {
                        _logger.LogInformation("Triggering Monthly Report Generation...");
                        // Ambil 1 bulan penuh di bulan sebelumnya (Misal: 1 Mei 00:00:00 s.d 31 Mei 23:59:59)
                        var prevMonthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                        var prevMonthEnd = new DateTime(now.Year, now.Month, 1).AddSeconds(-1);

                        await GenerateReportAsync("Monthly", prevMonthStart, prevMonthEnd);
                        await Task.Delay(TimeSpan.FromHours(2), stoppingToken);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing Report Scheduler.");
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }

        private async Task GenerateReportAsync(string type, DateTime start, DateTime end)
        {
            using var scope = _scopeFactory.CreateScope();
            var generator = scope.ServiceProvider.GetRequiredService<ReportGeneratorService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            string title = $"{type} Exposure Report - {start:dd MMM} to {end:dd MMM yyyy}";

            // 1. Buat record dengan status "generating" di database
            var reportRecord = new GeneratedReport
            {
                Title = title,
                Type = type,
                DateStart = start,
                DateEnd = end,
                FilePath = "generating",
                CreatedAt = DateTime.Now
            };

            dbContext.GeneratedReports.Add(reportRecord);
            await dbContext.SaveChangesAsync();
            int reportId = reportRecord.Id;

            try
            {
                // 2. Lakukan proses generate PDF dan update statusnya ke path file PDF asli
                await generator.GenerateReportAsync(start, end, title, type, reportId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Gagal memproses pembuatan laporan terjadwal {type} (ID: {reportId}). Menghapus record...");
                try
                {
                    var record = await dbContext.GeneratedReports.FindAsync(reportId);
                    if (record != null)
                    {
                        dbContext.GeneratedReports.Remove(record);
                        await dbContext.SaveChangesAsync();
                    }
                }
                catch (Exception deleteEx)
                {
                    _logger.LogError(deleteEx, $"Gagal menghapus record report terjadwal (ID: {reportId}) setelah proses gagal.");
                }
            }
        }
    }
}
