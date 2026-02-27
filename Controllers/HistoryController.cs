using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Models.Dto;

namespace portscanner_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class HistoryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public HistoryController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetHistory(
            [FromQuery] string scanType = "manual", 
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = "")
        {
            try
            {
                var pScanType = new Microsoft.Data.SqlClient.SqlParameter("@ScanType", scanType);
                var pSearch = new Microsoft.Data.SqlClient.SqlParameter("@SearchTerm", search ?? "");
                var pPage = new Microsoft.Data.SqlClient.SqlParameter("@PageNumber", page);
                var pSize = new Microsoft.Data.SqlClient.SqlParameter("@PageSize", pageSize);

                var rawData = await _context.Set<ScanHistoryDto>()
                    .FromSqlRaw("EXEC sp_GetScanHistoryLog @ScanType, @SearchTerm, @PageNumber, @PageSize", 
                        pScanType, pSearch, pPage, pSize)
                    .ToListAsync();

                int totalRecords = rawData.FirstOrDefault()?.TotalRecords ?? 0;
                int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

                return Ok(new 
                {
                    TotalRecords = totalRecords,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalPages = totalPages,
                    Data = rawData
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}