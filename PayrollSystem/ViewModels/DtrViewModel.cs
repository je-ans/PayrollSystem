using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;
using PayrollSystem.Models.Workflow;

namespace PayrollSystem.ViewModels
{
    // View model for Civil Service Form No. 48 - Daily Time Record document.
    public class DtrViewModel
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeIdNumber { get; set; } = string.Empty;
        public string MonthYear { get; set; } = string.Empty;
        public int Month { get; set; }
        public int Year { get; set; }
        public int DaysInMonth { get; set; }
        public string RegularDaysHours { get; set; } = string.Empty;
        public string SaturdaysHours { get; set; } = string.Empty;
        public List<DtrDayRowViewModel> DayRows { get; set; } = new();
        public int TardinessMinutes { get; set; }
        public int UndertimeMinutes { get; set; }
        public int TardinessFrequency { get; set; }
        public int UndertimeFrequency { get; set; }

        // Credited days for the month (shift hours ÷ 8). Includes work and paid leave; excludes DAY OFF/HOL.</summary>
        public decimal TotalDays { get; set; }
    }

    public class DtrDayRowViewModel
    {
        public int Day { get; set; }
        public bool IsDayOff { get; set; }
        public bool WorkedOnDayOff { get; set; }
        public string ShiftCode { get; set; } = "—";
        public string ShiftLabel { get; set; } = string.Empty;
        public string ShiftColorHex { get; set; } = "#6b7280";
        public string AmArrival { get; set; } = string.Empty;
        public string AmDeparture { get; set; } = string.Empty;
        public string PmArrival { get; set; } = string.Empty;
        public string PmDeparture { get; set; } = string.Empty;
        public bool ExpectsLunchBreak { get; set; }
        public bool AssessAttendance { get; set; }
        public string TardinessUndertimeHrs { get; set; } = "-";
        public string TardinessUndertimeMins { get; set; } = "-";
        public string UndertimeHrs { get; set; } = "-";
        public string UndertimeMins { get; set; } = "-";
        public string? SpecialDayLabel { get; set; }
        public bool ShowScheduledLeaveWithLogs { get; set; }
        public bool ShowFlexibleOfficialTime { get; set; }
        public string FlexibleOfficialTimeLabel { get; set; } = string.Empty;
        public bool UsesOvernightLayout { get; set; }
        public bool IsUserProvided { get; set; }
    }
    public class DtrManagementRowViewModel
    {
        public DateOnly Date { get; set; }
        public int DayNo { get; set; }
        public string DayName { get; set; } = string.Empty;

        public string ShiftCode { get; set; } = "—";
        public string ShiftLabel { get; set; } = string.Empty;
        public string ShiftColorHex { get; set; } = "#6b7280";

        public string AmArrival { get; set; } = "-";
        public string AmDeparture { get; set; } = "-";
        public string PmArrival { get; set; } = "-";
        public string PmDeparture { get; set; } = "-";

        public string TardinessHrs { get; set; } = "-";
        public string TardinessMins { get; set; } = "-";
        public string UndertimeHrs { get; set; } = "-";
        public string UndertimeMins { get; set; } = "-";

        // WorkingCalendar DayType code (attendance only — no pay amounts)
        public string DayTypeCode { get; set; } = "—";

        //Friendly DayType name for DTR display (e.g. Regular Day)
        public string DayTypeName { get; set; } = string.Empty;
        public string WorkedHoursDisplay { get; set; } = "—";
        public string NightDifferentialHoursDisplay { get; set; } = "—";
        public decimal WorkedHours { get; set; }
        public decimal ActualPresenceHours { get; set; }
        public decimal NightDifferentialHours { get; set; }

        //Post-shift eligible OT hours (internal / Overtime column when no request yet)
        public decimal PotentialOvertimeHours { get; set; }
        public int EarlyArrivalMinutes { get; set; }

        public bool ExpectsLunchBreak { get; set; }
        public bool UsesOvernightLayout { get; set; }
        public bool ExpectsTonightStart { get; set; }
        public bool ExpectsPreviousNightEnd { get; set; }
        public bool IsOvernightShiftDay { get; set; }
        public bool HasCompleteShiftPair { get; set; }
        public bool AssessAttendance { get; set; }

        public bool JustificationRequired { get; set; }
        public string TimeLogJustification { get; set; } = "-";
        public JustificationStatus? JustificationStatus { get; set; }
        public Guid? PendingJustificationId { get; set; }
        public Guid? LatestJustificationRequestId { get; set; }
        public bool HasTimeEntries { get; set; }

        public JustificationStatus? ScheduleChangeStatus { get; set; }
        public Guid? PendingScheduleChangeId { get; set; }
        public string ScheduleChangeRemarks { get; set; } = "-";
        public Guid? LatestScheduleChangeRequestId { get; set; }

        public OvertimeStatus? OvertimeStatus { get; set; }
        public Guid? PendingOvertimeRequestId { get; set; }
        public Guid? LatestOvertimeRequestId { get; set; }
        public string OvertimeRemarks { get; set; } = "-";
        public string? OvertimeHoursDisplay { get; set; }

        //Workflow label for the Overtime column, e.g. Approved (2h), Pending Approval (2h)
        public string OvertimeStatusLabel { get; set; } = "—";
        public bool IsOvertimeProcessedToPayroll { get; set; }

        public Guid? LatestUndertimeRequestId { get; set; }
        public Guid? PendingUndertimeRequestId { get; set; }
        public JustificationStatus? UndertimeStatus { get; set; }
        public string UndertimeRemarks { get; set; } = "-";
        public string UndertimeHoursDisplay { get; set; } = "—";
        public string UndertimeStatusLabel { get; set; } = "—";
        public int ComputedUndertimeMinutes { get; set; }
    }

    public class DtrArrangeTimeLogViewModel
    {
        public Guid EmployeeId { get; set; }
        public DateOnly Date { get; set; }
        public string DateDisplay { get; set; } = string.Empty;
        // Overnight, SimpleDay, or WithLunch — controls which slots are shown
        public string ArrangeLayout { get; set; } = "WithLunch";
        public List<DtrArrangeTimeLogSlotViewModel> Slots { get; set; } = new();
        public List<DtrArrangeTimeLogEntryViewModel> PoolEntries { get; set; } = new();
    }

    public class DtrArrangeTimeLogSlotViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DtrArrangeTimeLogEntryViewModel? Entry { get; set; }
    }

    public class DtrArrangeTimeLogEntryViewModel
    {
        public Guid Id { get; set; }
        public string TimeDisplay { get; set; } = string.Empty;
        public string TypeDisplay { get; set; } = string.Empty;
        public string SourceDisplay { get; set; } = string.Empty;
    }

    public class DtrArrangeTimeLogSubmitViewModel
    {
        public Guid EmployeeId { get; set; }
        public DateOnly Date { get; set; }
        public Guid? AmInEntryId { get; set; }
        public Guid? AmOutEntryId { get; set; }
        public Guid? PmInEntryId { get; set; }
        public Guid? PmOutEntryId { get; set; }
        public List<Guid> PoolEntryIds { get; set; } = new();
    }

    public class DtrJustificationListViewModel
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeIdNumber { get; set; } = string.Empty;
        public DateOnly EntryDate { get; set; }
        public string Justification { get; set; } = string.Empty;
        public string RequestedTimes { get; set; } = string.Empty;
        public JustificationStatus Status { get; set; }
        public int? ApprovalRequestId { get; set; }
        public ApprovalRequestStatus? ApprovalWorkflowStatus { get; set; }
        public int? CurrentStepOrder { get; set; }
        public bool CanReview { get; set; }
        public string SubmittedBy { get; set; } = string.Empty;
        public DateTime SubmittedOn { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedOn { get; set; }
    }

    public class DtrJustificationProcessViewModel
    {
        public Guid Id { get; set; }
        // Approve or Reject — posted from the review form (int avoids Status binding issues)
        public JustificationStatus ReviewAction { get; set; }
        public string ReviewNotes { get; set; } = string.Empty;

        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeIdNumber { get; set; } = string.Empty;
        public string OrganizationUnitDisplay { get; set; } = string.Empty;
        public DateOnly EntryDate { get; set; }
        public string DateDisplay { get; set; } = string.Empty;
        public string Justification { get; set; } = string.Empty;
        public string RequestedTimes { get; set; } = string.Empty;
        public List<string> BiometricLogs { get; set; } = new();
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

    public class DtrJustificationFormViewModel
    {
        public Guid EmployeeId { get; set; }
        public DateOnly Date { get; set; }
        public string DateDisplay { get; set; } = string.Empty;
        public string ScheduleLayout { get; set; } = "WithLunch";
        public DateOnly? RelatesToPreviousShiftDate { get; set; }
        public string? InfoMessage { get; set; }
        public List<DtrMissingSlotViewModel> MissingSlots { get; set; } = new();
        public List<string> BiometricLogs { get; set; } = new();

        [Required(ErrorMessage = "Justification is required.")]
        [Display(Name = "Justification")]
        public string? Justification { get; set; }
        public bool HasPendingRequest { get; set; }
        public JustificationStatus? ExistingStatus { get; set; }
        public string? StatusMessage { get; set; }
    }

    public class DtrMissingSlotViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class DtrJustificationSubmitViewModel
    {
        public Guid EmployeeId { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly? AmIn { get; set; }
        public TimeOnly? LunchOut { get; set; }
        public TimeOnly? LunchIn { get; set; }
        public TimeOnly? PmOut { get; set; }
        public TimeOnly? PmIn { get; set; }
        public TimeOnly? AmOut { get; set; }
        public DateOnly? RelatesToPreviousShiftDate { get; set; }
        public string Justification { get; set; } = string.Empty;
    }
}
