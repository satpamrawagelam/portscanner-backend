using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Services;
using portscanner_backend.Models;
using Microsoft.Extensions.Configuration;

namespace portscanner_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ReportGeneratorService _reportGenerator;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReportController> _logger;
        private readonly IConfiguration _config;

        public ReportController(AppDbContext context, ReportGeneratorService reportGenerator, IServiceScopeFactory scopeFactory, ILogger<ReportController> logger, IConfiguration config)
        {
            _context = context;
            _reportGenerator = reportGenerator;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _config = config;
        }

        // GET: api/Report/List
        [HttpGet("List")]
        public async Task<IActionResult> GetList()
        {
            try
            {
                var reports = await _context.GeneratedReports
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync();
                
                return Ok(new { success = true, data = reports });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // DELETE: api/Report/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var report = await _context.GeneratedReports.FindAsync(id);
                if (report == null)
                    return NotFound(new { success = false, message = "Report not found" });

                // Delete physical file from the external configured folder
                var outputPath = _config["ReportSettings:OutputPath"] ?? "../Reports";
                string reportsFolder = Path.IsPathRooted(outputPath)
                    ? outputPath
                    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), outputPath));

                string fileName = Path.GetFileName(report.FilePath);
                string physicalPath = Path.Combine(reportsFolder, fileName);

                if (System.IO.File.Exists(physicalPath))
                {
                    System.IO.File.Delete(physicalPath);
                }

                // Delete companion Excel file if it exists
                string xlsxFileName = Path.ChangeExtension(fileName, ".xlsx");
                string xlsxPhysicalPath = Path.Combine(reportsFolder, xlsxFileName);
                if (System.IO.File.Exists(xlsxPhysicalPath))
                {
                    System.IO.File.Delete(xlsxPhysicalPath);
                }

                // Delete DB record
                _context.GeneratedReports.Remove(report);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Report deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // POST: api/Report/GenerateManual
        // Ini untuk ngetest generate report secara manual di latar belakang
        [HttpPost("GenerateManual")]
        public async Task<IActionResult> GenerateManual([FromQuery] string type = "Weekly")
        {
            try
            {
                DateTime dateEnd = DateTime.Now;
                DateTime dateStart = type.ToLower() == "weekly" ? dateEnd.AddDays(-7) : dateEnd.AddMonths(-1);
                string title = $"{type} Exposure Report - {dateStart:dd MMM} to {dateEnd:dd MMM yyyy}";

                // 1. Buat record laporan terlebih dahulu dengan status "generating" agar tampil di UI laporan
                var reportRecord = new GeneratedReport
                {
                    Title = title,
                    Type = type,
                    DateStart = dateStart,
                    DateEnd = dateEnd,
                    FilePath = "generating",
                    CreatedAt = DateTime.Now
                };

                _context.GeneratedReports.Add(reportRecord);
                await _context.SaveChangesAsync();
                int reportId = reportRecord.Id;

                // 2. Jalankan proses generate report di latar belakang agar tidak memicu timeout pada browser
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var generator = scope.ServiceProvider.GetRequiredService<ReportGeneratorService>();
                        await generator.GenerateReportAsync(dateStart, dateEnd, title, type, reportId);
                        _logger.LogInformation($"[BACKGROUND] Sukses membuat laporan manual {type} (ID: {reportId}) untuk periode {dateStart:dd MMM yyyy} - {dateEnd:dd MMM yyyy}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"[BACKGROUND] Gagal membuat laporan manual {type} (ID: {reportId}) di latar belakang. Menghapus record...");
                        
                        // Hapus record dari database jika gagal agar tidak menggantung sebagai "generating" selamanya
                        try
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                            var record = await db.GeneratedReports.FindAsync(reportId);
                            if (record != null)
                            {
                                db.GeneratedReports.Remove(record);
                                await db.SaveChangesAsync();
                            }
                        }
                        catch (Exception deleteEx)
                        {
                            _logger.LogError(deleteEx, $"Gagal menghapus record report (ID: {reportId}) setelah proses gagal.");
                        }
                    }
                });

                return Ok(new { success = true, message = $"Pembuatan laporan {type} sedang diproses di latar belakang. Silakan refresh halaman laporan beberapa saat lagi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message, stack = ex.StackTrace });
            }
        }

        // POST: api/Report/GenerateFilteredReport
        // Ini untuk ngetest/membuat report dengan filter dari halaman history di background
        [HttpPost("GenerateFilteredReport")]
        public async Task<IActionResult> GenerateFilteredReport(
            [FromQuery] DateTime dateStart,
            [FromQuery] DateTime dateEnd,
            [FromQuery] string scanType = "all",
            [FromQuery] string? search = "",
            [FromQuery] string type = "Custom")
        {
            try
            {
                string title = $"{type} Exposure Report - {dateStart:dd MMM} s/d {dateEnd:dd MMM yyyy}";
                if (!string.IsNullOrWhiteSpace(search))
                {
                    title += $" (Filter: {search})";
                }

                // 1. Buat record laporan terlebih dahulu dengan status "generating" agar tampil di UI laporan
                var reportRecord = new GeneratedReport
                {
                    Title = title,
                    Type = type,
                    DateStart = dateStart,
                    DateEnd = dateEnd,
                    FilePath = "generating",
                    CreatedAt = DateTime.Now
                };

                _context.GeneratedReports.Add(reportRecord);
                await _context.SaveChangesAsync();
                int reportId = reportRecord.Id;

                // 2. Jalankan proses generate report di latar belakang agar tidak memicu timeout pada browser
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var generator = scope.ServiceProvider.GetRequiredService<ReportGeneratorService>();
                        await generator.GenerateReportAsync(dateStart, dateEnd, title, type, reportId, scanType, search ?? "");
                        _logger.LogInformation($"[BACKGROUND] Sukses membuat laporan terfilter {type} (ID: {reportId})");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"[BACKGROUND] Gagal membuat laporan terfilter {type} (ID: {reportId}) di latar belakang. Menghapus record...");
                        
                        // Hapus record dari database jika gagal agar tidak menggantung sebagai "generating" selamanya
                        try
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                            var record = await db.GeneratedReports.FindAsync(reportId);
                            if (record != null)
                            {
                                db.GeneratedReports.Remove(record);
                                await db.SaveChangesAsync();
                            }
                        }
                        catch (Exception deleteEx)
                        {
                            _logger.LogError(deleteEx, $"Gagal menghapus record report (ID: {reportId}) setelah proses gagal.");
                        }
                    }
                });

                return Ok(new { success = true, message = $"Pembuatan laporan terfilter {type} sedang diproses di latar belakang. Silakan cek menu 'Report' beberapa saat lagi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message, stack = ex.StackTrace });
            }
        }
    }
}
