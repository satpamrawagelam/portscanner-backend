using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace portscanner_backend.Models
{
    public class PortMaster
    {
        [Key]
        public int Pm_id { get; set; }
        public int Pm_port_number { get; set; }
        public string? Pm_port_desc { get; set; }
        public string Pm_severity { get; set; } = "Low";
        public int? Pg_id { get; set; }

        [ForeignKey(nameof(Pg_id))]
        public PortGroup? PortGroup { get; set; }

        public ICollection<ScanSchedulePort>? ScanSchedulePorts { get; set; }
    }
}
