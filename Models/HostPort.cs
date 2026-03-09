using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace portscanner_backend.Models
{
    public class HostPort
    {
        [Key]
        public int HostPort_id { get; set; }
        public int Ip_id { get; set; }
        public int Port_number { get; set; }
        public bool Status { get; set; }
        public DateTime Last_Updated { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(Ip_id))]
        public IpAddress? IpAddress { get; set; }
    }
}
