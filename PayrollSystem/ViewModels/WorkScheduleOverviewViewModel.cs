using PayrollSystem.Models;

namespace PayrollSystem.ViewModels.WorkSchedule
{
    public class WorkScheduleOverviewViewModel
    {
        public int Year { get; set; }
        public int OrganizationStructureId { get; set; }
        public string OrganizationUnitDisplay { get; set; } = string.Empty;
        public int EmployeeCount { get; set; }
        public bool IsReadOnly { get; set; }
        public string DetailController { get; set; } = "WorkSchedule";

        public List<OrganizationUnitOptionViewModel> OrganizationUnits { get; set; } = new();
        public List<WorkScheduleMonthSummary> Months { get; set; } = new();

        public int ApprovedCount => Months.Count(m => m.Status == WorkScheduleStatus.Approved);
        public int SubmittedCount => Months.Count(m => m.Status == WorkScheduleStatus.Submitted);
        public int DraftCount => Months.Count(m => m.Status is WorkScheduleStatus.Draft or WorkScheduleStatus.Rejected);
        public int NotStartedCount => Months.Count(m => m.Status == null);
    }

    public class WorkScheduleMonthSummary
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public int? ScheduleId { get; set; }
        public WorkScheduleStatus? Status { get; set; }
        public int EntryCount { get; set; }
        public int EmployeeCount { get; set; }
        public DateTime? LastModifiedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public bool IsCurrentMonth { get; set; }

        public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM");

        public string StatusLabel => Status switch
        {
            WorkScheduleStatus.Draft => "Draft",
            WorkScheduleStatus.Submitted => "Submitted",
            WorkScheduleStatus.Approved => "Approved",
            WorkScheduleStatus.Rejected => "Rejected",
            null => "Not Started",
            _ => "Unknown"
        };

        public string StatusBadgeClass => Status switch
        {
            WorkScheduleStatus.Draft => "badge-secondary",
            WorkScheduleStatus.Submitted => "badge-warning",
            WorkScheduleStatus.Approved => "badge-success",
            WorkScheduleStatus.Rejected => "badge-danger",
            null => "badge-light border",
            _ => "badge-secondary"
        };

        public string ProgressLabel
        {
            get
            {
                if (EmployeeCount <= 0)
                    return "No employees";

                if (Status == null || EntryCount == 0)
                    return "Not started";

                var daysInMonth = DateTime.DaysInMonth(Year, Month);
                var expected = EmployeeCount * daysInMonth;
                if (expected <= 0)
                    return $"{EntryCount} entries";

                var pct = Math.Min(100, (int)Math.Round(EntryCount * 100.0 / expected));
                return $"{pct}% filled";
            }
        }
    }
}
