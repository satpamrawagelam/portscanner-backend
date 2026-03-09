using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace portscanner_backend.Models
{
    public class ScanSession
    {
        [Key]
        public int Session_id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime ScanDate { get; set; } = DateTime.UtcNow;

        public ICollection<ScanHostResult>? ScanHostResults { get; set; }
    }
}
