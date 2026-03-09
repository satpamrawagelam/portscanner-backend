using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace portscanner_backend.Models
{
    public class IpAddress
    {
        [Key]
        public int Ip_id { get; set; }
        public int Ip_branchId { get; set; }
        public string Ip_address { get; set; } = string.Empty;
        public bool? Ip_isAlive { get; set; }
        public DateTime? Ip_lastScanned { get; set; }

        [ForeignKey(nameof(Ip_branchId))]
        public Branch? Branch { get; set; }

        public ICollection<HostPort>? HostPorts { get; set; }
        public ICollection<ScanHostResult>? ScanHostResults { get; set; }
    }
}
