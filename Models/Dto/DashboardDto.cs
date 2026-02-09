using Microsoft.AspNetCore.SignalR;
using Microsoft.Net.Http.Headers;

namespace portscanner_backend.Models.Dto
{
    public class DashboardPortStatusOverviewDto
    {
        public int Total { get; set; }
        public int Open { get; set; }
        public int Closed { get; set; }
    }

    public class BranchHealthOverviewDto
    {
        public int Branch_id { get; set; }
        public string Branch_name { get; set; }
        public string Branch_cidr { get; set; }
        public int TotalHost { get; set; }
        public int TotalPortOpen { get; set; }
        public int TotalPortClosed { get; set; }
    }




    public class BranchDetailDto
    {
        public string BranchName { get; set; } = string.Empty;
        public string BranchCidr { get; set; } = string.Empty;
        public int TotalHost { get; set; }
        public List<IpPortStatusDto> Results { get; set; } = new List<IpPortStatusDto>();
    }

    public class IpPortStatusDto
    {
        public string Ip { get; set; } = string.Empty;
        public bool HostStatus { get; set; }
        public List<PortStatusDto> Ports { get; set; } = new List<PortStatusDto>();
    }

    public class PortStatusDto
    {
        public int? Port { get; set; }
        public string Service { get; set; } = string.Empty;
        public bool? Status { get; set; } 
        public string Severity { get; set; }
    }

   
    public class BranchDetailRawDto
    {
        public string BranchName { get; set; }
        public string BranchCidr { get; set; }
        public string IpAddress { get; set; }
        public int PortNumber { get; set; }
        public string ServiceName { get; set; }
        public bool IsOpen { get; set; }
        public string Severity { get; set; }
        public bool HostStatus { get; set; }
    }

    

    public class RiskDistributionDto
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public int HighRisk { get; set; }
        public int MediumRisk { get; set; }
        public int LowRisk { get; set; }
    }

    public class GlobalTrendDto
    {
        public DateTime ScanDate { get; set; }
        public int? TotalOpenPorts { get; set; } 
    }


    
}