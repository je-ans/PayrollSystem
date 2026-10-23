using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class GovernmentContributionTypeViewModel
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Display(Name = "Code")]
        public string Code { get; set; } // "SSS", "PHILHEALTH", "PAGIBIG"

        [Required]
        [MaxLength(200)]
        [Display(Name = "Name")]
        public string Name { get; set; } // "Social Security System", "PhilHealth", "Pag-IBIG"

        [MaxLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Is Mandatory")]
        public bool IsMandatory { get; set; } = true;

        [Display(Name = "Has Employer Share")]
        public bool HasEmployerShare { get; set; } = true;

        [Display(Name = "Calculation Method")]
        public ContributionCalculationMethod CalculationMethod { get; set; } // Fixed, Percentage, Tiered

        [Display(Name = "Display Order")]
        public int DisplayOrder { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;
    }
}
