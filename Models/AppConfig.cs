using System.ComponentModel.DataAnnotations;

namespace portscanner_backend.Models
{
    public class AppConfig
    {
        [Key]
        public int Id { get; set; } 
        public int PingTimeout { get; set; } = 200;
        public int PingRetries { get; set; } = 1;
        public int PortScanTimeout { get; set; } = 1000;
        public int MaxConcurrency { get; set; } = 50;
    }
}