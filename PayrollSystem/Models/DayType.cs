using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class DayType : UserActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [StringLength(250)]
        public string? Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 4)")]
        public decimal BasicMultiplier { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 4)")]
        public decimal OvertimeMultiplier { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 4)")]
        public decimal NightDifferentialMultiplier { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 4)")]
        public decimal NightDifferentialOvertimeMultiplier { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime EffectiveFrom { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EffectiveTo { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
