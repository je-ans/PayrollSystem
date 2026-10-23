using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class EmployeeAllowance : UserActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Allowance")]
        public int AllowanceId { get; set; }
        public virtual Allowance Allowance { get; set; } = null!;

        [Required]
        [Display(Name = "Employee")]
        public Guid EmployeeId { get; set; }
        public virtual Employee Employee { get; set; } = null!;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [NotMapped]
        public bool IsCurrent => IsActive && !IsDeleted &&
                                 Allowance != null && Allowance.IsCurrent;

        [NotMapped]
        public decimal MonthlyAmount => IsCurrent ? Allowance?.MonthlyAmount ?? 0 : 0;
    }
}
