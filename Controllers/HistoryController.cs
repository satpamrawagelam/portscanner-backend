using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using portscanner_backend.Data;
using portscanner_backend.Models.Dto;

namespace portscanner_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class HistoryController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        public HistoryController(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
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
                string cacheKey = $"History_{scanType}_{page}_{pageSize}_{search}";

                var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

                    var pScanType = new Microsoft.Data.SqlClient.SqlParameter("@ScanType", scanType);
                    var pSearch = new Microsoft.Data.SqlClient.SqlParameter("@SearchTerm", search ?? "");
                    var pPage = new Microsoft.Data.SqlClient.SqlParameter("@PageNumber", page);
                    var pSize = new Microsoft.Data.SqlClient.SqlParameter("@PageSize", pageSize);

                    var rawData = await _context.Set<ScanHistoryDto>()
                        .FromSqlRaw("EXEC V2_sp_GetScanHistoryLog @ScanType, @SearchTerm, @PageNumber, @PageSize", 
                            pScanType, pSearch, pPage, pSize)
                        .ToListAsync();

                    return new 
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        Data = rawData
                    };
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
