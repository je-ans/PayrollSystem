using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    /// <summary>
    /// Stores the computed government contribution amounts per employee per payroll entry.
    /// Kept separate from Deduction — contributions have both employee and employer shares
    /// and are identified by GovernmentContributionTypeId (no hardcoded string mapping).
    /// </summary>
    public class PayrollEntryGovernmentContribution
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PayrollEntryId { get; set; }
        public virtual PayrollEntry PayrollEntry { get; set; }

        // FK to GovernmentContributionType — the only identifier used throughout.
        // Never rely on Code string ("SSS", "PHILHEALTH", etc.) in computation logic.
        [Required]
        public int GovernmentContributionTypeId { get; set; }
        public virtual GovernmentContributionType GovernmentContributionType { get; set; }

        // Snapshot of the type name at the time of processing for display/reporting.
        // e.g. "Social Security System", "PhilHealth Contribution", "Pag-IBIG Contribution"
        [MaxLength(200)]
        public string ContributionName { get; set; }

        // Amount deducted from the employee's pay.
        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployeeShare { get; set; }

        // Employer's matching contribution (not deducted from employee pay; for cost reporting)
        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployerShare { get; set; }

        // EmployeeShare + EmployerShare.
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalContribution { get; set; }

        // The monthly salary basis used for bracket lookup at the time of processing.
        // Stored for audit trail and payslip display purposes.
        [Column(TypeName = "decimal(18,2)")]
        public decimal SalaryBasis { get; set; }
    }
}