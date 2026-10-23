using System.ComponentModel.DataAnnotations;

namespace PayrollSystem.Models
{
    // One "batch" of employees + time cards under a payroll period (e.g. Accounting + Regular).
    // Used for multi-batch Assign Employees & Time Card UI.
    public class PayrollPeriodBatch
    {
        public int Id { get; set; }
        public int PayrollPeriodId { get; set; }
        public PayrollPeriod PayrollPeriod { get; set; }

        // Display label, e.g. "Accounting, HR" or "Accounting"
        [MaxLength(200)]
        public string DepartmentDisplay { get; set; } = string.Empty;

        // Employment basis filter used for this batch, e.g. "Regular", "Contractual".
        [MaxLength(50)]
        public string EmploymentTypeDisplay { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }
        public int EmployeeCount { get; set; }
        public bool IsDeleted { get; set; }

        public string? CreatedById { get; set; }
        public DateTime? CreatedOn { get; set; }
    }
}
