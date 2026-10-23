using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class Allowance : UserActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Display(Name = "Allowance Name")]
        public string AllowanceName { get; set; } = string.Empty;

        [MaxLength(10)]
        [Display(Name = "Code")]
        public string? Code { get; set; }

        [Required]
        [Display(Name = "Allowance Type")]
        public int AllowanceTypeId { get; set; }
        public virtual AllowanceType AllowanceType { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Frequency")]
        public AllowanceFrequency Frequency { get; set; }

        [Display(Name = "Taxable")]
        public bool IsTaxable { get; set; } = true;

        [Display(Name = "Mandatory")]
        public bool IsMandatory { get; set; }

        [Display(Name = "Effective Date")]
        public DateTime EffectiveDate { get; set; } = DateTime.Now;

        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }

        [MaxLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        public virtual ICollection<EmployeeAllowance> EmployeeAssignments { get; set; } = new List<EmployeeAllowance>();

        [NotMapped]
        public bool IsCurrent => IsActive && EffectiveDate <= DateTime.Now &&
                                 (!EndDate.HasValue || EndDate.Value >= DateTime.Now);

        [NotMapped]
        public decimal MonthlyAmount => Frequency switch
        {
            AllowanceFrequency.Monthly => Amount,
            AllowanceFrequency.Daily => Amount * 30,
            AllowanceFrequency.Hourly => Amount * 160,
            AllowanceFrequency.Yearly => Amount / 12,
            _ => Amount
        };
    }
}
