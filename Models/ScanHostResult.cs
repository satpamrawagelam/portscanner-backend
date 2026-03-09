using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace portscanner_backend.Models
{
    public class ScanHostResult
    {
        [Key]
        public int HostRes_id { get; set; }
        public int Session_id { get; set; }
        public int Ip_id { get; set; }
        public bool IsAlive { get; set; }

        [ForeignKey(nameof(Session_id))]
        public ScanSession? ScanSession { get; set; }

        [ForeignKey(nameof(Ip_id))]
        public IpAddress? IpAddress { get; set; }

        public ICollection<ScanPortResult>? ScanPortResults { get; set; }
    }
}
