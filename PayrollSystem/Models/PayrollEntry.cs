using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class PayrollEntry : UserActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Payroll Run")]
        public int PayrollRunId { get; set; }
        public virtual PayrollRun PayrollRun { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public Guid EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        // ── Hours worked ──────────────────────────────────────────────────────────

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Basic Hours")]
        public decimal RegularHours { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Overtime Hours")]
        public decimal OvertimeHours { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Night Differential Hours")]
        public decimal NightDifferentialHours { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Night Differential OT Hours")]
        public decimal NightDifferentialOvertimeHours { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Holiday Hours")]
        public decimal HolidayHours { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Rest Day Hours")]
        public decimal RestDayHours { get; set; } = 0;

        // ── Rate information ──────────────────────────────────────────────────────

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Hourly Rate")]
        public decimal HourlyRate { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Overtime Rate")]
        public decimal OvertimeRate { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Night Differential Rate")]
        public decimal NightDifferentialRate { get; set; } = 0;

        // ── Pay calculations ──────────────────────────────────────────────────────

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Regular Pay")]
        public decimal RegularPay { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Overtime Pay")]
        public decimal OvertimePay { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Night Differential Pay")]
        public decimal NightDifferentialPay { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Holiday Pay")]
        public decimal HolidayPay { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Rest Day Pay")]
        public decimal RestDayPay { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Gross Pay")]
        public decimal GrossPay { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Benefits")]
        public decimal TotalBenefits { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Deductions")]
        public decimal TotalDeductions { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Net Pay")]
        public decimal NetPay { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Tax Withheld")]
        public decimal TaxWithheld { get; set; } = 0;

        [MaxLength(1000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Is Processed")]
        public bool IsProcessed { get; set; } = false;

        // ── Navigation properties ─────────────────────────────────────────────────

        // Income tax / withholding tax deductions
        public virtual ICollection<PayrollDeduction> Deductions { get; set; } = new List<PayrollDeduction>();

        public virtual ICollection<PayrollEntryGovernmentContribution> GovernmentContributions { get; set; }
            = new List<PayrollEntryGovernmentContribution>();

        [NotMapped]
        public decimal TotalHours =>
            RegularHours + OvertimeHours + NightDifferentialHours
            + NightDifferentialOvertimeHours + HolidayHours + RestDayHours;


        [NotMapped]
        public decimal TotalGovernmentContributions =>
            GovernmentContributions?.Sum(g => g.EmployeeShare) ?? 0;

        [NotMapped]
        public decimal TotalAllDeductions => TotalDeductions + TotalGovernmentContributions;

        [NotMapped]
        public decimal TotalPay => GrossPay + TotalBenefits - TotalAllDeductions;

        [NotMapped]
        public DateTime? DateCreated => CreatedOn;

        [NotMapped]
        public DateTime? DateUpdated => UpdatedOn;
    }
}