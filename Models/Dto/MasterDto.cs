namespace portscanner_backend.Models.Dto
{
    // Master Port
    public class PortMasterDto 
    { 
        public int Pg_id { get; set; }
        public int Pm_port_number { get; set; }
        public string? Pm_port_desc { get; set; }
        public string Pm_severity { get; set; } = "Low";
    }

    // Port Group 
    public class PortGroupDto { public string Pg_name { get; set; } }

    public class PortGroupResult 
    {
        public int Pg_id { get; set; }
        public string Pg_name { get; set; }
        public int TotalPorts { get; set; }
    }

    public class BranchDto
    {
        public string Branch_name { get; set; }
        public string Branch_cidr { get; set; }
    }
}
    