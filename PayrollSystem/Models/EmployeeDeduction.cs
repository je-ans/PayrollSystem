using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class EmployeeDeduction : UserActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Deduction")]
        public int DeductionId { get; set; }
        public virtual Deduction Deduction { get; set; } = null!;

        [Required]
        [Display(Name = "Employee")]
        public Guid EmployeeId { get; set; }
        public virtual Employee Employee { get; set; } = null!;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [NotMapped]
        public bool IsCurrent => IsActive && !IsDeleted &&
                                 Deduction != null && Deduction.IsCurrent;
    }
}
