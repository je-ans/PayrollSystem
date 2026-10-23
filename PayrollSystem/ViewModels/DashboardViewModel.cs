namespace PayrollSystem.ViewModels
{
    public class DashboardViewModel
    {
        // ── Context ──────────────────────────────────────────────────────────
        public int? SelectedPeriodId { get; set; }
        public string? SelectedPeriodName { get; set; }
        public bool SelectedPeriodIsLocked { get; set; }
        public DateOnly? SelectedPeriodStart { get; set; }
        public DateOnly? SelectedPeriodEnd { get; set; }
        public DateOnly? SelectedPeriodPayDate { get; set; }
        public List<DashboardPeriodOption> PeriodOptions { get; set; } = new();

        public DashboardAudience Audience { get; set; } = new();

        // ── Employees (global) ────────────────────────────────────────────────
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }

        // ── Payroll periods (global) ─────────────────────────────────────────
        public int PayrollPeriodsCount { get; set; }
        public int ActivePayrollPeriodsCount { get; set; }
        public int CompletedPayrollRunsCount { get; set; }
        public int DraftPayrollRunsCount { get; set; }

        // ── Period payroll totals ────────────────────────────
        public string? PeriodPayrollRunName { get; set; }
        public DateTime? PeriodPayrollProcessedDate { get; set; }
        public decimal? PeriodGrossPay { get; set; }
        public decimal? PeriodTotalDeductions { get; set; }
        public decimal? PeriodTotalBenefits { get; set; }
        public decimal? PeriodTotalNetPay { get; set; }
        public int PeriodPayrollEmployeeCount { get; set; }
        public bool HasPeriodPayrollTotals { get; set; }

        public decimal? LatestPayrollGrossPay => PeriodGrossPay;
        public decimal? LatestPayrollTotalDeductions => PeriodTotalDeductions;
        public decimal? LatestPayrollTotalBenefits => PeriodTotalBenefits;
        public decimal? LatestPayrollTotalNetPay => PeriodTotalNetPay;
        public int LatestPayrollEmployeeCount => PeriodPayrollEmployeeCount;
        public string? LatestPayrollRunName => PeriodPayrollRunName;
        public string? LatestPayrollPeriodName => SelectedPeriodName;
        public DateTime? LatestPayrollProcessedDate => PeriodPayrollProcessedDate;

        public List<DashboardRecentPayrollItem> RecentPayrollRuns { get; set; } = new();
        public List<DashboardRecentPayrollItem> DraftPayrollRuns { get; set; } = new();

        // ── Pending approvals ────────────────────────────────────────────────
        public DashboardPendingApprovals PendingApprovals { get; set; } = new();

        // ── Payroll progress ─────────────────────────────────────────────────
        public DashboardPayrollProgress PayrollProgress { get; set; } = new();

        // ── Overtime summary ─────────────────────────────────────────────────
        public DashboardOvertimeSummary OvertimeSummary { get; set; } = new();

        // ── Attendance summary ───────────────────────────────────────────────
        public DashboardAttendanceSummary AttendanceSummary { get; set; } = new();

        // ── Attention ────────────────────────────────────────────────────────
        public DashboardAttention Attention { get; set; } = new();

        // ── Upcoming / activity ──────────────────────────────────────────────
        public List<DashboardUpcomingItem> UpcomingEvents { get; set; } = new();
        public List<DashboardActivityItem> RecentActivities { get; set; } = new();

        // ── Charts ───────────────────────────────────────────────────────────
        public DashboardChartPayload Charts { get; set; } = new();

        // Setup (kept for potential use)
        public int TaxTablesCount { get; set; }
        public int GovernmentContributionTypesCount { get; set; }
        public int GovernmentContributionTablesCount { get; set; }
        public int PayrollConfigurationsCount { get; set; }
    }

    public class DashboardChartPayload
    {
        public DashboardAttendanceTrendChart AttendanceTrend { get; set; } = new();
        public DashboardPayrollCostChart PayrollCost { get; set; } = new();
        public DashboardEmployeeStatusChart EmployeeStatus { get; set; } = new();
    }

    public class DashboardAttendanceTrendChart
    {
        public List<string> Labels { get; set; } = new();
        public List<int> Present { get; set; } = new();
        public List<int> Late { get; set; } = new();
        public List<int> Absent { get; set; } = new();
        public List<int> Leave { get; set; } = new();
        public List<string> DateKeys { get; set; } = new(); // yyyy-MM-dd for click nav
        public string DtrUrl { get; set; } = "/Dtr";
    }

    public class DashboardPayrollCostChart
    {
        public List<string> Labels { get; set; } = new();
        public List<decimal> BasicPay { get; set; } = new();
        public List<decimal> OvertimePay { get; set; } = new();
        public List<decimal> Benefits { get; set; } = new();
        public List<decimal> Deductions { get; set; } = new();
        public List<int> PayrollRunIds { get; set; } = new();
        public List<int> PayrollPeriodIds { get; set; } = new();
        public string PayrollUrl { get; set; } = "/Payroll";
    }

    public class DashboardEmployeeStatusChart
    {
        public List<string> Labels { get; set; } = new();
        public List<int> Counts { get; set; } = new();
        public string EmployeeUrl { get; set; } = "/Employee";
    }

    public class DashboardAudience
    {
        public bool IsAdmin { get; set; }
        public bool IsPayrollOfficer { get; set; }
        public bool IsHr { get; set; }
        public bool IsDepartmentHead { get; set; }

        public bool ShowPayrollSummary { get; set; } = true;
        public bool ShowPayrollProgress { get; set; } = true;
        public bool ShowPendingApprovals { get; set; } = true;
        public bool ShowOvertimeSummary { get; set; } = true;
        public bool ShowAttendanceSummary { get; set; } = true;
        public bool ShowAttention { get; set; } = true;
        public bool ShowUpcoming { get; set; } = true;
        public bool ShowRecentActivity { get; set; } = true;
        public bool ShowRecentRuns { get; set; } = true;
        public bool ShowCharts { get; set; } = true;
    }

    public class DashboardPeriodOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsLocked { get; set; }
        public DateOnly PeriodStartDate { get; set; }
        public DateOnly PeriodEndDate { get; set; }
        public DateOnly PayDate { get; set; }
    }

    public class DashboardPendingApprovals
    {
        public int Overtime { get; set; }
        public int Leave { get; set; }
        public int DtrJustifications { get; set; }
        public int ScheduleChanges { get; set; }
        public int WorkSchedules { get; set; }
        public int Total => Overtime + Leave + DtrJustifications + ScheduleChanges + WorkSchedules;
    }

    public class DashboardPayrollProgress
    {
        public int TotalEmployees { get; set; }
        public int Processed { get; set; }
        public int Remaining => Math.Max(0, TotalEmployees - Processed);
        public bool IsLocked { get; set; }
        public bool IsOpen => !IsLocked;
        public string StatusLabel => IsLocked ? "Locked" : "Open";
        public double PercentComplete =>
            TotalEmployees <= 0 ? 0 : Math.Round(100.0 * Processed / TotalEmployees, 1);
    }

    public class DashboardOvertimeSummary
    {
        public int PendingApproval { get; set; }
        public int ApprovedPendingImport { get; set; }
        public int Imported { get; set; }
        public int Rejected { get; set; }
    }

    public class DashboardAttendanceSummary
    {
        public int Present { get; set; }
        public int Late { get; set; }
        public int Absent { get; set; }
        public int OnLeave { get; set; }
        public int RestDay { get; set; }
        public string? PeriodLabel { get; set; }
    }

    public class DashboardAttention
    {
        public int MissingDtrLogs { get; set; }
        public int MissingSchedules { get; set; }
        public int MissingTimeCards { get; set; }
        public int PayrollValidationErrors { get; set; }
        public int Total => MissingDtrLogs + MissingSchedules + MissingTimeCards + PayrollValidationErrors;
    }

    public class DashboardUpcomingItem
    {
        public string Kind { get; set; } = ""; // Holiday | CutOff | Release
        public string Title { get; set; } = "";
        public DateOnly Date { get; set; }
        public string? Subtitle { get; set; }
        public string IconClass { get; set; } = "fas fa-calendar";
        public string AccentClass { get; set; } = "ops-accent-blue";
    }

    public class DashboardActivityItem
    {
        public string Title { get; set; } = "";
        public string? Detail { get; set; }
        public DateTime OccurredOn { get; set; }
        public string IconClass { get; set; } = "fas fa-circle";
        public string AccentClass { get; set; } = "ops-accent-slate";
        public string? Url { get; set; }
    }

    public class DashboardRecentPayrollItem
    {
        public int Id { get; set; }
        public int PayrollPeriodId { get; set; }
        public string Name { get; set; } = "";
        public string PeriodName { get; set; } = "";
        public string Status { get; set; } = "";
        public int TotalEmployees { get; set; }
        public decimal TotalGrossPay { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal TotalBenefits { get; set; }
        public decimal TotalNetPay { get; set; }
        public DateTime? ProcessedDate { get; set; }
        public DateTime? LastUpdated { get; set; }
    }

    public class PayrollPeriodSummary
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public DateOnly PeriodStartDate { get; set; }
        public DateOnly PeriodEndDate { get; set; }
        public DateOnly PayDate { get; set; }
    }
}
