using portscanner_backend.Models.Dto;
using portscanner_backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace portscanner_backend.Services
{
    public class DashboardService
    {
        private readonly AppDbContext _context;

        public DashboardService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardPortStatusOverviewDto> GetPortStatusOverview()
        {
            var result = await _context.DashboardOverviews
                .FromSqlRaw("EXEC sp_GetDashboardOverview")
                .ToListAsync();

            return result.FirstOrDefault() ?? new DashboardPortStatusOverviewDto();
        }

        public async Task<List<BranchHealthOverviewDto>> GetBranchHealthAsync()
        {
            var result = await _context.BranchHealthOverviews
                .FromSqlRaw("EXEC sp_GetBranchHealth")
                .ToListAsync();

            return result;
        }

        public async Task<BranchDetailDto?> GetBranchDetailAsync(int branchId)
        {
            var rawData = await _context.BranchDetailRaws
                .FromSqlRaw("EXEC sp_GetBranchDetail @BranchId", new SqlParameter("@BranchId", branchId))
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
                var firtsRow = grp.First();

                var ipDto = new IpPortStatusDto
                {
                    Ip = grp.Key,
                    HostStatus = firtsRow.HostStatus,
                    Ports = grp.Select(x => new PortStatusDto
                    {
                        Port = x.PortNumber,
                        Service = x.ServiceName,
                        Status = x.IsOpen,
                        Severity = x.Severity
                    }).OrderByDescending(p => p.Status).ThenBy(p => p.Port).ToList()
                };
                result.Results.Add(ipDto);
            }
            return result;
        }

        public async Task<List<GlobalTrendDto>> GetGlobalTrendAsync(int? month, int? year)
        {
            var pMonth = new SqlParameter("@Month", month ?? DateTime.Now.Month);
            var pYear = new SqlParameter("@Year", year ?? DateTime.Now.Year);

            var data = await _context.GlobalTrends
                .FromSqlRaw("EXEC sp_GetGlobalTrend @Month, @Year", pMonth, pYear)
                .ToListAsync();

            return data;
        }

        public async Task<List<RiskDistributionDto>> GetRiskDistributionAsync()
        {
            var data = await _context.RiskDistributions
                .FromSqlRaw("EXEC sp_GetRiskDistribution")
                .ToListAsync();
            return data;
        }
    }
}