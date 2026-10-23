using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.ViewModels.WorkSchedule
{
    public class WorkScheduleViewModel
    {
        public int ScheduleId { get; set; }
        public int? ApprovalRequestId { get; set; }
        public int OrganizationStructureId { get; set; }
        public string OrganizationUnitDisplay { get; set; } = string.Empty;
        public int Month { get; set; }
        public int Year { get; set; }
        public WorkScheduleStatus Status { get; set; } = WorkScheduleStatus.Draft;
        public string? RejectionReason { get; set; }
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public bool IsEditable { get; set; }
        public bool CanSubmit { get; set; }
        public bool CanApprove { get; set; }
        public bool CanReject { get; set; }
        public bool CanReopen { get; set; }

        public string StatusBadgeClass => Status switch
        {
            WorkScheduleStatus.Draft => "badge-secondary",
            WorkScheduleStatus.Submitted => "badge-warning",
            WorkScheduleStatus.Approved => "badge-success",
            WorkScheduleStatus.Rejected => "badge-danger",
            _ => "badge-secondary"
        };

        public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM");
        public int DaysInMonth => DateTime.DaysInMonth(Year, Month);

        public List<OrganizationUnitOptionViewModel> OrganizationUnits { get; set; } = new();
        public List<ShiftType> ShiftTypes { get; set; } = new();
        public List<SectionScheduleGroup> Sections { get; set; } = new();
        public Dictionary<Guid, Dictionary<int, ShiftType>> ScheduleMap { get; set; } = new();

        public ShiftType GetShift(Guid employeeId, int day)
        {
            if (ScheduleMap.TryGetValue(employeeId, out var empMap) && empMap.TryGetValue(day, out var shift))
                return shift;
            return ShiftTypeScheduleHelper.BlankShift;
        }

        public string GetOnDuty(int day)
        {
            int onDuty = 0, total = 0;
            foreach (var section in Sections)
            {
                foreach (var emp in section.Employees)
                {
                    total++;
                    if (ScheduleMap.TryGetValue(emp.Id, out var empMap) &&
                        empMap.TryGetValue(day, out var shift) &&
                        !ShiftTypeScheduleHelper.IsUnassigned(shift) &&
                        !shift.IsLeave)
                    {
                        onDuty++;
                    }
                }
            }
            return $"{onDuty}/{total}";
        }

        public EmployeeSummaryDto GetSummary(Guid employeeId)
        {
            var summary = new EmployeeSummaryDto { EmployeeId = employeeId };
            if (!ScheduleMap.TryGetValue(employeeId, out var empMap))
                return summary;

            foreach (var shift in empMap.Values)
            {
                if (ShiftTypeScheduleHelper.IsUnassigned(shift))
                    continue;

                if (shift.IsLeave)
                    summary.LeaveOffCount++;
                else
                {
                    summary.DutyDays++;
                    summary.DutyHours += shift.Hours;
                }
            }
            return summary;
        }

        public bool IsWeekend(int day)
        {
            var date = new DateTime(Year, Month, day);
            return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
        }

        public string DayName(int day) => new DateTime(Year, Month, day).ToString("ddd");
    }

    public class OrganizationUnitOptionViewModel
    {
        public int Id { get; set; }
        public string DisplayText { get; set; } = string.Empty;
    }

    public class SectionScheduleGroup
    {
        public int OrganizationStructureId { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<Employee> Employees { get; set; } = new();
    }
}
