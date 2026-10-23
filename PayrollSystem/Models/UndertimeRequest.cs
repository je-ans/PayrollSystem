using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models.Workflow;

namespace PayrollSystem.Models
{
    /// Formal undertime authorization request. Approval is administrative only —
    /// it does not modify punches, schedules, DTR calculations, or payroll.
    public class UndertimeRequest : UserActivity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid EmployeeId { get; set; }
        public virtual Employee Employee { get; set; } = null!;

        [DataType(DataType.Date)]
        public DateOnly DateFiled { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required]
        [DataType(DataType.Date)]
        public DateOnly DateOfUndertime { get; set; }

        [Range(0.01, 24)]
        public decimal DurationHours { get; set; }

        [Range(0.01, 24)]
        public decimal TotalHours { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Purpose { get; set; } = string.Empty;

        // DTR computed undertime at submission (informational only)
        public int? ComputedUndertimeMinutesAtSubmit { get; set; }

        [MaxLength(256)]
        public string? SubmittedBy { get; set; }

        public DateTime SubmittedOn { get; set; } = DateTime.Now;

        public JustificationStatus Status { get; set; } = JustificationStatus.Pending;

        [MaxLength(1000)]
        public string? ReviewNotes { get; set; }

        [MaxLength(450)]
        public string? ReviewedById { get; set; }
        public virtual ApplicationUser? ReviewedBy { get; set; }

        public DateTime? ReviewedOn { get; set; }

        public int? ApprovalRequestId { get; set; }
        public virtual ApprovalRequest? ApprovalRequest { get; set; }
    }
}
