using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;
using PayrollSystem.Models.Workflow;

namespace PayrollSystem.ViewModels
{
    public class OvertimeRequestListViewModel
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeIdNumber { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public string DateRangeDisplay { get; set; } = string.Empty;
        public string RequestedTimes { get; set; } = string.Empty;
        public string? ApprovedTimes { get; set; }
        public bool HasLunchBreak { get; set; }
        public string? LunchBreakDisplay { get; set; }
        public decimal HoursPerDay { get; set; }
        public int DayCount { get; set; }
        public decimal RequestedHours { get; set; }
        public decimal? ApprovedHours { get; set; }
        public string Reason { get; set; } = string.Empty;
        public OvertimeStatus Status { get; set; }
        public int? ApprovalRequestId { get; set; }
        public ApprovalRequestStatus? ApprovalWorkflowStatus { get; set; }
        public int? CurrentStepOrder { get; set; }
        public bool CanReview { get; set; }
        public bool IsProcessedToPayroll { get; set; }
        public string SubmittedBy { get; set; } = string.Empty;
        public DateTime SubmittedOn { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedOn { get; set; }
        public string? DayTypeName { get; set; }
    }

    public class OvertimeRequestFormViewModel
    {
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Start date is required.")]
        [Display(Name = "Start date")]
        [DataType(DataType.Date)]
        public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required(ErrorMessage = "End date is required.")]
        [Display(Name = "End date")]
        [DataType(DataType.Date)]
        public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required(ErrorMessage = "Start time is required.")]
        [Display(Name = "Start time")]
        [DataType(DataType.Time)]
        public TimeOnly? RequestedStartTime { get; set; }

        [Required(ErrorMessage = "End time is required.")]
        [Display(Name = "End time")]
        [DataType(DataType.Time)]
        public TimeOnly? RequestedEndTime { get; set; }

        [Display(Name = "Has lunch break")]
        public bool HasLunchBreak { get; set; }

        [Display(Name = "Lunch start")]
        [DataType(DataType.Time)]
        public TimeOnly? LunchStart { get; set; }

        [Display(Name = "Lunch end")]
        [DataType(DataType.Time)]
        public TimeOnly? LunchEnd { get; set; }

        [Required(ErrorMessage = "Reason is required.")]
        [Display(Name = "Reason")]
        [MaxLength(2000)]
        public string Reason { get; set; } = string.Empty;

        public bool HasPendingRequest { get; set; }
        public string? StatusMessage { get; set; }
        public bool RequiresApproval { get; set; } = true;
    }

    public class OvertimeRequestProcessViewModel
    {
        public Guid Id { get; set; }
        public OvertimeStatus ReviewAction { get; set; }

        [Display(Name = "Review notes")]
        public string ReviewNotes { get; set; } = string.Empty;

        [Display(Name = "Approved start")]
        [DataType(DataType.Time)]
        public TimeOnly? ApprovedStartTime { get; set; }

        [Display(Name = "Approved end")]
        [DataType(DataType.Time)]
        public TimeOnly? ApprovedEndTime { get; set; }

        [Display(Name = "Has lunch break")]
        public bool ApprovedHasLunchBreak { get; set; }

        [Display(Name = "Lunch start")]
        [DataType(DataType.Time)]
        public TimeOnly? ApprovedLunchStart { get; set; }

        [Display(Name = "Lunch end")]
        [DataType(DataType.Time)]
        public TimeOnly? ApprovedLunchEnd { get; set; }

        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeIdNumber { get; set; } = string.Empty;
        public string OrganizationUnitDisplay { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public string DateDisplay { get; set; } = string.Empty;
        public string RequestedTimes { get; set; } = string.Empty;
        public bool HasLunchBreak { get; set; }
        public string? LunchBreakDisplay { get; set; }
        public decimal GrossHoursPerDay { get; set; }
        public decimal HoursPerDay { get; set; }
        public int DayCount { get; set; }
        public decimal RequestedHours { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? DayTypeName { get; set; }
        public int? ApprovalRequestId { get; set; }
        public OvertimeStatus CurrentStatus { get; set; } = OvertimeStatus.Pending;
        public bool IsProcessedToPayroll { get; set; }
        public string SubmittedBy { get; set; } = string.Empty;
        public DateTime SubmittedOn { get; set; }
        public DateTime? ReviewedOn { get; set; }
        public bool CanApprove { get; set; }
        public bool CanReject { get; set; }
        public ApprovalRequestTimelineViewModel? ApprovalTimeline { get; set; }
        public ProcessRequestSummaryViewModel Summary { get; set; } = new();
    }

    public class OvertimeRequestPrintViewModel
    {
        public Guid Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyAddress { get; set; }
        public string? CompanyLogoUrl { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeIdNumber { get; set; } = string.Empty;
        public string? Designation { get; set; }
        public string OrganizationUnitDisplay { get; set; } = string.Empty;
        public string DateRangeDisplay { get; set; } = string.Empty;
        public string DailyTimeDisplay { get; set; } = string.Empty;
        public string? LunchBreakDisplay { get; set; }
        public decimal GrossHoursPerDay { get; set; }
        public decimal NetHoursPerDay { get; set; }
        public int DayCount { get; set; }
        public decimal TotalNetHours { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
        public string SubmittedBy { get; set; } = string.Empty;
        public DateTime SubmittedOn { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedOn { get; set; }
        public string? ReviewNotes { get; set; }
        public string? DayTypeName { get; set; }
        public DateTime PrintedOn { get; set; } = DateTime.Now;
        public List<OvertimeRequestPrintDayRow> Days { get; set; } = new();
    }

    public class OvertimeRequestPrintDayRow
    {
        public DateOnly Date { get; set; }
        public string DayName { get; set; } = string.Empty;
        public string TimeDisplay { get; set; } = string.Empty;
        public string? LunchBreakDisplay { get; set; }
        public decimal NetHours { get; set; }
    }
}
