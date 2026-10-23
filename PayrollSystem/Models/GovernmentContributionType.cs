using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class GovernmentContributionType : UserActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        [Display(Name = "Code")]
        public string Code { get; set; }

        [Required]
        [MaxLength(200)]
        [Display(Name = "Name")]
        public string Name { get; set; }

        [MaxLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Is Mandatory")]
        public bool IsMandatory { get; set; } = true;

        [Display(Name = "Has Employer Share")]
        public bool HasEmployerShare { get; set; } = true;

        [Required]
        [Display(Name = "Calculation Method")]
        public ContributionCalculationMethod CalculationMethod { get; set; }

        [Display(Name = "Display Order")]
        public int DisplayOrder { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        public virtual ICollection<GovernmentContributionTable> ContributionTables { get; set; }
            = new List<GovernmentContributionTable>();
    }
}