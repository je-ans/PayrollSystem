using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;
using PayrollSystem.Models.Workflow;

namespace PayrollSystem.ViewModels
{
    public class ScheduleChangeRequestListViewModel
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeIdNumber { get; set; } = string.Empty;
        public int Month { get; set; }
        public int Year { get; set; }
        public int Day { get; set; }
        public string CurrentShiftCode { get; set; } = string.Empty;
        public string RequestedShiftCode { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public JustificationStatus Status { get; set; }
        public int? ApprovalRequestId { get; set; }
        public ApprovalRequestStatus? ApprovalWorkflowStatus { get; set; }
        public int? CurrentStepOrder { get; set; }
        public bool CanReview { get; set; }
        public string SubmittedBy { get; set; } = string.Empty;
        public DateTime SubmittedOn { get; set; }
        public string OrganizationUnitName { get; set; } = string.Empty;
    }

    public class ScheduleChangeRequestFormViewModel
    {
        public Guid EmployeeId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public int Day { get; set; }
        public string DateDisplay { get; set; } = string.Empty;
        public string CurrentShiftCode { get; set; } = string.Empty;
        public string CurrentShiftLabel { get; set; } = string.Empty;
        public string CurrentShiftColorHex { get; set; } = "#6b7280";
        public int? CurrentShiftTypeId { get; set; }

        [Required(ErrorMessage = "Please select a requested shift.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a requested shift.")]
        [Display(Name = "Requested shift")]
        public int RequestedShiftTypeId { get; set; }

        [Required(ErrorMessage = "Reason is required.")]
        [Display(Name = "Reason for change")]
        public string Reason { get; set; } = string.Empty;
        public List<ShiftTypeOptionViewModel> ShiftTypes { get; set; } = new();
        public bool HasPendingRequest { get; set; }
        public string? StatusMessage { get; set; }
        public int? OrganizationStructureId { get; set; }
        public string OrganizationUnitDisplay { get; set; } = string.Empty;
    }

    public class ShiftTypeOptionViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#6b7280";
    }

    public class ScheduleChangeProcessViewModel
    {
        public Guid Id { get; set; }
        public JustificationStatus ReviewAction { get; set; }

        [Display(Name = "Review notes")]
        public string ReviewNotes { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeIdNumber { get; set; } = string.Empty;
        public string OrganizationUnitDisplay { get; set; } = string.Empty;
        public string DateDisplay { get; set; } = string.Empty;
        public string CurrentShiftCode { get; set; } = string.Empty;
        public string CurrentShiftLabel { get; set; } = string.Empty;
        public string RequestedShiftCode { get; set; } = string.Empty;
        public string RequestedShiftLabel { get; set; } = string.Empty;
        public string CurrentShiftColorHex { get; set; } = "#6b7280";
        public string RequestedShiftColorHex { get; set; } = "#6b7280";
        public string Reason { get; set; } = string.Empty;
        public int? ApprovalRequestId { get; set; }
        public JustificationStatus CurrentStatus { get; set; } = JustificationStatus.Pending;
        public string SubmittedBy { get; set; } = string.Empty;
        public DateTime SubmittedOn { get; set; }
        public DateTime? ReviewedOn { get; set; }
        public bool CanApprove { get; set; }
        public bool CanReject { get; set; }
        public ApprovalRequestTimelineViewModel? ApprovalTimeline { get; set; }
        public ProcessRequestSummaryViewModel Summary { get; set; } = new();
    }
}
