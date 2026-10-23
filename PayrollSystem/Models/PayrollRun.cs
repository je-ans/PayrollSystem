using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class PayrollRun : UserActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Payroll Period")]
        public int PayrollPeriodId { get; set; }
        public virtual PayrollPeriod PayrollPeriod { get; set; }

        [Required]
        [MaxLength(200)]
        [Display(Name = "Run Name")]
        public string Name { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Status")]
        public PayrollStatus Status { get; set; } = PayrollStatus.Draft;

        [Display(Name = "Processed Date")]
        public DateTime? ProcessedDate { get; set; }

        [Display(Name = "Processed By")]
        public string? ProcessedByUserId { get; set; }
        public virtual ApplicationUser? ProcessedByUser { get; set; }

        [Display(Name = "Total Employees")]
        public int TotalEmployees { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Gross Pay")]
        public decimal TotalGrossPay { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Deductions")]
        public decimal TotalDeductions { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Net Pay")]
        public decimal TotalNetPay { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Benefits")]
        public decimal TotalBenefits { get; set; } = 0;

        [MaxLength(2000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Is Finalized")]
        public bool IsFinalized { get; set; } = false;

        public virtual ICollection<PayrollEntry> PayrollEntries { get; set; } = new List<PayrollEntry>();

        // Computed properties
        [NotMapped]
        public decimal TotalPayrollCost => TotalGrossPay + TotalBenefits;
    }
}