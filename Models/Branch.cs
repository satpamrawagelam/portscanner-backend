using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace portscanner_backend.Models
{
    public class Branch
    {
        [Key]
        public int Branch_id { get; set; }
        public string Branch_name { get; set; } = string.Empty;
        public string Branch_cidr { get; set; } = string.Empty;

        public ICollection<IpAddress>? IpAddresses { get; set; }
        public ICollection<ScanScheduleTarget>? ScanScheduleTargets { get; set; }
    }
}
