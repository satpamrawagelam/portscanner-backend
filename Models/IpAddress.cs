using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace portscanner_backend.Models
{
    public class IpAddress
    {
        [Key]
        public int Ip_id { get; set; }
        public int Ip_branchId { get; set; }
        public string Ip_address { get; set; } = string.Empty;

        [ForeignKey(nameof(Ip_branchId))]
        public Branch? Branch { get; set; }
        public ICollection<ScanResult>? ScanResults { get; set; }
    }
}
