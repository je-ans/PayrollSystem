using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models.Workflow;

namespace PayrollSystem.Models
{
    public class ScheduleChangeRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid EmployeeId { get; set; }
        public virtual Employee Employee { get; set; } = null!;

        public int WorkScheduleId { get; set; }
        public virtual WorkSchedule WorkSchedule { get; set; } = null!;

        public int Day { get; set; }

        public int CurrentShiftTypeId { get; set; }
        public virtual ShiftType CurrentShiftType { get; set; } = null!;

        public int RequestedShiftTypeId { get; set; }
        public virtual ShiftType RequestedShiftType { get; set; } = null!;

        [Required, MaxLength(2000)]
        public string Reason { get; set; } = string.Empty;

        public JustificationStatus Status { get; set; } = JustificationStatus.Pending;

        [MaxLength(1000)]
        public string? ReviewNotes { get; set; }

        [MaxLength(256)]
        public string? SubmittedBy { get; set; }

        public DateTime SubmittedOn { get; set; } = DateTime.Now;

        public int? ApprovalRequestId { get; set; }
        public virtual ApprovalRequest? ApprovalRequest { get; set; }

        public string? ReviewedById { get; set; }
        public virtual ApplicationUser? ReviewedBy { get; set; }

        public DateTime? ReviewedOn { get; set; }
    }
}
