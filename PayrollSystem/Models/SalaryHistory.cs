using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class SalaryHistory : UserActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public Guid EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        [Required]
        [Display(Name = "Change Type")]
        public SalaryChangeType ChangeType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Previous Basic Salary")]
        public decimal PreviousBasicSalary { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "New Basic Salary")]
        public decimal NewBasicSalary { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Salary Difference")]
        public decimal SalaryDifference { get; set; } = 0;

        [Display(Name = "Effective Date")]
        public DateTime EffectiveDate { get; set; }

        [Required]
        [MaxLength(500)]
        [Display(Name = "Reason")]
        public string Reason { get; set; }

        [MaxLength(100)]
        [Display(Name = "Reference Number")]
        public string? ReferenceNumber { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Approved By")]
        public string? ApprovedById { get; set; }
        public virtual ApplicationUser? ApprovedBy { get; set; }

        [Display(Name = "Approval Date")]
        public DateTime? ApprovalDate { get; set; }

        [NotMapped]
        public string ChangeDescription => $"{Utility.EnumHelper.GetDisplayName(ChangeType)}: ₱{PreviousBasicSalary:N2} → ₱{NewBasicSalary:N2}";

        [NotMapped]
        public decimal PercentageChange => PreviousBasicSalary > 0 ?
            ((NewBasicSalary - PreviousBasicSalary) / PreviousBasicSalary) * 100 : 0;
    }
}