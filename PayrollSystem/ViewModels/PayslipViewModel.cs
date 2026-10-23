using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class PayslipViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Payroll Entry")]
        public int PayrollEntryId { get; set; }

        [Display(Name = "Payslip Number")]
        public string PayslipNumber { get; set; }

        [Display(Name = "Generated Date")]
        public DateTime? GeneratedDate { get; set; }

        [Display(Name = "Sent Date")]
        public DateTime? SentDate { get; set; }

        [Display(Name = "Printed Date")]
        public DateTime? PrintedDate { get; set; }

        [Display(Name = "File Path")]
        public string? FilePath { get; set; }

        [Display(Name = "Email Address")]
        public string? EmailAddress { get; set; }

        [Display(Name = "Is Sent")]
        public bool IsSent { get; set; } = false;

        [Display(Name = "Is Printed")]
        public bool IsPrinted { get; set; } = false;

        [Display(Name = "Send Attempts")]
        public int SendAttempts { get; set; } = 0;

        [Display(Name = "Send Error Message")]
        public string? SendErrorMessage { get; set; }

        // Employee snapshot
        [Display(Name = "Employee Name")]
        public string EmployeeName { get; set; }

        [Display(Name = "Employee ID")]
        public string EmployeeIdNumber { get; set; }

        [Display(Name = "Organization Unit")]
        public string OrganizationUnitName { get; set; }

        [Display(Name = "Designation")]
        public string DesignationName { get; set; }

        [Display(Name = "Rate Type")]
        public string RateTypeName { get; set; }

        // Payroll period snapshot
        [Display(Name = "Period Start")]
        public DateOnly? PeriodStartDate { get; set; }

        [Display(Name = "Period End")]
        public DateOnly? PeriodEndDate { get; set; }

        [Display(Name = "Pay Date")]
        public DateOnly? PayDate { get; set; }

        [Display(Name = "Payroll Period Name")]
        public string PayrollPeriodName { get; set; }

        // Compensation information
        [Display(Name = "Rate")]
        public decimal? Rate { get; set; }

        [Display(Name = "Rate Basis")]
        public string? RateBasis { get; set; }

        [Display(Name = "Days on Duty")]
        public decimal? DaysOnDuty { get; set; }

        // Earnings breakdown
        [Display(Name = "Basic Salary / Regular Pay")]
        public decimal? RegularPay { get; set; }

        [Display(Name = "Overtime Pay")]
        public decimal? OvertimePay { get; set; }

        [Display(Name = "Holiday Pay")]
        public decimal? HolidayPay { get; set; }

        [Display(Name = "Rest Day Pay")]
        public decimal? RestDayPay { get; set; }

        [Display(Name = "Night Differential Pay")]
        public decimal? NightDifferentialPay { get; set; }

        // Day types used (from time card) for dynamic compensation rows
        public IList<DayTypeColumnInfo> DayTypeColumns { get; set; } = new List<DayTypeColumnInfo>();

        // Per-day-type breakdown for this employee's payslip
        public Dictionary<int, DayTypeRowBreakdown> DayTypeBreakdown { get; set; } = new();

        // Financial summary snapshot
        [Display(Name = "Gross Pay")]
        public decimal? GrossPay { get; set; }

        [Display(Name = "Total Benefits")]
        public decimal? TotalBenefits { get; set; }

        [Display(Name = "Total Deductions")]
        public decimal? TotalDeductions { get; set; }

        [Display(Name = "Net Pay")]
        public decimal? NetPay { get; set; }

        // Company information
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; }

        [Display(Name = "Company Address")]
        public string CompanyAddress { get; set; }

        [Display(Name = "Company Phone")]
        public string CompanyPhone { get; set; }

        [Display(Name = "Company Email")]
        public string CompanyEmail { get; set; }

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        public string? CompanyShortName { get; set; }

        public string? CompanyLogoPath { get; set; }

        public string? CompanyWebsite { get; set; }

        // Navigation properties
        public PayrollEntryViewModel? PayrollEntry { get; set; }
        public ICollection<BenefitViewModel>? Benefits { get; set; }

        // Standard deductions only: absence, late/undertime, withholding tax.
        // Government contributions are stored separately in GovernmentContributions below.
        public ICollection<DeductionViewModel>? Deductions { get; set; }

        // Government contributions (SSS, PhilHealth, Pag-IBIG, etc.) � employee share only.
        // Populated from PayrollEntryGovernmentContribution, keyed by ContributionName.
        public ICollection<GovtContributionViewModel>? GovernmentContributions { get; set; }

        // Computed properties
        public string PeriodRange => $"{PeriodStartDate:MMM dd, yyyy} - {PeriodEndDate:MMM dd, yyyy}";

        public string Status
        {
            get
            {
                if (IsSent && IsPrinted) return "Sent & Printed";
                if (IsSent) return "Sent";
                if (IsPrinted) return "Printed";
                return "Generated";
            }
        }

        public bool CanSend => !IsSent && !string.IsNullOrEmpty(EmailAddress);
        public bool CanPrint => !IsPrinted;

        public decimal? TotalEarnings => GrossPay + TotalBenefits;

        // Total government contribution employee shares (SSS + PhilHealth + Pag-IBIG + �).
        // Computed from the GovernmentContributions collection.
        public decimal TotalGovernmentContributions =>
            GovernmentContributions?.Sum(gc => gc.EmployeeShare) ?? 0M;

        // Grand total of ALL deductions shown on the payslip:
        // standard deductions (tax + absence + late/undertime) + govt contributions.
        public decimal TotalAllDeductions =>
            (TotalDeductions ?? 0M) + TotalGovernmentContributions;
    }

    public class GovtContributionViewModel
    {
        //Snapshot name from PayrollEntryGovernmentContribution.ContributionName
        public string ContributionName { get; set; } = string.Empty;

        public decimal EmployeeShare { get; set; }
        public decimal EmployerShare { get; set; }

        public decimal SalaryBasis { get; set; }
    }
}