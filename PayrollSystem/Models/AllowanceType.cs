using System.ComponentModel.DataAnnotations;

namespace PayrollSystem.Models
{
    public class AllowanceType : UserActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Name")]
        public string Name { get; set; }

        [StringLength(20)]
        [Display(Name = "Code")]
        public string? Code { get; set; }

        [StringLength(250)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        public ICollection<Allowance>? Allowances { get; set; }
    }
}
