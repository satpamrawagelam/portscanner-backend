namespace portscanner_backend.Models.Dto
{
    public class CheckPortByHostDto
    {
        public string IpAddress { get; set; } = string.Empty;
        public bool HostStatus { get; set; }
        public string Port_number { get; set; } = string.Empty;

    }

    public class CheckPortByBranchDto
    {
        public string BranchName { get; set; } = string.Empty;
        public string BranchCidr { get; set; } = string.Empty;
        public List<CheckPortByHostDto> Results { get; set; } = new List<CheckPortByHostDto>();
    }

    public class CheckPortByBranchRawDto{
        public string BranchName { get; set; } = string.Empty;
        public string BranchCidr { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public bool HostStatus { get; set; }
        public string Port_number { get; set; } = string.Empty;
    }

    public class SpesificPortStatusDto
    {
        public string IpAddress { get; set; } = string.Empty;
        public string PortNumber { get; set; } = string.Empty;
        public string PortStatus { get; set; } = string.Empty;
    }

    public class SpesificPortRequestDto
    {
        public string? Ip { get; set; } 
        public string? Ports { get; set; }
    }
}