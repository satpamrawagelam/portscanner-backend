using Microsoft.EntityFrameworkCore;
using portscanner_backend.Models;
using portscanner_backend.Models.Dto;

namespace portscanner_backend.Data 
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Branch> Branches { get; set; }
        public DbSet<IpAddress> IpAddresses { get; set; }
        public DbSet<PortMaster> PortMasters { get; set; }
        public DbSet<PortGroup> PortGroups { get; set; }
        public DbSet<HostPort> HostPorts { get; set; }
        public DbSet<AppConfig> AppConfigs { get; set; }
        public DbSet<ScanSchedule> ScanSchedules { get; set; }
        public DbSet<ScanScheduleTarget> ScanScheduleTargets { get; set; }
        public DbSet<ScanSchedulePort> ScanSchedulePorts { get; set; }
        public DbSet<ScanSession> ScanSessions { get; set; }
        public DbSet<ScanHostResult> ScanHostResults { get; set; }
        public DbSet<ScanPortResult> ScanPortResults { get; set; }

        public DbSet<DashboardPortStatusOverviewDto> DashboardOverviews { get; set; }
        public DbSet<BranchHealthOverviewDto> BranchHealthOverviews { get; set; }
        public DbSet<BranchDetailRawDto> BranchDetailRaws { get; set; }
        public DbSet<GlobalTrendDto> GlobalTrends { get; set; }
        public DbSet<RiskDistributionDto> RiskDistributions { get; set; }
        public DbSet<ScanHistoryDto> ScanHistoryLogs { get; set; }

        
        

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DashboardPortStatusOverviewDto>().HasNoKey();
            modelBuilder.Entity<BranchHealthOverviewDto>().HasNoKey();
            modelBuilder.Entity<PortGroupResult>().HasNoKey();
            modelBuilder.Entity<BranchDetailRawDto>().HasNoKey();
            modelBuilder.Entity<GlobalTrendDto>().HasNoKey();
            modelBuilder.Entity<RiskDistributionDto>().HasNoKey();
            modelBuilder.Entity<ScanHistoryDto>().HasNoKey();
        }
    }
}