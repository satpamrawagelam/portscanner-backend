using portscanner_backend.Models.Dto;
using portscanner_backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace portscanner_backend.Services
{
    public class DashboardService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        public DashboardService(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<DashboardPortStatusOverviewDto> GetPortStatusOverview()
        {
            return await _cache.GetOrCreateAsync("DashboardOverview", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                var result = await _context.DashboardOverviews
                    .FromSqlRaw("EXEC V2_sp_GetDashboardOverview")
                    .ToListAsync();
                return result.FirstOrDefault() ?? new DashboardPortStatusOverviewDto();
            });
        }

        public async Task<DashboardPortStatusOverviewDtoHost> GetPortStatusOverviewHost()
        {
            return await _cache.GetOrCreateAsync("DashboardOverviewHost", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                var result = await _context.DashboardOverviewsHost
                    .FromSqlRaw("EXEC V2_sp_GetDashboardOverviewHost")
                    .ToListAsync();
                return result.FirstOrDefault() ?? new DashboardPortStatusOverviewDtoHost();
            });
        }


        public async Task<List<BranchHealthOverviewDto>> GetBranchHealthAsync()
        {
            return await _cache.GetOrCreateAsync("BranchHealth", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await _context.BranchHealthOverviews
                    .FromSqlRaw("EXEC V2_sp_GetBranchHealth")
                    .ToListAsync();
            });
        }

        public async Task<BranchDetailDto?> GetBranchDetailAsync(int branchId)
        {
            var rawData = await _context.BranchDetailRaws
                .FromSqlRaw("EXEC V2_sp_GetBranchDetail @BranchId", new SqlParameter("@BranchId", branchId))
                .ToListAsync();

            if (!rawData.Any()) return null;

            var result = new BranchDetailDto
            {
                BranchName = rawData.First().BranchName,
                BranchCidr = rawData.First().BranchCidr,
                TotalHost = rawData.Select(x => x.IpAddress).Distinct().Count()
            };

            var grouped = rawData.GroupBy(x => x.IpAddress);
            foreach (var grp in grouped)
            {
                var firstRow = grp.First();
                var ipDto = new IpPortStatusDto
                {
                    Ip = grp.Key,
                    HostStatus = firstRow.HostStatus,
                    Ports = grp.Select(x => new PortStatusDto
                    {
                        Port = x.PortNumber,
                        Service = x.ServiceName,
                        Status = x.IsOpen,
                        IsWhitelisted = x.IsWhitelisted,
                        Severity = x.Severity
                    }).OrderByDescending(p => p.Status).ThenBy(p => p.Port).ToList()
                };
                result.Results.Add(ipDto);
            }
            return result;
        }

        public async Task<List<GlobalTrendDto>> GetGlobalTrendAsync(int? month, int? year)
        {
            return await _cache.GetOrCreateAsync($"GlobalTrend_{month}_{year}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                var pMonth = new SqlParameter("@Month", month ?? DateTime.Now.Month);
                var pYear = new SqlParameter("@Year", year ?? DateTime.Now.Year);

                return await _context.GlobalTrends
                    .FromSqlRaw("EXEC V2_sp_GetGlobalTrend @Month, @Year", pMonth, pYear)
                    .ToListAsync();
            });
        }

        public async Task<List<RiskDistributionDto>> GetRiskDistributionAsync()
        {
            return await _cache.GetOrCreateAsync("RiskDistribution", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await _context.RiskDistributions
                    .FromSqlRaw("EXEC V2_sp_GetRiskDistribution")
                    .ToListAsync();
            });
        }
    }
}
