using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace portscanner_backend.Models
{
    public class Port
    {
        [Key]
        public int Port_id { get; set; }
        public int Port_branchId { get; set; }
        public int Port_number { get; set; }

        [ForeignKey(nameof(Port_branchId))]
        public Branch? Branch { get; set; }
        public ICollection<ScanResult>? ScanResults { get; set; }
    }
}
