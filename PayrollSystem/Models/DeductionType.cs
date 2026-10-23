using System.ComponentModel.DataAnnotations;

namespace PayrollSystem.Models
{
    public class DeductionType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        [Display(Name = "Code")]
        public string Code { get; set; } = default!; // e.g. INCOME_TAX, ABSENCE, LOAN

        [Required]
        [MaxLength(100)]
        [Display(Name = "Name")]
        public string Name { get; set; } = default!;

        [MaxLength(200)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Is System Type")]
        public bool IsSystem { get; set; } = true;

        [Display(Name = "Display Order")]
        public int DisplayOrder { get; set; } = 0;
    }
}

