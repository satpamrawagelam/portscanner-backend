using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace portscanner_backend.Models
{
    public class ScanScheduleTarget
    {
        [Key]
        public int Tgt_id { get; set; }
        public int Tgt_schId { get; set; }
        public int Tgt_branchId { get; set; }

        [ForeignKey(nameof(Tgt_schId))]
        public ScanSchedule? ScanSchedule { get; set; }

        [ForeignKey(nameof(Tgt_branchId))]
        public Branch? Branch { get; set; }
    }
}