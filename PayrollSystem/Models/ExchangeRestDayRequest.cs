using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models.BuildUp;
using PayrollSystem.Models.Workflow;

namespace PayrollSystem.Models
{
    public class ExchangeRestDayRequest : UserActivity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public int? OrganizationStructureId { get; set; }
        public virtual OrganizationStructure? OrganizationStructure { get; set; }

        [DataType(DataType.Date)]
        public DateOnly DateFiled { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required]
        public Guid FirstEmployeeId { get; set; }
        public virtual Employee FirstEmployee { get; set; } = null!;

        [Required]
        public Guid SecondEmployeeId { get; set; }
        public virtual Employee SecondEmployee { get; set; } = null!;

        // Employee 1's rest/off date before the exchange.
        [Required]
        [DataType(DataType.Date)]
        public DateOnly FirstRestDate { get; set; }

        // Employee 2's rest/off date before the exchange.
        [Required]
        [DataType(DataType.Date)]
        public DateOnly SecondRestDate { get; set; }

        // Emp1 shift on FirstRestDate (must be leave/rest).
        [Required]
        public int FirstRestShiftTypeId { get; set; }
        public virtual ShiftType FirstRestShiftType { get; set; } = null!;

        // Emp2 shift on SecondRestDate (must be leave/rest).
        [Required]
        public int SecondRestShiftTypeId { get; set; }
        public virtual ShiftType SecondRestShiftType { get; set; } = null!;

        // Emp1 shift on SecondRestDate (must be work — not leave).
        [Required]
        public int FirstWorkShiftTypeId { get; set; }
        public virtual ShiftType FirstWorkShiftType { get; set; } = null!;

        // Emp2 shift on FirstRestDate (must be work — not leave).
        [Required]
        public int SecondWorkShiftTypeId { get; set; }
        public virtual ShiftType SecondWorkShiftType { get; set; } = null!;

        [Required]
        [MaxLength(2000)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? SubmittedBy { get; set; }

        public DateTime SubmittedOn { get; set; } = DateTime.Now;

        public JustificationStatus Status { get; set; } = JustificationStatus.Pending;

        [MaxLength(1000)]
        public string? ReviewNotes { get; set; }

        [MaxLength(256)]
        public string? ReviewedById { get; set; }
        public virtual ApplicationUser? ReviewedBy { get; set; }

        public DateTime? ReviewedOn { get; set; }

        public int? ApprovalRequestId { get; set; }
        public virtual ApprovalRequest? ApprovalRequest { get; set; }
    }
}
