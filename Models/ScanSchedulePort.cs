using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace portscanner_backend.Models
{
    public class ScanSchedulePort
    {
        [Key]
        public int SchPort_id { get; set; }
        public int Sch_id { get; set; }
        public int Pm_id { get; set; }

        [ForeignKey(nameof(Sch_id))]
        public ScanSchedule? ScanSchedule { get; set; }

        [ForeignKey(nameof(Pm_id))]
        public PortMaster? PortMaster { get; set; }
    }
}
