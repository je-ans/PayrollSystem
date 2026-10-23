using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PayrollSystem.Models;
using PayrollSystem.Models.BuildUp;

namespace PayrollSystem.Models
{
    public class Employee : UserActivity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Foreign key to ApplicationUser (for login/authentication)
        public string ApplicationUserId { get; set; }
        public virtual ApplicationUser ApplicationUser { get; set; }

        [Display(Name = "Default Rate Type")]
        public int? DefaultRateTypeId { get; set; }
        public virtual RateType? DefaultRateType { get; set; }

        [NotMapped] // Not mapped to database column
        public string FullName => $"{FirstName} {MiddleName} {Surname}".Trim();

        [Display(Name = "Employee ID")]
        public string EmployeeIdNumber { get; set; }

        [Display(Name = "Surname")]
        public string Surname { get; set; }

        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Display(Name = "Middle Name")]
        public string? MiddleName { get; set; }

        [Display(Name = "Name Extension (Jr., Sr.)")]
        public string? NameExtension { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date of Birth")]
        public DateOnly? DateOfBirth { get; set; }

        [Display(Name = "Sex")]
        public string Sex { get; set; } // e.g., "Male", "Female"

        [Display(Name = "Telephone No.")]
        public string? TelephoneNo { get; set; }

        [Display(Name = "Mobile No.")]
        public string? MobileNo { get; set; }

        [EmailAddress]
        [Display(Name = "Email Address")]
        public string? EmailAddress { get; set; }

        [Required]
        [Display(Name = "Employment Basis")]
        public EmploymentBasis EmploymentBasis { get; set; }

        [MaxLength(20)]
        [Display(Name = "Biometric ID")]
        public string? BiometricId { get; set; } // 3, 4, 5 (from biometric device)

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date Hired")]
        public DateOnly? DateHired { get; set; }

        // NAVIGATION ONLY (no direct org dependency)
        public virtual ICollection<EmployeeOrganizationAssignment> OrganizationAssignments { get; set; } =
            new List<EmployeeOrganizationAssignment>();

        public virtual ICollection<EmployeeDesignation> Designations { get; set; } =
            new List<EmployeeDesignation>();

        public virtual ICollection<EmployeeRoleAssignment> RoleAssignments { get; set; } =
            new List<EmployeeRoleAssignment>();

        // Navigation properties to PDS sections
        public virtual PersonalInformation? PersonalInformation { get; set; }

        // Compensation Details (derived from DefaultRateType; supports Monthly, Daily, and Hourly basis)
        [NotMapped]
        [Display(Name = "Basic Salary")]
        public decimal BasicSalary =>
            DefaultRateType == null ? 0
            : DefaultRateType.RateBasis == RateBasis.Monthly && DefaultRateType.MonthlySalary.HasValue ? DefaultRateType.MonthlySalary.Value
            : DefaultRateType.RateBasis == RateBasis.Monthly && DefaultRateType.DailyRate.HasValue ? DefaultRateType.DailyRate.Value * 22M
            : DefaultRateType.RateBasis == RateBasis.Daily && DefaultRateType.DailyRate.HasValue ? DefaultRateType.DailyRate.Value * 22M
            : DefaultRateType.RateBasis == RateBasis.Hourly && DefaultRateType.HourlyRate.HasValue ? DefaultRateType.HourlyRate.Value * 8M * 22M
            : 0;

        [NotMapped]
        [Display(Name = "Monthly Rate")]
        public decimal MonthlyRate => BasicSalary / 2.0M; // Semi-monthly payout: one period = half of monthly

        [NotMapped]
        [Display(Name = "Daily Rate")]
        public decimal DailyRate =>
            DefaultRateType == null ? 0
            : DefaultRateType.RateBasis == RateBasis.Monthly ? (MonthlyRate * 2M) / 22M
            : DefaultRateType.RateBasis == RateBasis.Daily && DefaultRateType.DailyRate.HasValue ? DefaultRateType.DailyRate.Value
            : DefaultRateType.RateBasis == RateBasis.Hourly && DefaultRateType.HourlyRate.HasValue ? DefaultRateType.HourlyRate.Value * 8M
            : (MonthlyRate * 2M) / 22M;

        [NotMapped]
        [Display(Name = "Hourly Rate")]
        public decimal HourlyRate =>
            DefaultRateType == null ? 0
            : DefaultRateType.RateBasis == RateBasis.Hourly && DefaultRateType.HourlyRate.HasValue ? DefaultRateType.HourlyRate.Value
            : DailyRate / 8.0M;


        public virtual ICollection<EmployeeAllowance> Allowances { get; set; } = new List<EmployeeAllowance>();

        // Computed properties
        [NotMapped]
        public bool IsCurrentActiveCompensation => IsActive && DefaultRateType != null && DefaultRateType.IsActive;

        [NotMapped]
        public decimal TotalMonthlyCompensation => BasicSalary +
            (Allowances?.Where(a => a.IsActive && a.Allowance != null &&
                                    a.Allowance.IsActive &&
                                    a.Allowance.Frequency == AllowanceFrequency.Monthly)
                      .Sum(a => a.Allowance!.Amount) ?? 0);

        public bool IsActive { get; set; } = true;

        public virtual ICollection<PayrollPeriodEmployee> PayrollPeriodEmployees { get; set; } = new List<PayrollPeriodEmployee>();

        [Display(Name = "Gov't Contribution Rule")]
        public ContributionApplicationRule GovContributionRule { get; set; } = ContributionApplicationRule.EveryPayroll;

        // Employee loans
        public virtual ICollection<EmployeeLoan> EmployeeLoans { get; set; } = new List<EmployeeLoan>();

        // Employee deductions (non-loan recurring deductions)
        public virtual ICollection<EmployeeDeduction> EmployeeDeductions { get; set; } = new List<EmployeeDeduction>();
    }
}
