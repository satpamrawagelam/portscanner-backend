using Microsoft.AspNetCore.Mvc;
using portscanner_backend.Services;
using portscanner_backend.Models.Dto;

namespace portscanner_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class DashboardController : ControllerBase
    {
        private readonly DashboardService _service;

        public DashboardController(DashboardService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetOverview()
        {
            var data = await _service.GetPortStatusOverview();
            return Ok(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetOverviewHost()
        {
            var data = await _service.GetPortStatusOverviewHost();
            return Ok(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetBranchHealth()
        {
            var data = await _service.GetBranchHealthAsync();
            return Ok(data);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBranchDetail(int id)
        {
            var data = await _service.GetBranchDetailAsync(id);
            if (data == null) return NotFound("Branch not found or no scan data");
            return Ok(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetGlobalTrend([FromQuery] int? month, [FromQuery] int? year)
        {
            var data = await _service.GetGlobalTrendAsync(month, year);
            return Ok(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetRiskDistribution()
        {
            var data = await _service.GetRiskDistributionAsync();
            return Ok(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetVulnerableHosts()
        {
            var data = await _service.GetVulnerableHostsAsync();
            return Ok(data);
        }
    }
}