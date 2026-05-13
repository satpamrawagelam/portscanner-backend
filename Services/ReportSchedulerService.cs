using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using portscanner_backend.Services;

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
                    // For example, generate on Sunday at 00:00 (or near it)
                    var now = DateTime.Now;

                    if (now.DayOfWeek == DayOfWeek.Sunday && now.Hour == 0)
                    {
                        _logger.LogInformation("Triggering Weekly Report Generation...");
                        await GenerateReportAsync("Weekly", now.AddDays(-7), now);
                        // Sleep a bit more to avoid generating twice in the same hour
                        await Task.Delay(TimeSpan.FromHours(2), stoppingToken);
                        continue;
                    }

                    // Check for Monthly Report (e.g. 1st day of the month)
                    if (now.Day == 1 && now.Hour == 0)
                    {
                        _logger.LogInformation("Triggering Monthly Report Generation...");
                        await GenerateReportAsync("Monthly", now.AddMonths(-1), now);
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

            string title = $"{type} Exposure Report - {start:dd MMM} to {end:dd MMM yyyy}";
            await generator.GenerateReportAsync(start, end, title, type);
        }
    }
}
