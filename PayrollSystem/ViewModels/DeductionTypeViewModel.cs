using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class DeductionTypeViewModel
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        [Display(Name = "Code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Is System Type")]
        public bool IsSystem { get; set; }

        [Display(Name = "Display Order")]
        public int DisplayOrder { get; set; }
    }
}

