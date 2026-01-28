using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Models;

namespace portscanner_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class ConfigController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ConfigController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetConfig()
        {
            var config = await _context.AppConfigs.FirstOrDefaultAsync();
            if (config == null) 
            {
                config = new AppConfig(); 
            }
            return Ok(config);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateConfig([FromBody] AppConfig update)
        {
            var config = await _context.AppConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                update.Id = 1;
                _context.AppConfigs.Add(update);
            }
            else
            {
                config.PingTimeout = update.PingTimeout;
                config.PingRetries = update.PingRetries;
                config.PortScanTimeout = update.PortScanTimeout;
                config.MaxConcurrency = update.MaxConcurrency;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Configuration updated", data = config });
        }
    }
}