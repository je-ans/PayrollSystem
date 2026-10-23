using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class PayrollRunViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Payroll Period")]
        public int PayrollPeriodId { get; set; }

        [Required]
        [MaxLength(200)]
        [Display(Name = "Run Name")]
        public string Name { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Status")]
        public PayrollStatus Status { get; set; } = PayrollStatus.Draft;

        [Display(Name = "Processed Date")]
        public DateTime? ProcessedDate { get; set; }

        [Display(Name = "Processed By")]
        public string? ProcessedByUserName { get; set; }

        [Display(Name = "Total Employees")]
        public int TotalEmployees { get; set; } = 0;

        [Display(Name = "Total Gross Pay")]
        public decimal? TotalGrossPay { get; set; }

        [Display(Name = "Total Deductions")]
        public decimal? TotalDeductions { get; set; }

        [Display(Name = "Total Net Pay")]
        public decimal? TotalNetPay { get; set; }

        [Display(Name = "Total Benefits")]
        public decimal? TotalBenefits { get; set; }

        [Display(Name = "Payroll Period Name")]
        public string? PayrollPeriodName { get; set; }

        [MaxLength(2000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Is Finalized")]
        public bool IsFinalized { get; set; } = false;

        // Navigation properties
        public PayrollPeriodViewModel? PayrollPeriod { get; set; }
        public ICollection<PayrollEntryViewModel>? PayrollEntries { get; set; }

        // For dropdowns
        public IEnumerable<PayrollPeriod>? AvailablePeriods { get; set; }

        // Computed properties
        public decimal TotalPayrollCost => (TotalGrossPay ?? 0) + (TotalBenefits ?? 0);
        public bool CanProcess => Status == PayrollStatus.Draft;
        public bool CanFinalize => Status == PayrollStatus.Completed && !IsFinalized;
        public bool CanEdit => Status == PayrollStatus.Draft;
    }
}