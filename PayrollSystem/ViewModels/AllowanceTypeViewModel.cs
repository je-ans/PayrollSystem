using System.ComponentModel.DataAnnotations;

namespace PayrollSystem.ViewModels
{
    public class AllowanceTypeViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Allowance Type Name is required")]
        [Display(Name = "Name")]
        [StringLength(100)]
        public string Name { get; set; }

        [Display(Name = "Code")]
        [StringLength(20)]
        public string? Code { get; set; }

        [Display(Name = "Description")]
        [StringLength(250)]
        public string? Description { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;
    }
}
