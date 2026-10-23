using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class PayrollEntryViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Payroll Run")]
        public int PayrollRunId { get; set; }

        [Display(Name = "Employee")]
        public Guid EmployeeId { get; set; }

        [Display(Name = "Employee Name")]
        public string EmployeeName { get; set; }

        [Display(Name = "Organization Unit")]
        public string OrganizationUnitName { get; set; }

        // Hours worked
        [Display(Name = "Basic Hours")]
        public decimal? RegularHours { get; set; }

        [Display(Name = "Overtime Hours")]
        public decimal? OvertimeHours { get; set; }

        [Display(Name = "Night Differential Hours")]
        public decimal? NightDifferentialHours { get; set; }

        [Display(Name = "Night Differential OT Hours")]
        public decimal? NightDifferentialOvertimeHours { get; set; }

        [Display(Name = "Holiday Hours")]
        public decimal? HolidayHours { get; set; }

        [Display(Name = "Rest Day Hours")]
        public decimal? RestDayHours { get; set; }

        // Rate information
        [Display(Name = "Hourly Rate")]
        public decimal? HourlyRate { get; set; }

        [Display(Name = "Overtime Rate")]
        public decimal? OvertimeRate { get; set; }

        [Display(Name = "Night Differential Rate")]
        public decimal? NightDifferentialRate { get; set; }

        // Pay calculations
        [Display(Name = "Regular Pay")]
        public decimal? RegularPay { get; set; }

        [Display(Name = "Overtime Pay")]
        public decimal? OvertimePay { get; set; }

        [Display(Name = "Night Differential Pay")]
        public decimal? NightDifferentialPay { get; set; }

        [Display(Name = "Holiday Pay")]
        public decimal? HolidayPay { get; set; }

        [Display(Name = "Rest Day Pay")]
        public decimal? RestDayPay { get; set; }

        [Display(Name = "Gross Pay")]
        public decimal? GrossPay { get; set; }

        [Display(Name = "Total Benefits")]
        public decimal? TotalBenefits { get; set; }

        [Display(Name = "Total Deductions")]
        public decimal? TotalDeductions { get; set; }

        [Display(Name = "Net Pay")]
        public decimal? NetPay { get; set; }

        [Display(Name = "Tax Withheld")]
        public decimal? TaxWithheld { get; set; }

        [Display(Name = "SSS Contribution")]
        public decimal? SSSContribution { get; set; }

        [Display(Name = "PhilHealth Contribution")]
        public decimal? PhilHealthContribution { get; set; }

        [Display(Name = "Pag-IBIG Contribution")]
        public decimal? PagIbigContribution { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Is Processed")]
        public bool IsProcessed { get; set; } = false;

        // Navigation properties
        public EmployeeViewModel? Employee { get; set; }
        public PayrollRunViewModel? PayrollRun { get; set; }
        public ICollection<DeductionViewModel>? Deductions { get; set; }
        public ICollection<BenefitViewModel>? Benefits { get; set; }

        // Computed properties
        public decimal TotalHours => (RegularHours ?? 0) + (OvertimeHours ?? 0) + (NightDifferentialHours ?? 0) +
                                   (NightDifferentialOvertimeHours ?? 0) + (HolidayHours ?? 0) + (RestDayHours ?? 0);
        public decimal TotalPay => (GrossPay ?? 0) + (TotalBenefits ?? 0) - (TotalDeductions ?? 0);
    }
}