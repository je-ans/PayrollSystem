using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PayrollSystem.ViewModels
{
    // Metadata for a day-type column in the payroll report
    public class DayTypeColumnInfo
    {
        public int DayTypeId { get; set; }
        public string DayTypeName { get; set; } = string.Empty;
        // If true, show only OT Hours and OT Amount (regular is in Hours Worked / Total Reg. Wage)
        public bool IsRegularDay { get; set; }
    }

    // <summary>Per-day-type hours and amounts for one employee row
    public class DayTypeRowBreakdown
    {
        public decimal Hours { get; set; }
        public decimal Amount { get; set; }
        public decimal OvertimeHours { get; set; }
        public decimal OvertimeAmount { get; set; }
    }

    public class PayrollSummaryRowViewModel
    {
        public int PayrollEntryId { get; set; }

        [Display(Name = "Emp. ID No.")]
        public string EmployeeIdNumber { get; set; }

        [Display(Name = "Full Name")]
        public string EmployeeName { get; set; }

        [Display(Name = "Status")]
        public string EmploymentStatus { get; set; }

        [Display(Name = "Position")]
        public string Position { get; set; }

        [Display(Name = "Organization Unit")]
        public string OrganizationUnit { get; set; }

        [Display(Name = "Date From")]
        public DateOnly DateFrom { get; set; }

        [Display(Name = "Date To")]
        public DateOnly DateTo { get; set; }

        [Display(Name = "Semi-Monthly")]
        public decimal SemiMonthlyRate { get; set; }

        [Display(Name = "Rate (Monthly)")]
        public decimal MonthlyRate { get; set; }

        [Display(Name = "Rate (Daily)")]
        public decimal DailyRate { get; set; }

        [Display(Name = "Rate (Hourly)")]
        public decimal HourlyRate { get; set; }

        [Display(Name = "Hours Worked")]
        public decimal TotalHours { get; set; }

        // Regular hours only (excludes holiday/rest). Use for "Hours Worked" column when showing regular-only
        public decimal RegularHours { get; set; }

        [Display(Name = "Total Reg. Wage")]
        public decimal RegularPay { get; set; }

        [Display(Name = "Gross Salaries")]
        public decimal GrossPay { get; set; }

        [Display(Name = "Allowances / Benefits")]
        public decimal TotalBenefits { get; set; }

        [Display(Name = "Gross Pay (with Benefits)")]
        public decimal GrossWithBenefits => GrossPay + TotalBenefits;

        // Per-day-type breakdown (key = DayTypeId) for dynamic columns
        public Dictionary<int, DayTypeRowBreakdown> DayTypeBreakdown { get; set; } = new();

        // Dynamic breakdowns by name/description
        public Dictionary<string, decimal> AllowanceBreakdown { get; set; } = new();
        public Dictionary<string, decimal> DeductionBreakdown { get; set; } = new();

        [Display(Name = "Late/Undertime (min)")]
        public int LateUndertimeMinutes { get; set; }

        [Display(Name = "Total Deductions")]
        public decimal TotalDeductions { get; set; }

        [Display(Name = "Net Pay")]
        public decimal NetPay { get; set; }

        public Dictionary<string, decimal> GovtContributionBreakdown { get; set; } = new();
    }

    public class PayrollSummaryViewModel
    {
        public int PayrollRunId { get; set; }
        public string PayrollRunName { get; set; }
        public string PayrollPeriodName { get; set; }
        public DateOnly PeriodStartDate { get; set; }
        public DateOnly PeriodEndDate { get; set; }

        // ========== COMPANY PROFILE (from PayrollConfiguration) ==========
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyShortName { get; set; }
        public string? CompanyLogoPath { get; set; }
        public string? CompanyAddress { get; set; }

        public string? CompanyContactNumber { get; set; }
        public string? CompanyEmail { get; set; }
        public string? CompanyWebsite { get; set; }

        // ========== COLUMNS ==========
        public IList<DayTypeColumnInfo> DayTypeColumns { get; set; } = new List<DayTypeColumnInfo>();

        public IList<string> AllowanceColumns { get; set; } = new List<string>();
        public IList<string> DeductionColumns { get; set; } = new List<string>();
        public List<string> GovtContribColumns { get; set; } = new();

        public IList<PayrollSummaryRowViewModel> Rows { get; set; } = new List<PayrollSummaryRowViewModel>();

        //When set, the document is filtered to this employee only
        public Guid? FilterEmployeeId { get; set; }

        // All employees in this run (for filter dropdown)
        public IList<(Guid Id, string Name)> AllEmployees { get; set; } = new List<(Guid, string)>();
    }
}