namespace portscanner_backend.Models.Dto
{
    public class ScanRequestDto
    {
        public List<int> BranchIds { get; set; } = new List<int>();

        public int? Pg_id { get; set; }

        public List<int>? Manual_ports { get; set; }
        public string Title { get; set; }
    }

    public class IpScanResultDto
    {
        public string Ip { get; set; } = string.Empty;

        public bool IsHostAlive { get; set; }
        
        public List<PortScanResultDto> Ports { get; set; } = new List<PortScanResultDto>();
    }

    public class PortScanResultDto
    {
        public int Port { get; set; }
        public bool Status { get; set; }
        public string Severity { get; set; }
    }

    public class ScanHistoryDto
    {
        public DateTime ScanDate { get; set; }
        public string ScanTitle { get; set; }
        public string ScanType { get; set; }
        public string BranchName { get; set; }
        public string IpAddress { get; set; }
        public string OpenPorts { get; set; }
        public bool HostStatus { get; set; }
    }


    public class ScheduleRequestDto
    {
        public string Sch_title { get; set; }
        public string Sch_frequency { get; set; }
        public string Sch_time { get; set; }
        public string Sch_portMode { get; set; }
        public int? Sch_pgId { get; set; }
        public List<int>? Sch_targetManualPort { get; set; } 
        public List<int>? TargetBranchIds { get; set; }
    }

    public class ScheduleDetailDto
    {
        public int Sch_id { get; set; }
        public string Sch_title { get; set; }
        public string Sch_frequency { get; set; }
        public string Sch_time { get; set; }
        public string Sch_portMode { get; set; }
        public int? Sch_pgId { get; set; }
        public List<int>? Sch_targetManualPort { get; set; }
        public DateTime? Sch_nextRun { get; set; }
        public bool Sch_isActive { get; set; }

        public List<TargetBranchDto> Targets { get; set; } = new List<TargetBranchDto>();
    }

    public class TargetBranchDto
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public string BranchCidr { get; set; }
    }

    public class PortStatusChange
    {
        public string IpAddress { get; set; } = string.Empty;
        public int PortNumber { get; set; }
        public bool IsNowOpen { get; set; }
    }

    // ── PDF Report DTOs ──────────────────────────────────────────────────────

    public class ScanReportSummaryDto
    {
        public int TotalScan { get; set; }
        public int TotalOpenPortFindings { get; set; }
        public int TotalHostWithOpenPort { get; set; }
        public int HighSeverityPortCount { get; set; }
        public int MediumSeverityPortCount { get; set; }
    }

    public class TopBranchReportDto
    {
        public string BranchName { get; set; } = string.Empty;
        public int OpenPortCount { get; set; }
        public int RiskScore { get; set; }
    }

    public class TopHostReportDto
    {
        public string IpAddress { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public int OpenPortCount { get; set; }
        public int RiskScore { get; set; }
    }

    public class TopPortReportDto
    {
        public int PortNumber { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string PortDesc { get; set; } = string.Empty;
        public int OpenCount { get; set; }
    }

    public class ScanReportResponseDto
    {
        public string PeriodeStart { get; set; } = string.Empty;
        public string PeriodeEnd { get; set; } = string.Empty;
        public ScanReportSummaryDto Summary { get; set; } = new();
        public List<TopBranchReportDto> TopBranches { get; set; } = new();
        public List<TopHostReportDto> TopHosts { get; set; } = new();
        public List<TopPortReportDto> TopPorts { get; set; } = new();
        public List<ScanHistoryDto> DetailHistory { get; set; } = new();
    }
}