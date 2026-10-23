using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Services;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _db;
        private readonly IPayrollTimeCardFromDtrService _timeCardFromDtrService;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext db,
            IPayrollTimeCardFromDtrService timeCardFromDtrService,
            IApprovalWorkflowService workflowService,
            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _db = db;
            _timeCardFromDtrService = timeCardFromDtrService;
            _workflowService = workflowService;
            _userManager = userManager;
        }

        [Permission("DASHBOARD")]
        public async Task<IActionResult> Index(int? periodId = null)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var vm = new DashboardViewModel();

            await PopulateAudienceAsync(vm);
            await PopulatePeriodContextAsync(vm, periodId, today);
            await PopulateGlobalCountsAsync(vm);
            await PopulatePeriodPayrollTotalsAsync(vm);
            await PopulateRecentRunsAsync(vm);

            if (vm.Audience.ShowPendingApprovals)
                await PopulatePendingApprovalsAsync(vm);

            if (vm.Audience.ShowPayrollProgress)
                await PopulatePayrollProgressAsync(vm);

            if (vm.Audience.ShowOvertimeSummary)
                await PopulateOvertimeSummaryAsync(vm);

            if (vm.Audience.ShowAttendanceSummary || vm.Audience.ShowAttention)
                await PopulateAttendanceAndAttentionAsync(vm, today);

            if (vm.Audience.ShowUpcoming)
                await PopulateUpcomingEventsAsync(vm, today);

            if (vm.Audience.ShowRecentActivity)
                await PopulateRecentActivitiesAsync(vm);

            if (vm.Audience.ShowCharts)
                await PopulateChartsAsync(vm, today);

            return View(vm);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private async Task PopulateAudienceAsync(DashboardViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = user != null
                ? await _userManager.GetRolesAsync(user)
                : Array.Empty<string>();
            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);

            var isAdmin = PermissionHelper.IsPrivilegedUser(User) || access.IsAdmin;
            var isPayroll = isAdmin
                || roles.Contains("Payroll Staff", StringComparer.OrdinalIgnoreCase)
                || PermissionHelper.HasPermission(User, "PAYROLL_PERIOD_VIEW")
                || PermissionHelper.HasPermission(User, "PAYROLL_VIEW");
            var isHr = isAdmin
                || roles.Contains("HR Staff", StringComparer.OrdinalIgnoreCase)
                || PermissionHelper.HasPermission(User, "DTR_VIEW")
                || PermissionHelper.HasPermission(User, "EMPLOYEE_VIEW");
            var isDeptHead = access.LeadershipHeadOrgUnitIds.Count > 0
                || access.ApproveOrgUnitIds.Count > 0;

            vm.Audience = new DashboardAudience
            {
                IsAdmin = isAdmin,
                IsPayrollOfficer = isPayroll && !isAdmin,
                IsHr = isHr && !isAdmin && !isPayroll,
                IsDepartmentHead = isDeptHead && !isAdmin && !isPayroll && !isHr,
                ShowPayrollSummary = true,
                ShowPayrollProgress = true,
                ShowPendingApprovals = true,
                ShowOvertimeSummary = true,
                ShowAttendanceSummary = true,
                ShowAttention = true,
                ShowUpcoming = true,
                ShowRecentActivity = true,
                ShowRecentRuns = true,
                ShowCharts = true
            };

            if (isAdmin)
                return;

            if (isPayroll && !isHr)
            {
                // Payroll officer focus
                vm.Audience.ShowPayrollSummary = true;
                vm.Audience.ShowPayrollProgress = true;
                vm.Audience.ShowOvertimeSummary = true;
                vm.Audience.ShowAttention = true;
                vm.Audience.ShowRecentRuns = true;
                vm.Audience.ShowAttendanceSummary = false;
                vm.Audience.ShowPendingApprovals = true;
            }
            else if (isHr && !isPayroll)
            {
                // HR focus
                vm.Audience.ShowPayrollSummary = false;
                vm.Audience.ShowPayrollProgress = false;
                vm.Audience.ShowRecentRuns = false;
                vm.Audience.ShowAttendanceSummary = true;
                vm.Audience.ShowPendingApprovals = true;
                vm.Audience.ShowOvertimeSummary = true;
                vm.Audience.ShowAttention = true;
            }
            else if (isDeptHead && !isHr && !isPayroll)
            {
                // Department head — approvals + team attendance
                vm.Audience.ShowPayrollSummary = false;
                vm.Audience.ShowPayrollProgress = false;
                vm.Audience.ShowRecentRuns = false;
                vm.Audience.ShowOvertimeSummary = true;
                vm.Audience.ShowAttendanceSummary = true;
                vm.Audience.ShowPendingApprovals = true;
                vm.Audience.ShowAttention = false;
                vm.Audience.ShowRecentActivity = false;
            }
        }

        private async Task PopulatePeriodContextAsync(DashboardViewModel vm, int? periodId, DateOnly today)
        {
            var periods = await _db.PayrollPeriods
                .AsNoTracking()
                .Where(pp => !pp.IsDeleted)
                .OrderByDescending(pp => pp.PeriodStartDate)
                .Take(24)
                .Select(pp => new DashboardPeriodOption
                {
                    Id = pp.Id,
                    Name = pp.Name,
                    IsLocked = pp.IsLocked,
                    PeriodStartDate = pp.PeriodStartDate,
                    PeriodEndDate = pp.PeriodEndDate,
                    PayDate = pp.PayDate
                })
                .ToListAsync();

            vm.PeriodOptions = periods;

            PayrollPeriod? selected = null;
            if (periodId.HasValue)
            {
                selected = await _db.PayrollPeriods
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pp => pp.Id == periodId.Value && !pp.IsDeleted);
            }

            if (selected == null)
            {
                selected = await _db.PayrollPeriods
                    .AsNoTracking()
                    .Where(pp => !pp.IsDeleted && !pp.IsLocked
                                 && pp.PeriodStartDate <= today && pp.PeriodEndDate >= today)
                    .OrderBy(pp => pp.PayDate)
                    .FirstOrDefaultAsync();
            }

            if (selected == null)
            {
                selected = await _db.PayrollPeriods
                    .AsNoTracking()
                    .Where(pp => !pp.IsDeleted && !pp.IsLocked && pp.PayDate >= today)
                    .OrderBy(pp => pp.PayDate)
                    .FirstOrDefaultAsync();
            }

            if (selected == null)
            {
                selected = await _db.PayrollPeriods
                    .AsNoTracking()
                    .Where(pp => !pp.IsDeleted)
                    .OrderByDescending(pp => pp.PeriodStartDate)
                    .FirstOrDefaultAsync();
            }

            if (selected == null)
                return;

            vm.SelectedPeriodId = selected.Id;
            vm.SelectedPeriodName = selected.Name;
            vm.SelectedPeriodIsLocked = selected.IsLocked;
            vm.SelectedPeriodStart = selected.PeriodStartDate;
            vm.SelectedPeriodEnd = selected.PeriodEndDate;
            vm.SelectedPeriodPayDate = selected.PayDate;
        }

        private async Task PopulateGlobalCountsAsync(DashboardViewModel vm)
        {
            vm.TotalEmployees = await _db.Employees.CountAsync(e => !e.IsDeleted);
            vm.ActiveEmployees = await _db.Employees.CountAsync(e => !e.IsDeleted && e.IsActive);
            vm.PayrollPeriodsCount = await _db.PayrollPeriods.CountAsync(pp => !pp.IsDeleted);
            vm.ActivePayrollPeriodsCount = await _db.PayrollPeriods
                .CountAsync(pp => !pp.IsDeleted && pp.IsActive && !pp.IsLocked);
            vm.CompletedPayrollRunsCount = await _db.PayrollRuns
                .CountAsync(pr => !pr.IsDeleted && pr.Status == PayrollStatus.Completed);
            vm.DraftPayrollRunsCount = await _db.PayrollRuns
                .CountAsync(pr => !pr.IsDeleted && pr.Status == PayrollStatus.Draft);

            vm.TaxTablesCount = await _db.TaxTables.CountAsync(t => !t.IsDeleted && t.IsActive);
            vm.GovernmentContributionTypesCount = await _db.GovernmentContributionTypes
                .CountAsync(g => !g.IsDeleted && g.IsActive);
            vm.GovernmentContributionTablesCount = await _db.GovernmentContributionTables
                .CountAsync(g => !g.IsDeleted && g.IsActive);
            vm.PayrollConfigurationsCount = await _db.PayrollConfigurations
                .CountAsync(pc => !pc.IsDeleted && pc.IsActive);
        }

        private async Task PopulatePeriodPayrollTotalsAsync(DashboardViewModel vm)
        {
            if (!vm.SelectedPeriodId.HasValue)
                return;

            var run = await _db.PayrollRuns
                .AsNoTracking()
                .Where(pr => !pr.IsDeleted
                             && pr.PayrollPeriodId == vm.SelectedPeriodId.Value
                             && pr.Status == PayrollStatus.Completed)
                .OrderByDescending(pr => pr.IsFinalized)
                .ThenByDescending(pr => pr.ProcessedDate ?? pr.CreatedOn)
                .FirstOrDefaultAsync();

            if (run == null)
                return;

            vm.HasPeriodPayrollTotals = true;
            vm.PeriodPayrollRunName = run.Name;
            vm.PeriodPayrollProcessedDate = run.ProcessedDate ?? run.CreatedOn;
            vm.PeriodGrossPay = run.TotalGrossPay;
            vm.PeriodTotalDeductions = run.TotalDeductions;
            vm.PeriodTotalBenefits = run.TotalBenefits;
            vm.PeriodTotalNetPay = run.TotalNetPay;
            vm.PeriodPayrollEmployeeCount = run.TotalEmployees;
        }

        private async Task PopulateRecentRunsAsync(DashboardViewModel vm)
        {
            var recentCompleted = await _db.PayrollRuns
                .AsNoTracking()
                .Where(pr => !pr.IsDeleted && pr.Status == PayrollStatus.Completed)
                .Include(pr => pr.PayrollPeriod)
                .OrderByDescending(pr => pr.ProcessedDate ?? pr.CreatedOn)
                .Take(3)
                .ToListAsync();

            vm.RecentPayrollRuns = recentCompleted.Select(MapRunItem).ToList();

            var drafts = await _db.PayrollRuns
                .AsNoTracking()
                .Where(pr => !pr.IsDeleted && pr.Status == PayrollStatus.Draft)
                .Include(pr => pr.PayrollPeriod)
                .OrderByDescending(pr => pr.CreatedOn)
                .Take(3)
                .ToListAsync();

            vm.DraftPayrollRuns = drafts.Select(MapRunItem).ToList();
        }

        private static DashboardRecentPayrollItem MapRunItem(PayrollRun pr) => new()
        {
            Id = pr.Id,
            PayrollPeriodId = pr.PayrollPeriodId,
            Name = pr.Name,
            PeriodName = pr.PayrollPeriod?.Name ?? string.Empty,
            Status = pr.Status.ToString(),
            TotalEmployees = pr.TotalEmployees,
            TotalGrossPay = pr.TotalGrossPay,
            TotalDeductions = pr.TotalDeductions,
            TotalBenefits = pr.TotalBenefits,
            TotalNetPay = pr.TotalNetPay,
            ProcessedDate = pr.ProcessedDate,
            LastUpdated = pr.ProcessedDate ?? pr.UpdatedOn ?? pr.CreatedOn
        };

        private async Task PopulatePendingApprovalsAsync(DashboardViewModel vm)
        {
            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            if (employee == null)
                return;

            vm.PendingApprovals = new DashboardPendingApprovals
            {
                Overtime = await _workflowService.GetPendingCountForEmployeeAsync(
                    employee.Id, WorkflowProcessType.Overtime),
                DtrJustifications = await _workflowService.GetPendingCountForEmployeeAsync(
                    employee.Id, WorkflowProcessType.TimeLogJustification),
                ScheduleChanges = await _workflowService.GetPendingCountForEmployeeAsync(
                    employee.Id, WorkflowProcessType.ScheduleChange),
                WorkSchedules = await _workflowService.GetPendingCountForEmployeeAsync(
                    employee.Id, WorkflowProcessType.WorkSchedule),
                Leave = await _workflowService.GetPendingCountForEmployeeAsync(
                    employee.Id, WorkflowProcessType.Leave)
            };
        }

        private async Task PopulatePayrollProgressAsync(DashboardViewModel vm)
        {
            if (!vm.SelectedPeriodId.HasValue)
                return;

            var periodId = vm.SelectedPeriodId.Value;
            var total = await _db.PayrollPeriodEmployees
                .CountAsync(ppe => ppe.PayrollPeriodId == periodId);

            var withCards = await _db.PayrollPeriodTimeCards
                .AsNoTracking()
                .Where(tc => !tc.IsDeleted && tc.PayrollPeriodId == periodId)
                .Select(tc => tc.EmployeeId)
                .Distinct()
                .CountAsync();

            vm.PayrollProgress = new DashboardPayrollProgress
            {
                TotalEmployees = total,
                Processed = withCards,
                IsLocked = vm.SelectedPeriodIsLocked
            };
        }

        private async Task PopulateOvertimeSummaryAsync(DashboardViewModel vm)
        {
            if (!vm.SelectedPeriodStart.HasValue || !vm.SelectedPeriodEnd.HasValue)
            {
                // Fall back to all-time status counts when no period
                vm.OvertimeSummary = new DashboardOvertimeSummary
                {
                    PendingApproval = await _db.OvertimeRequests
                        .CountAsync(r => !r.IsDeleted && r.Status == OvertimeStatus.Pending),
                    ApprovedPendingImport = await _db.OvertimeRequests
                        .CountAsync(r => !r.IsDeleted && r.Status == OvertimeStatus.Approved && !r.IsProcessedToPayroll),
                    Imported = await _db.OvertimeRequests
                        .CountAsync(r => !r.IsDeleted && r.IsProcessedToPayroll),
                    Rejected = await _db.OvertimeRequests
                        .CountAsync(r => !r.IsDeleted && r.Status == OvertimeStatus.Rejected)
                };
                return;
            }

            var start = vm.SelectedPeriodStart.Value;
            var end = vm.SelectedPeriodEnd.Value;

            var q = _db.OvertimeRequests.AsNoTracking()
                .Where(r => !r.IsDeleted && r.StartDate <= end && r.EndDate >= start);

            vm.OvertimeSummary = new DashboardOvertimeSummary
            {
                PendingApproval = await q.CountAsync(r => r.Status == OvertimeStatus.Pending),
                ApprovedPendingImport = await q.CountAsync(r =>
                    r.Status == OvertimeStatus.Approved && !r.IsProcessedToPayroll),
                Imported = await q.CountAsync(r => r.IsProcessedToPayroll),
                Rejected = await q.CountAsync(r => r.Status == OvertimeStatus.Rejected)
            };
        }

        private async Task PopulateAttendanceAndAttentionAsync(DashboardViewModel vm, DateOnly today)
        {
            if (!vm.SelectedPeriodStart.HasValue || !vm.SelectedPeriodEnd.HasValue)
                return;

            var start = vm.SelectedPeriodStart.Value;
            var end = vm.SelectedPeriodEnd.Value;
            var attendanceEnd = end < today ? end : today;

            vm.AttendanceSummary.PeriodLabel = vm.SelectedPeriodName;

            var activeEmployeeIds = await _db.Employees
                .AsNoTracking()
                .Where(e => !e.IsDeleted && e.IsActive)
                .Select(e => e.Id)
                .ToListAsync();

            if (activeEmployeeIds.Count == 0)
                return;

            // Scope to period assignees when available
            List<Guid> scopeIds = activeEmployeeIds;
            if (vm.SelectedPeriodId.HasValue)
            {
                var assigned = await _db.PayrollPeriodEmployees
                    .AsNoTracking()
                    .Where(ppe => ppe.PayrollPeriodId == vm.SelectedPeriodId.Value)
                    .Select(ppe => ppe.EmployeeId)
                    .ToListAsync();
                if (assigned.Count > 0)
                    scopeIds = assigned;
            }

            if (vm.Audience.ShowAttendanceSummary && attendanceEnd >= start)
            {
                var trend = await _timeCardFromDtrService.BuildDailyAttendanceTrendAsync(
                    scopeIds, start, attendanceEnd);

                vm.AttendanceSummary.Present = trend.Sum(t => t.Present);
                vm.AttendanceSummary.Late = trend.Sum(t => t.Late);
                vm.AttendanceSummary.Absent = trend.Sum(t => t.Absent);
                vm.AttendanceSummary.OnLeave = trend.Sum(t => t.Leave);

                vm.Charts.AttendanceTrend = new DashboardAttendanceTrendChart
                {
                    Labels = trend.Select(t => t.Date.ToString("MMM d")).ToList(),
                    DateKeys = trend.Select(t => t.Date.ToString("yyyy-MM-dd")).ToList(),
                    Present = trend.Select(t => t.Present).ToList(),
                    Late = trend.Select(t => t.Late).ToList(),
                    Absent = trend.Select(t => t.Absent).ToList(),
                    Leave = trend.Select(t => t.Leave).ToList(),
                    DtrUrl = Url.Action("Index", "Dtr") ?? "/Dtr"
                };

                // Rest / day-off days from schedules in the period
                var months = Enumerable.Range(0, (end.Year - start.Year) * 12 + end.Month - start.Month + 1)
                    .Select(i => start.AddMonths(i))
                    .Select(d => new { d.Year, d.Month })
                    .Distinct()
                    .ToList();

                var rest = 0;
                foreach (var m in months)
                {
                    rest += await _db.ScheduleEntries
                        .AsNoTracking()
                        .Where(se => scopeIds.Contains(se.EmployeeId)
                                     && se.WorkSchedule.Year == m.Year
                                     && se.WorkSchedule.Month == m.Month
                                     && se.Day >= 1
                                     && se.ShiftType.IsLeave
                                     && se.ShiftType.Hours <= 0)
                        .CountAsync();
                }

                vm.AttendanceSummary.RestDay = rest;

                if (vm.Audience.ShowAttention)
                {
                    // Approximate: employees with any absence in the period window
                    var timeCards = await _timeCardFromDtrService.BuildForEmployeesAsync(
                        scopeIds, start, attendanceEnd);
                    vm.Attention.MissingDtrLogs = timeCards.Count(tc =>
                        tc.AbsentDays > 0 || (tc.PresentDays == 0 && tc.LeaveDays == 0));
                }
            }

            if (!vm.Audience.ShowAttention || !vm.SelectedPeriodId.HasValue)
                return;

            var periodId = vm.SelectedPeriodId.Value;
            var periodMonths = new HashSet<(int Year, int Month)>();
            for (var d = start; d <= end; d = d.AddDays(1))
                periodMonths.Add((d.Year, d.Month));

            var scheduledEmployeeIds = new HashSet<Guid>();
            foreach (var m in periodMonths)
            {
                var ids = await _db.ScheduleEntries
                    .AsNoTracking()
                    .Where(se => se.WorkSchedule.Year == m.Year
                                 && se.WorkSchedule.Month == m.Month
                                 && se.WorkSchedule.Status == WorkScheduleStatus.Approved
                                 && scopeIds.Contains(se.EmployeeId))
                    .Select(se => se.EmployeeId)
                    .Distinct()
                    .ToListAsync();
                foreach (var id in ids) scheduledEmployeeIds.Add(id);
            }

            vm.Attention.MissingSchedules = scopeIds.Count(id => !scheduledEmployeeIds.Contains(id));

            var withCards = await _db.PayrollPeriodTimeCards
                .AsNoTracking()
                .Where(tc => !tc.IsDeleted && tc.PayrollPeriodId == periodId)
                .Select(tc => tc.EmployeeId)
                .Distinct()
                .ToListAsync();
            var cardSet = withCards.ToHashSet();
            vm.Attention.MissingTimeCards = scopeIds.Count(id => !cardSet.Contains(id));

            // Soft validation: draft runs with zero employees or zero net
            vm.Attention.PayrollValidationErrors = await _db.PayrollRuns
                .AsNoTracking()
                .CountAsync(pr => !pr.IsDeleted
                                  && pr.PayrollPeriodId == periodId
                                  && pr.Status == PayrollStatus.Draft
                                  && (pr.TotalEmployees <= 0 || pr.TotalNetPay < 0));
        }

        private async Task PopulateChartsAsync(DashboardViewModel vm, DateOnly today)
        {
            // Attendance trend may already be filled when attendance summary ran
            if (vm.Charts.AttendanceTrend.Labels.Count == 0
                && vm.SelectedPeriodStart.HasValue
                && vm.SelectedPeriodEnd.HasValue)
            {
                var start = vm.SelectedPeriodStart.Value;
                var end = vm.SelectedPeriodEnd.Value < today ? vm.SelectedPeriodEnd.Value : today;
                if (end >= start)
                {
                    var scopeIds = await ResolveScopeEmployeeIdsAsync(vm);
                    if (scopeIds.Count > 0)
                    {
                        var trend = await _timeCardFromDtrService.BuildDailyAttendanceTrendAsync(
                            scopeIds, start, end);
                        vm.Charts.AttendanceTrend = new DashboardAttendanceTrendChart
                        {
                            Labels = trend.Select(t => t.Date.ToString("MMM d")).ToList(),
                            DateKeys = trend.Select(t => t.Date.ToString("yyyy-MM-dd")).ToList(),
                            Present = trend.Select(t => t.Present).ToList(),
                            Late = trend.Select(t => t.Late).ToList(),
                            Absent = trend.Select(t => t.Absent).ToList(),
                            Leave = trend.Select(t => t.Leave).ToList(),
                            DtrUrl = Url.Action("Index", "Dtr") ?? "/Dtr"
                        };
                    }
                }
            }

            // Payroll cost — last 6 completed runs (oldest → newest for left-to-right reading)
            var runs = await _db.PayrollRuns
                .AsNoTracking()
                .Include(r => r.PayrollPeriod)
                .Where(r => !r.IsDeleted && r.Status == PayrollStatus.Completed)
                .OrderByDescending(r => r.ProcessedDate ?? r.CreatedOn)
                .Take(6)
                .ToListAsync();

            runs.Reverse();
            var runIds = runs.Select(r => r.Id).ToList();

            var entryTotals = await _db.PayrollEntries
                .AsNoTracking()
                .Where(e => !e.IsDeleted && runIds.Contains(e.PayrollRunId))
                .GroupBy(e => e.PayrollRunId)
                .Select(g => new
                {
                    PayrollRunId = g.Key,
                    Basic = g.Sum(x => x.RegularPay + x.HolidayPay + x.RestDayPay + x.NightDifferentialPay),
                    OvertimePay = g.Sum(x => x.OvertimePay),
                    Benefits = g.Sum(x => x.TotalBenefits),
                    Deductions = g.Sum(x => x.TotalDeductions)
                })
                .ToListAsync();

            var byRun = entryTotals.ToDictionary(x => x.PayrollRunId);

            var cost = new DashboardPayrollCostChart
            {
                PayrollUrl = Url.Action("Index", "Payroll") ?? "/Payroll"
            };

            foreach (var run in runs)
            {
                byRun.TryGetValue(run.Id, out var tot);
                cost.Labels.Add(run.PayrollPeriod?.Name ?? run.Name);
                cost.BasicPay.Add(tot?.Basic ?? 0);
                cost.OvertimePay.Add(tot?.OvertimePay ?? 0);
                cost.Benefits.Add(tot?.Benefits ?? run.TotalBenefits);
                cost.Deductions.Add(tot?.Deductions ?? run.TotalDeductions);
                cost.PayrollRunIds.Add(run.Id);
                cost.PayrollPeriodIds.Add(run.PayrollPeriodId);
            }

            // Fallback when no entries: use run gross as basic
            if (runs.Count > 0 && cost.BasicPay.All(v => v == 0) && cost.OvertimePay.All(v => v == 0))
            {
                for (var i = 0; i < runs.Count; i++)
                {
                    cost.BasicPay[i] = runs[i].TotalGrossPay;
                    cost.Benefits[i] = runs[i].TotalBenefits;
                    cost.Deductions[i] = runs[i].TotalDeductions;
                }
            }

            vm.Charts.PayrollCost = cost;

            // Employee status pie — active only, by EmploymentBasis
            var statusGroups = await _db.Employees
                .AsNoTracking()
                .Where(e => !e.IsDeleted && e.IsActive)
                .GroupBy(e => e.EmploymentBasis)
                .Select(g => new { Basis = g.Key, Count = g.Count() })
                .OrderBy(g => g.Basis)
                .ToListAsync();

            vm.Charts.EmployeeStatus = new DashboardEmployeeStatusChart
            {
                Labels = statusGroups.Select(g => g.Basis.ToString()).ToList(),
                Counts = statusGroups.Select(g => g.Count).ToList(),
                EmployeeUrl = Url.Action("Index", "Employee") ?? "/Employee"
            };
        }

        private async Task<List<Guid>> ResolveScopeEmployeeIdsAsync(DashboardViewModel vm)
        {
            var active = await _db.Employees
                .AsNoTracking()
                .Where(e => !e.IsDeleted && e.IsActive)
                .Select(e => e.Id)
                .ToListAsync();

            if (!vm.SelectedPeriodId.HasValue)
                return active;

            var assigned = await _db.PayrollPeriodEmployees
                .AsNoTracking()
                .Where(ppe => ppe.PayrollPeriodId == vm.SelectedPeriodId.Value)
                .Select(ppe => ppe.EmployeeId)
                .ToListAsync();

            return assigned.Count > 0 ? assigned : active;
        }

        private async Task PopulateUpcomingEventsAsync(DashboardViewModel vm, DateOnly today)
        {
            var items = new List<DashboardUpcomingItem>();

            var holidays = await _db.Holidays
                .AsNoTracking()
                .Where(h => h.IsActive && !h.IsDeleted)
                .Where(h => h.IsRecurring || h.Date >= today)
                .OrderBy(h => h.Date.Month).ThenBy(h => h.Date.Day)
                .Take(8)
                .ToListAsync();

            foreach (var h in holidays)
            {
                var date = h.IsRecurring
                    ? SafeDate(today.Year, h.Date.Month, h.Date.Day)
                      ?? SafeDate(today.Year + 1, h.Date.Month, h.Date.Day)
                    : h.Date;
                if (!date.HasValue || date.Value < today)
                {
                    if (h.IsRecurring)
                        date = SafeDate(today.Year + 1, h.Date.Month, h.Date.Day);
                }

                if (!date.HasValue || date.Value < today)
                    continue;

                items.Add(new DashboardUpcomingItem
                {
                    Kind = "Holiday",
                    Title = h.Name,
                    Date = date.Value,
                    Subtitle = h.IsRecurring ? "Recurring holiday" : "Holiday",
                    IconClass = "fas fa-star",
                    AccentClass = "ops-accent-green"
                });
            }

            if (vm.SelectedPeriodEnd.HasValue && vm.SelectedPeriodEnd.Value >= today)
            {
                items.Add(new DashboardUpcomingItem
                {
                    Kind = "CutOff",
                    Title = "Payroll cut-off",
                    Date = vm.SelectedPeriodEnd.Value,
                    Subtitle = vm.SelectedPeriodName,
                    IconClass = "fas fa-cut",
                    AccentClass = "ops-accent-orange"
                });
            }

            if (vm.SelectedPeriodPayDate.HasValue && vm.SelectedPeriodPayDate.Value >= today)
            {
                items.Add(new DashboardUpcomingItem
                {
                    Kind = "Release",
                    Title = "Payroll release / pay date",
                    Date = vm.SelectedPeriodPayDate.Value,
                    Subtitle = vm.SelectedPeriodName,
                    IconClass = "fas fa-money-check-alt",
                    AccentClass = "ops-accent-blue"
                });
            }

            vm.UpcomingEvents = items
                .OrderBy(i => i.Date)
                .Take(5)
                .ToList();
        }

        private async Task PopulateRecentActivitiesAsync(DashboardViewModel vm)
        {
            var activities = new List<DashboardActivityItem>();

            var recentRuns = await _db.PayrollRuns
                .AsNoTracking()
                .Include(r => r.PayrollPeriod)
                .Where(r => !r.IsDeleted && r.Status == PayrollStatus.Completed)
                .OrderByDescending(r => r.ProcessedDate ?? r.CreatedOn)
                .Take(3)
                .ToListAsync();

            foreach (var r in recentRuns)
            {
                activities.Add(new DashboardActivityItem
                {
                    Title = "Payroll generated",
                    Detail = $"{r.Name} · {r.PayrollPeriod?.Name}",
                    OccurredOn = r.ProcessedDate ?? r.CreatedOn ?? DateTime.Now,
                    IconClass = "fas fa-file-invoice-dollar",
                    AccentClass = "ops-accent-blue",
                    Url = Url.Action("Index", "Payroll")
                });
            }

            var otApproved = await _db.OvertimeRequests
                .AsNoTracking()
                .Include(r => r.Employee)
                .Where(r => !r.IsDeleted && r.Status == OvertimeStatus.Approved)
                .OrderByDescending(r => r.ReviewedOn ?? r.SubmittedOn)
                .Take(3)
                .ToListAsync();

            foreach (var r in otApproved)
            {
                var name = r.Employee != null
                    ? $"{r.Employee.Surname}, {r.Employee.FirstName}"
                    : "Employee";
                activities.Add(new DashboardActivityItem
                {
                    Title = "OT approved",
                    Detail = $"{name} · {OvertimeRequest.FormatDateRange(r.StartDate, r.EndDate)}",
                    OccurredOn = r.ReviewedOn ?? r.SubmittedOn,
                    IconClass = "fas fa-business-time",
                    AccentClass = "ops-accent-green",
                    Url = Url.Action("Index", "OvertimeRequest")
                });
            }

            var employees = await _db.Employees
                .AsNoTracking()
                .Where(e => !e.IsDeleted)
                .OrderByDescending(e => e.CreatedOn)
                .Take(2)
                .ToListAsync();

            foreach (var e in employees)
            {
                activities.Add(new DashboardActivityItem
                {
                    Title = "Employee added",
                    Detail = $"{e.Surname}, {e.FirstName}",
                    OccurredOn = e.CreatedOn ?? DateTime.Now,
                    IconClass = "fas fa-user-plus",
                    AccentClass = "ops-accent-purple",
                    Url = Url.Action("Index", "Employee")
                });
            }

            var holidays = await _db.Holidays
                .AsNoTracking()
                .Where(h => !h.IsDeleted)
                .OrderByDescending(h => h.CreatedOn)
                .Take(2)
                .ToListAsync();

            foreach (var h in holidays)
            {
                activities.Add(new DashboardActivityItem
                {
                    Title = "Holiday created",
                    Detail = $"{h.Name} · {h.Date:MMM d}",
                    OccurredOn = h.CreatedOn ?? DateTime.Now,
                    IconClass = "fas fa-calendar-day",
                    AccentClass = "ops-accent-orange",
                    Url = Url.Action("Index", "Holiday")
                });
            }

            // Leave approved — no leave module yet; skip

            vm.RecentActivities = activities
                .OrderByDescending(a => a.OccurredOn)
                .Take(5)
                .ToList();
        }

        private static DateOnly? SafeDate(int year, int month, int day)
        {
            try { return new DateOnly(year, month, day); }
            catch { return null; }
        }
    }
}
