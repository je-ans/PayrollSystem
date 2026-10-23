using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class Deduction : UserActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Display(Name = "Deduction Name")]
        public string DeductionName { get; set; } = string.Empty;

        [MaxLength(10)]
        [Display(Name = "Code")]
        public string? Code { get; set; }

        [Required]
        [Display(Name = "Deduction Type")]
        public int DeductionTypeId { get; set; }
        public virtual DeductionType DeductionType { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Effective Start Date")]
        public DateOnly EffectiveStartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Effective End Date")]
        public DateOnly? EffectiveEndDate { get; set; }

        [MaxLength(500)]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        public virtual ICollection<EmployeeDeduction> EmployeeAssignments { get; set; } = new List<EmployeeDeduction>();

        [NotMapped]
        public bool IsCurrent
        {
            get
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                return IsActive && EffectiveStartDate <= today &&
                       (!EffectiveEndDate.HasValue || EffectiveEndDate.Value >= today);
            }
        }
    }
}
