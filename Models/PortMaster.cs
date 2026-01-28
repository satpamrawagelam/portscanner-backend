using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace portscanner_backend.Models
{
    public class PortMaster
    {
        [Key]
        public int Pm_id { get; set; }
        public int Pm_portGroup { get; set; }
        public int Pm_portNumber { get; set; }
        public string Pm_desc { get; set; } = string.Empty;
        public string Pm_severity { get; set; } = "Low";

        [ForeignKey(nameof(Pm_portGroup))]
        public PortGroup? PortGroup { get; set; }
    }
}
