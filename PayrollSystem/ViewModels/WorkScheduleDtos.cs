using PayrollSystem.Models;

namespace PayrollSystem.ViewModels.WorkSchedule
{
    public class SaveEntryDto
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public int OrganizationStructureId { get; set; }
        public Guid EmployeeId { get; set; }
        public int Day { get; set; }
        public int ShiftTypeId { get; set; }
        public string? Remarks { get; set; }
    }

    public class ScheduleWorkflowDto
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public int OrganizationStructureId { get; set; }
    }

    public class ScheduleRejectDto : ScheduleWorkflowDto
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class SaveEntryResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public EmployeeSummaryDto? UpdatedSummary { get; set; }
    }

    public class EmployeeSummaryDto
    {
        public Guid EmployeeId { get; set; }
        public decimal DutyDays { get; set; }
        public decimal DutyHours { get; set; }
        public int LeaveOffCount { get; set; }
    }
}
