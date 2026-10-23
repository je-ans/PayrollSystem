using PayrollSystem.Models.BuildUp;
using PayrollSystem.Models.Workflow;

namespace PayrollSystem.Models
{
    public class WorkSchedule
    {
        public int Id { get; set; }

        public int OrganizationStructureId { get; set; }
        public OrganizationStructure? OrganizationStructure { get; set; }

        public int Month { get; set; }

        public int Year { get; set; }

        public WorkScheduleStatus Status { get; set; } = WorkScheduleStatus.Draft;

        public bool IsLocked { get; set; }

        public string? LockedBy { get; set; }
        public DateTime? LockedAt { get; set; }

        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public Guid? SubmittedByEmployeeId { get; set; }
        public Employee? SubmittedByEmployee { get; set; }

        public int? ApprovalRequestId { get; set; }
        public ApprovalRequest? ApprovalRequest { get; set; }

        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public string? RejectedBy { get; set; }
        public DateTime? RejectedAt { get; set; }

        public string? RejectionReason { get; set; }

        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? LastModifiedBy { get; set; }
        public DateTime? LastModifiedAt { get; set; }

        public virtual ICollection<ScheduleEntry> Entries { get; set; } = new List<ScheduleEntry>();

        public bool IsEditable => Status is WorkScheduleStatus.Draft or WorkScheduleStatus.Rejected;

        public string MonthName =>
            new DateTime(Year, Month, 1).ToString("MMMM").ToUpper();
    }
}
