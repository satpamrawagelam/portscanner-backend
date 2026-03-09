using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace portscanner_backend.Models
{
    public class ScanSchedule
    {
        [Key]
        public int Sch_id { get; set; }
        public string Sch_title { get; set; } = string.Empty;
        public string Sch_frequency { get; set; } = string.Empty;
        public TimeSpan Sch_time { get; set; }
        public string? Sch_days { get; set; }
        public DateTime? Sch_lastRun { get; set; }
        public DateTime? Sch_nextRun { get; set; }
        public bool Sch_isActive { get; set; } = true;
        public DateTime Sch_createdDate { get; set; } = DateTime.UtcNow;
        public string Sch_portMode { get; set; } = "Custom";
        public int? Sch_pgId { get; set; }
        public string? Sch_customPorts { get; set; }

        [ForeignKey(nameof(Sch_pgId))]
        public PortGroup? PortGroup { get; set; }

        public ICollection<ScanScheduleTarget>? ScanScheduleTargets { get; set; }
        public ICollection<ScanSchedulePort>? ScanSchedulePorts { get; set; }

        [NotMapped]
        public List<int> TargetBranchIds { get; set; } = new List<int>();
    }
}