using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class PayrollPeriodTimeCardDetail
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TimeCardId { get; set; }
        public PayrollPeriodTimeCardEntry TimeCard { get; set; }

        [Required]
        public int DayTypeId { get; set; }
        public DayType DayType { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal RegularHours { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal OvertimeHours { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal NightDifferentialHours { get; set; }

        [Column(TypeName = "decimal(18, 4)")]
        public decimal NightDifferentialOvertimeHours { get; set; }
    }
}
