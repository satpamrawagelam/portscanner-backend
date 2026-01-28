using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace portscanner_backend.Models
{
    public class ScanResult
    {
        [Key]
        public int Res_id { get; set; }
        public int Res_ipAddressId { get; set; }
        public int Res_portId { get; set; }
        public bool Res_status { get; set; }
        public DateTime Res_scanDate { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(Res_ipAddressId))]  
        public IpAddress? IpAddress { get; set; }
        [ForeignKey(nameof(Res_portId))]  
        public Port? Port { get; set; }
    }
}
