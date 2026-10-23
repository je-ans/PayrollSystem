using System.ComponentModel.DataAnnotations;

namespace PayrollSystem.ViewModels
{
    public class DayTypeViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Day Type Name is required")]
        [StringLength(50)]
        [Display(Name = "Day Type Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Code is required")]
        [StringLength(20)]
        [Display(Name = "Code")]
        public string Code { get; set; } = string.Empty;

        [StringLength(250)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Basic Multiplier is required")]
        [Display(Name = "Basic Multiplier")]
        public decimal BasicMultiplier { get; set; }

        [Required(ErrorMessage = "Overtime Multiplier is required")]
        [Display(Name = "Overtime Multiplier")]
        public decimal OvertimeMultiplier { get; set; }

        [Required(ErrorMessage = "Night Differential Multiplier is required")]
        [Display(Name = "Night Differential Multiplier")]
        public decimal NightDifferentialMultiplier { get; set; }

        [Required(ErrorMessage = "Night Differential Overtime Multiplier is required")]
        [Display(Name = "Night Differential Overtime Multiplier")]
        public decimal NightDifferentialOvertimeMultiplier { get; set; }

        [Required(ErrorMessage = "Effective From is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Effective From")]
        public DateTime EffectiveFrom { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "Effective To")]
        public DateTime? EffectiveTo { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;
    }
}
