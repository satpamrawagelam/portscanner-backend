using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace portscanner_backend.Models
{
    public class PortGroup
    {
        [Key]
        public int Pg_id { get; set; }
        public string Pg_name { get; set; } = string.Empty;

        public ICollection<PortMaster>? PortMasters { get; set; }
        public ICollection<ScanSchedule>? ScanSchedules { get; set; }
    }
}
