using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace portscanner_backend.Models
{
    public class ScanPortResult
    {
        [Key]
        public int PortRes_id { get; set; }
        public int HostRes_id { get; set; }
        public int Port_number { get; set; }

        [ForeignKey(nameof(HostRes_id))]
        public ScanHostResult? ScanHostResult { get; set; }
    }
}
