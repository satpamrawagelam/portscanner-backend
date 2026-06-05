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
        public DbSet<GeneratedReport> GeneratedReports { get; set; }

        public DbSet<DashboardPortStatusOverviewDto> DashboardOverviews { get; set; }
        public DbSet<DashboardPortStatusOverviewDtoHost> DashboardOverviewsHost { get; set; }
        public DbSet<BranchHealthOverviewDto> BranchHealthOverviews { get; set; }
        public DbSet<BranchDetailRawDto> BranchDetailRaws { get; set; }
        public DbSet<GlobalTrendDto> GlobalTrends { get; set; }
        public DbSet<RiskDistributionDto> RiskDistributions { get; set; }
        public DbSet<ScanHistoryDto> ScanHistoryLogs { get; set; }

        public DbSet<CheckPortByHostDto> CheckPortByHostDtos { get; set; }
        public DbSet<CheckPortByBranchDto> CheckPortByBranchDtos { get; set; }
        public DbSet<CheckPortByBranchRawDto> CheckPortByBranchRawDtos { get; set; }


        
        

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Mapping EF Core Entities ke V2 Server Tables
            modelBuilder.Entity<Branch>().ToTable("V2_Branches");
            modelBuilder.Entity<IpAddress>().ToTable("V2_IpAddresses");
            modelBuilder.Entity<PortMaster>().ToTable("V2_PortMasters");
            modelBuilder.Entity<PortGroup>().ToTable("V2_PortGroups");
            modelBuilder.Entity<HostPort>().ToTable("V2_HostPorts");
            modelBuilder.Entity<AppConfig>().ToTable("V2_AppConfigs");
            modelBuilder.Entity<ScanSchedule>().ToTable("V2_ScanSchedules");
            modelBuilder.Entity<ScanScheduleTarget>().ToTable("V2_ScanScheduleTargets");
            modelBuilder.Entity<ScanSchedulePort>().ToTable("V2_ScanSchedulePorts");
            modelBuilder.Entity<ScanSession>().ToTable("V2_ScanSessions");
            modelBuilder.Entity<ScanHostResult>().ToTable("V2_ScanHostResults");
            modelBuilder.Entity<ScanPortResult>().ToTable("V2_ScanPortResults");
            modelBuilder.Entity<GeneratedReport>().ToTable("V2_GeneratedReports");

            // Keyless Entities
            modelBuilder.Entity<DashboardPortStatusOverviewDto>().HasNoKey();
            modelBuilder.Entity<BranchHealthOverviewDto>().HasNoKey();
            modelBuilder.Entity<PortGroupResult>().HasNoKey();
            modelBuilder.Entity<BranchDetailRawDto>().HasNoKey();
            modelBuilder.Entity<GlobalTrendDto>().HasNoKey();
            modelBuilder.Entity<RiskDistributionDto>().HasNoKey();
            modelBuilder.Entity<ScanHistoryDto>().HasNoKey();
            modelBuilder.Entity<DashboardPortStatusOverviewDtoHost>().HasNoKey();
            modelBuilder.Entity<CheckPortByHostDto>().HasNoKey();
            modelBuilder.Entity<CheckPortByBranchDto>().HasNoKey();
            modelBuilder.Entity<CheckPortByBranchRawDto>().HasNoKey();
            modelBuilder.Entity<SpesificPortStatusDto>().HasNoKey();
            modelBuilder.Entity<SpesificPortRequestDto>().HasNoKey();
            modelBuilder.Entity<SegmentPortRequestDto>().HasNoKey();
            modelBuilder.Entity<SegmentPortRawDto>().HasNoKey();
            modelBuilder.Entity<CheckPortsRequestDto>().HasNoKey();
        }
    }
}