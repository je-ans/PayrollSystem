using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.ViewModels
{
    public class EmployeeDetailsViewModel
    {
        public Guid Id { get; set; }

        [Display(Name = "Employee ID")]
        public string? EmployeeIdNumber { get; set; }

        [Display(Name = "Employee Name")]
        public string? FullName { get; set; }

        [EmailAddress]
        [Display(Name = "Email Address")]
        public string? EmailAddress { get; set; }

        [Required]
        [Display(Name = "Default Rate Type")]
        public int? DefaultRateTypeId { get; set; }

        [Display(Name = "Default Rate Type Name")]
        public string? DefaultRateTypeName { get; set; }

        [Display(Name = "Basic Salary")]
        public decimal BasicSalary { get; set; }

        [Display(Name = "Monthly Rate")]
        public decimal MonthlyRate { get; set; }

        [Display(Name = "Daily Rate")]
        public decimal DailyRate { get; set; }

        [Display(Name = "Hourly Rate")]
        public decimal HourlyRate { get; set; }


        [Display(Name = "Payment Method")]
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;

        [MaxLength(100)]
        [Display(Name = "Payroll Code")]
        public string? PayrollCode { get; set; }

        [MaxLength(100)]
        [Display(Name = "Cost Center")]
        public string? CostCenter { get; set; }

        [MaxLength(500)]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        public List<EmployeeAllowanceViewModel> Allowances { get; set; } = new();

        public bool IsCurrentActiveCompensation { get; set; }

        public decimal TotalMonthlyCompensation { get; set; }

        public string PaymentMethodDisplay => Utility.EnumHelper.GetDisplayName(PaymentMethod);
        public string StatusDisplay => IsActive ? "Active" : "Inactive";
    }

    public class EmployeeAllowanceViewModel
    {
        public int Id { get; set; }

        public Guid EmployeeId { get; set; }

        [Required]
        [MaxLength(100)]
        [Display(Name = "Allowance Name")]
        public string AllowanceName { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Display(Name = "Frequency")]
        public AllowanceFrequency Frequency { get; set; }

        [Display(Name = "Is Taxable")]
        public bool IsTaxable { get; set; } = true;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        public string FrequencyDisplay => Utility.EnumHelper.GetDisplayName(Frequency);
    }
}