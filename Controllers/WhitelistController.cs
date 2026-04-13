using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using portscanner_backend.Data;
using portscanner_backend.Models.Dto;

namespace portscanner_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class WhitelistController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        public WhitelistController(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpPost]
        public async Task<IActionResult> UpdateList([FromBody] WhitelistUpdateDto request)
        {
            if (string.IsNullOrWhiteSpace(request.IpAddress))
                return BadRequest(new { message = "IP Address is required" });

            string portsCsv = request.WhitelistedPorts != null && request.WhitelistedPorts.Any() 
                ? string.Join(",", request.WhitelistedPorts) 
                : "";

            var ipParam = new SqlParameter("@IpAddress", request.IpAddress);
            var portsParam = new SqlParameter("@PortsCsv", portsCsv);

            try
            {
                await _context.Database.ExecuteSqlRawAsync("EXEC sp_UpdateWhitelist @IpAddress, @PortsCsv", ipParam, portsParam);
                return Ok(new { message = "Whitelist updated successfully" });
            }
            catch (Exception ex)
            {
                var sqlEx = ex.InnerException as SqlException ?? ex as SqlException;
                if (sqlEx != null && sqlEx.Number == 50001)
                {
                    return NotFound(new { message = sqlEx.Message });
                }

                return StatusCode(500, new { message = "Internal server error", detail = ex.Message });
            }
        }
    }
}
