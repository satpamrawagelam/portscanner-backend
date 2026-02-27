using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace portscanner_backend.Models
{
    public class ScanSchedule
    {
        [Key]
        public int Sch_id { get; set; }
        public string Sch_title { get; set; } = string.Empty;
        public string Sch_frequency { get; set; } = "Daily";
        public TimeSpan Sch_time { get; set; }
        public string? Sch_days { get; set; }
        public string Sch_portMode { get; set; } = "group";
        public int? Sch_targetPortGroupId { get; set; }
        public string? Sch_targetManualPorts { get; set; }
        public DateTime? Sch_lastRun { get; set; }
        public DateTime? Sch_nextRun { get; set; }
        public bool Sch_isActive { get; set; } = true;
        public DateTime Sch_createdDate { get; set; } = DateTime.Now;

        [NotMapped]
        public List<int> TargetBranchIds { get; set; } = new List<int>();
    }
    
}