using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class Payslip : UserActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Payroll Entry")]
        public int PayrollEntryId { get; set; }
        public virtual PayrollEntry PayrollEntry { get; set; }

        [Required]
        [MaxLength(50)]
        [Display(Name = "Payslip Number")]
        public string PayslipNumber { get; set; }

        [Required]
        [Display(Name = "Generated Date")]
        public DateTime GeneratedDate { get; set; } = DateTime.Now;

        [Display(Name = "Sent Date")]
        public DateTime? SentDate { get; set; }

        [Display(Name = "Printed Date")]
        public DateTime? PrintedDate { get; set; }

        [MaxLength(500)]
        [Display(Name = "File Path")]
        public string? FilePath { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Email Address")]
        public string? EmailAddress { get; set; }

        [Display(Name = "Is Sent")]
        public bool IsSent { get; set; } = false;

        [Display(Name = "Is Printed")]
        public bool IsPrinted { get; set; } = false;

        [Display(Name = "Send Attempts")]
        public int SendAttempts { get; set; } = 0;

        [MaxLength(2000)]
        [Display(Name = "Send Error Message")]
        public string? SendErrorMessage { get; set; }

        // Employee snapshot for historical reference
        [MaxLength(200)]
        [Display(Name = "Employee Name")]
        public string EmployeeName { get; set; }

        [MaxLength(50)]
        [Display(Name = "Employee ID")]
        public string EmployeeIdNumber { get; set; }

        [MaxLength(100)]
        [Display(Name = "Department")]
        public string DepartmentName { get; set; }

        [MaxLength(100)]
        [Display(Name = "Designation")]
        public string DesignationName { get; set; }

        [MaxLength(100)]
        [Display(Name = "Rate Type")]
        public string RateTypeName { get; set; }

        // Payroll period snapshot
        [DataType(DataType.Date)]
        [Display(Name = "Period Start")]
        public DateOnly PeriodStartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Period End")]
        public DateOnly PeriodEndDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Pay Date")]
        public DateOnly PayDate { get; set; }

        // Financial summary snapshot
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Gross Pay")]
        public decimal GrossPay { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Benefits")]
        public decimal TotalBenefits { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Deductions")]
        public decimal TotalDeductions { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Net Pay")]
        public decimal NetPay { get; set; }

        [MaxLength(2000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        // Alias for GeneratedDate for backward compatibility
        [NotMapped]
        public DateTime DateCreated => GeneratedDate;

        // Computed properties
        [NotMapped]
        public string PeriodRange => $"{PeriodStartDate:MMM dd, yyyy} - {PeriodEndDate:MMM dd, yyyy}";

        [NotMapped]
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
    }
}