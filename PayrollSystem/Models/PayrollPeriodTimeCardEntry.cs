using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    /// Manual time card inputs for a payroll period (no scheduling needed).
    /// One row per (PayrollPeriod, Employee). Hours are broken down by DayType via Details.
    public class PayrollPeriodTimeCardEntry : UserActivity
    {
        [Key]
        public int Id { get; set; }

        public int PayrollPeriodId { get; set; }
        public PayrollPeriod PayrollPeriod { get; set; }

        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; }

        /// <summary>Optional batch this entry belongs to (for multi-batch Assign &amp; Time Card).</summary>
        public int? PayrollPeriodBatchId { get; set; }
        public PayrollPeriodBatch? PayrollPeriodBatch { get; set; }

        // Late/undertime/absence are not day-type specific — they remain on the header
        // and feed DeductionTypes (LATE_UNDERTIME, ABSENCE).
        public int LateMinutes { get; set; } = 0;
        public int UndertimeMinutes { get; set; } = 0;

        /// <summary>Scheduled-work hours with no complete DTR pair; used for Absence Deduction.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal AbsentHours { get; set; } = 0;

        [MaxLength(1000)]
        public string? Notes { get; set; }

        /// <summary>Per-day-type hour breakdown.</summary>
        public List<PayrollPeriodTimeCardDetail> Details { get; set; } = new();
    }
}
