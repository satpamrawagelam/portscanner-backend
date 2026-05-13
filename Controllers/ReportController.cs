using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Services;

namespace portscanner_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ReportGeneratorService _reportGenerator;

        public ReportController(AppDbContext context, ReportGeneratorService reportGenerator)
        {
            _context = context;
            _reportGenerator = reportGenerator;
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

                // Delete physical file
                string webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                // Remove leading slash if any
                string relativePath = report.FilePath.TrimStart('/', '\\');
                string physicalPath = Path.Combine(webRoot, relativePath);

                if (System.IO.File.Exists(physicalPath))
                {
                    System.IO.File.Delete(physicalPath);
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
        // Ini untuk ngetest generate report secara manual
        [HttpPost("GenerateManual")]
        public async Task<IActionResult> GenerateManual([FromQuery] string type = "Weekly")
        {
            try
            {
                DateTime dateEnd = DateTime.Now;
                DateTime dateStart = type.ToLower() == "weekly" ? dateEnd.AddDays(-7) : dateEnd.AddMonths(-1);
                string title = $"{type} Exposure Report - {dateStart:dd MMM} to {dateEnd:dd MMM yyyy}";

                await _reportGenerator.GenerateReportAsync(dateStart, dateEnd, title, type);

                return Ok(new { success = true, message = $"{type} report generated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message, stack = ex.StackTrace });
            }
        }
    }
}
