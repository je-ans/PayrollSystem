using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Services;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    [Authorize]
    public class DtrController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<DtrController> _logger;
        private readonly TimeEntryProcessingService _processingService;
        private readonly DtrAttendanceCalculationService _attendanceCalculation;
        private readonly IAttendanceComputationService _attendanceComputation;
        private readonly IOvertimeEngineService _overtimeEngine;
        private readonly IWorkingCalendarService _workingCalendar;
        private readonly ITimeLogJustificationService _justificationService;
        private readonly IScheduleChangeRequestService _scheduleChangeService;

        public DtrController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILogger<DtrController> logger,
            TimeEntryProcessingService processingService,
            DtrAttendanceCalculationService attendanceCalculation,
            IAttendanceComputationService attendanceComputation,
            IOvertimeEngineService overtimeEngine,
            IWorkingCalendarService workingCalendar,
            ITimeLogJustificationService justificationService,
            IScheduleChangeRequestService scheduleChangeService)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
            _processingService = processingService;
            _attendanceCalculation = attendanceCalculation;
            _attendanceComputation = attendanceComputation;
            _overtimeEngine = overtimeEngine;
            _workingCalendar = workingCalendar;
            _justificationService = justificationService;
            _scheduleChangeService = scheduleChangeService;
        }

        /// DTR index - filter form and document preview.
        /// Admin: select employee, month, year.
        /// Employee: select month, year only (own DTR).
        public async Task<IActionResult> Index(int? year = null, int? month = null)
        {
            var isEmployeeOnly = await IsEmployeeOnlyUserAsync();
            ViewBag.IsEmployeeOnly = isEmployeeOnly;

            if (!isEmployeeOnly && !PermissionHelper.HasPermission(User, "DTR_VIEW"))
                return RedirectToAction("AccessDenied", "Account");

            if (!isEmployeeOnly)
            {
                var employees = await _db.Employees
                    .Where(e => !e.IsDeleted && e.IsActive)
                    .Select(e => new { e.Id, e.Surname, e.FirstName, e.MiddleName, e.EmployeeIdNumber })
                    .AsNoTracking()
                    .ToListAsync();

                ViewBag.Employees = employees
                    .Select(e => new
                    {
                        Id = e.Id,
                        Name = $"{e.Surname}, {e.FirstName} {e.MiddleName}".Trim() + $" ({e.EmployeeIdNumber})"
                    })
                    .OrderBy(e => e.Name)
                    .ToList();
            }
            else
            {
                var employee = await GetCurrentUserEmployeeAsync();
                if (employee == null)
                {
                    return RedirectToAction("AccessDenied", "Account");
                }
                ViewBag.CurrentEmployeeId = employee.Id;
            }

            ViewBag.CurrentMonth = month is >= 1 and <= 12 ? month.Value : DateTime.Now.Month;
            ViewBag.CurrentYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.Now.Year;

            return View();
        }

        /// Returns the DTR document content (Civil Service Form No. 48) as partial HTML for document preview.
        [HttpGet]
        public async Task<IActionResult> GetDtrDocument(Guid? employeeId, int month, int year)
        {
            if (month < 1 || month > 12 || year < 2000 || year > 2100)
            {
                return BadRequest("Invalid month or year.");
            }

            var isEmployeeOnly = await IsEmployeeOnlyUserAsync();
            Guid targetEmployeeId;

            if (isEmployeeOnly)
            {
                var employee = await GetCurrentUserEmployeeAsync();
                if (employee == null)
                {
                    return Forbid();
                }
                targetEmployeeId = employee.Id;
            }
            else
            {
                if (!employeeId.HasValue)
                {
                    return BadRequest("Employee is required.");
                }
                targetEmployeeId = employeeId.Value;
            }

            var viewModel = await BuildDtrViewModelAsync(targetEmployeeId, month, year);
            if (viewModel == null)
            {
                return NotFound("Employee not found or no data available.");
            }

            return PartialView("Partials/_DtrDocumentContent", viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetDtrManagementTable(Guid? employeeId, int month, int year)
        {
            if (month < 1 || month > 12 || year < 2000 || year > 2100)
            {
                return BadRequest("Invalid month or year.");
            }

            var isEmployeeOnly = await IsEmployeeOnlyUserAsync();
            Guid targetEmployeeId;

            if (isEmployeeOnly)
            {
                var employees = await GetCurrentUserEmployeeAsync();
                if (employees == null)
                {
                    return Forbid();
                }
                targetEmployeeId = employees.Id;
            }
            else
            {
                if (!employeeId.HasValue)
                {
                    return BadRequest("Employee is required.");
                }
                targetEmployeeId = employeeId.Value;
            }

            var rows = await BuildDtrManagementRowsAsync(targetEmployeeId, month, year);

            var employee = await _db.Employees
                .AsNoTracking()
                .Where(e => e.Id == targetEmployeeId)
                .Select(e => new { e.Surname, e.FirstName, e.EmployeeIdNumber })
                .FirstOrDefaultAsync();

            ViewBag.EmployeeId = targetEmployeeId;
            ViewBag.EmployeeName = employee != null
                ? $"{employee.Surname}, {employee.FirstName}"
                : string.Empty;
            ViewBag.EmployeeIdNumber = employee?.EmployeeIdNumber ?? string.Empty;
            ViewBag.Month = month;
            ViewBag.Year = year;
            ViewBag.CanReviewJustifications = !isEmployeeOnly;
            ViewBag.CanReviewScheduleChanges = !isEmployeeOnly;
            ViewBag.CanReviewOvertime = !isEmployeeOnly;

            var approvedSchedule = await EmployeeOrganizationHelper.FindWorkScheduleForEmployeeAsync(
                _db, targetEmployeeId, month, year, approvedOnly: true);
            ViewBag.HasApprovedSchedule = approvedSchedule != null &&
                                          approvedSchedule.Status == WorkScheduleStatus.Approved;

            return PartialView("Partials/_DtrManagementTable", rows);
        }

        [HttpGet]
        public async Task<IActionResult> GetArrangeTimeLogForm(Guid employeeId, DateTime date)
        {
            if (!await CanAccessEmployeeAsync(employeeId))
            {
                return Forbid();
            }

            var targetDate = DateOnly.FromDateTime(date);
            var form = await BuildArrangeTimeLogFormAsync(employeeId, targetDate);
            if (form.Slots.All(s => s.Entry == null) && !form.PoolEntries.Any())
            {
                return Content("<div class=\"alert alert-warning mb-0\">No time logs found for this day.</div>", "text/html");
            }

            return PartialView("Partials/_ArrangeTimeLog", form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveArrangedTimeLog(DtrArrangeTimeLogSubmitViewModel model)
        {
            if (!await CanAccessEmployeeAsync(model.EmployeeId))
            {
                return Forbid();
            }

            // Prefer form values (modal POST) in case model binding misses empty GUID fields.
            model.AmInEntryId = ParseOptionalGuid(Request.Form["AmInEntryId"].FirstOrDefault()) ?? model.AmInEntryId;
            model.AmOutEntryId = ParseOptionalGuid(Request.Form["AmOutEntryId"].FirstOrDefault()) ?? model.AmOutEntryId;
            model.PmInEntryId = ParseOptionalGuid(Request.Form["PmInEntryId"].FirstOrDefault()) ?? model.PmInEntryId;
            model.PmOutEntryId = ParseOptionalGuid(Request.Form["PmOutEntryId"].FirstOrDefault()) ?? model.PmOutEntryId;
            model.PoolEntryIds = Request.Form["PoolEntryIds"]
                .Select(ParseOptionalGuid)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var layout = await GetScheduleLayoutAsync(model.EmployeeId, model.Date);
            var slotTypes = GetArrangeSlotTypes(layout);
            var entryDates = layout == DtrScheduleLayout.Overnight
                ? new[] { model.Date, model.Date.AddDays(1) }
                : new[] { model.Date };

            var entries = await _db.TimeEntries
                .Where(te => te.EmployeeId == model.EmployeeId && entryDates.Contains(te.EntryDate))
                .ToListAsync();

            if (!entries.Any())
            {
                return Json(new { success = false, message = "No time logs found for this day." });
            }

            var slotAssignments = new Dictionary<string, Guid?>
            {
                ["AmIn"] = model.AmInEntryId,
                ["AmOut"] = model.AmOutEntryId,
                ["PmIn"] = model.PmInEntryId,
                ["PmOut"] = model.PmOutEntryId
            };

            foreach (var key in slotAssignments.Keys.Where(k => !slotTypes.ContainsKey(k)).ToList())
                slotAssignments.Remove(key);

            var assignedIds = slotAssignments.Values
                .Where(id => id.HasValue && id.Value != Guid.Empty)
                .Select(id => id!.Value)
                .ToList();

            if (!assignedIds.Any())
            {
                return Json(new { success = false, message = "Assign at least one time log to an AM/PM slot before saving." });
            }

            if (assignedIds.Distinct().Count() != assignedIds.Count)
            {
                return Json(new { success = false, message = "The same time log cannot be assigned to more than one slot." });
            }

            foreach (var id in assignedIds)
            {
                if (!entries.Any(e => e.Id == id))
                {
                    return Json(new { success = false, message = "One or more selected time logs are invalid for this day." });
                }
            }

            var poolIds = model.PoolEntryIds?.Where(id => id != Guid.Empty).Distinct().ToList() ?? new List<Guid>();
            foreach (var id in poolIds)
            {
                if (!entries.Any(e => e.Id == id))
                {
                    return Json(new { success = false, message = "One or more unassigned time logs are invalid for this day." });
                }
            }

            var overlap = assignedIds.Intersect(poolIds).ToList();
            if (overlap.Any())
            {
                return Json(new { success = false, message = "A time log cannot be both assigned and unassigned." });
            }

            var modifiedBy = User.Identity?.Name ?? "Arrange Time Log";
            var now = DateTime.Now;

            foreach (var slot in slotTypes)
            {
                var entryId = slotAssignments[slot.Key];
                if (!entryId.HasValue || entryId.Value == Guid.Empty)
                {
                    continue;
                }

                var targetType = slotTypes[slot.Key];
                var entryIdValue = entryId.Value;

                await _db.TimeEntries
                    .Where(e => e.Id == entryIdValue)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(e => e.EntryType, targetType)
                        .SetProperty(e => e.PairedEntryId, (Guid?)null)
                        .SetProperty(e => e.Status, EntryStatus.Corrected)
                        .SetProperty(e => e.ModifiedBy, modifiedBy)
                        .SetProperty(e => e.ModifiedDate, now)
                        .SetProperty(e => e.ValidationNotes, "Reassigned via Arrange Time Log"));
            }

            foreach (var poolId in poolIds)
            {
                await MarkAsArrangePoolEntryAsync(poolId, modifiedBy, now);
            }

            // Any punch not placed in a DTR slot goes to the pool so it cannot fill AM/PM columns.
            var touchedIds = assignedIds.Concat(poolIds).ToHashSet();
            var orphanCandidates = entries.Where(e => !touchedIds.Contains(e.Id));

            // Overnight arrange loads the next calendar day only for shift-end (morning) punches.
            // Do not pool next-day afternoon/evening logs — they belong to that day's own shift.
            if (layout == DtrScheduleLayout.Overnight)
            {
                orphanCandidates = orphanCandidates.Where(e =>
                    e.EntryDate == model.Date ||
                    (e.EntryDate == model.Date.AddDays(1) && e.EntryTime < ArrangeNoon));
            }

            var orphanIds = orphanCandidates.Select(e => e.Id).ToList();
            foreach (var orphanId in orphanIds)
            {
                await MarkAsArrangePoolEntryAsync(orphanId, modifiedBy, now);
            }

            // Arrange explicitly sets slot types; skip same-day pairing that breaks overnight spans.
            if (layout != DtrScheduleLayout.Overnight)
            {
                await _processingService.ValidateAndPairEntries(model.Date);
            }

            return Json(new { success = true, message = "Time logs arranged successfully." });
        }

        private static readonly TimeOnly ArrangeNoon = new(12, 0);

        private async Task<DtrScheduleLayout> GetScheduleLayoutAsync(Guid employeeId, DateOnly date)
        {
            var shift = await _attendanceCalculation.GetEmployeeShiftTypeForDayAsync(employeeId, date, approvedOnly: false);
            var config = await _attendanceCalculation.GetActiveConfigurationAsync();
            var schedule = _attendanceCalculation.ResolveSchedule(config, shift);
            return DtrPunchResolutionService.GetLayout(schedule);
        }

        private static Dictionary<string, EntryType> GetArrangeSlotTypes(DtrScheduleLayout layout) =>
            layout switch
            {
                DtrScheduleLayout.Overnight => new Dictionary<string, EntryType>
                {
                    ["PmIn"] = EntryType.TimeIn,
                    ["AmOut"] = EntryType.TimeOut
                },
                DtrScheduleLayout.SimpleDay => new Dictionary<string, EntryType>
                {
                    ["AmIn"] = EntryType.TimeIn,
                    ["PmOut"] = EntryType.TimeOut
                },
                _ => new Dictionary<string, EntryType>
                {
                    ["AmIn"] = EntryType.TimeIn,
                    ["AmOut"] = EntryType.LunchStart,
                    ["PmIn"] = EntryType.LunchEnd,
                    ["PmOut"] = EntryType.TimeOut
                }
            };

        private async Task<DtrArrangeTimeLogViewModel> BuildArrangeTimeLogFormAsync(Guid employeeId, DateOnly date)
        {
            var payrollConfig = await _attendanceCalculation.GetActiveConfigurationAsync();
            var shift = await _attendanceCalculation.GetEmployeeShiftTypeForDayAsync(employeeId, date, approvedOnly: false);
            var schedule = _attendanceCalculation.ResolveSchedule(payrollConfig, shift);
            var layout = DtrPunchResolutionService.GetLayout(schedule);

            var dayEntries = await _db.TimeEntries
                .Where(te => te.EmployeeId == employeeId && te.EntryDate == date)
                .OrderBy(te => te.EntryTime)
                .ToListAsync();

            List<TimeEntry> nextDayEntries = new();
            if (layout == DtrScheduleLayout.Overnight)
            {
                nextDayEntries = await _db.TimeEntries
                    .Where(te => te.EmployeeId == employeeId && te.EntryDate == date.AddDays(1))
                    .OrderBy(te => te.EntryTime)
                    .ToListAsync();
            }

            var consumedIds = new HashSet<Guid>();
            var resolved = DtrPunchResolutionService.ResolveShift(schedule, dayEntries, nextDayEntries);

            var arrangeDay = DtrPunchResolutionService.FilterForArrange(dayEntries, consumedIds);
            var arrangeNext = DtrPunchResolutionService.FilterForArrange(nextDayEntries, consumedIds);

            DtrArrangeTimeLogEntryViewModel? ToVm(Guid? id, IReadOnlyList<TimeEntry> source)
            {
                if (!id.HasValue)
                    return null;

                var e = source.FirstOrDefault(x => x.Id == id.Value)
                    ?? dayEntries.Concat(nextDayEntries).FirstOrDefault(x => x.Id == id.Value);
                if (e == null)
                    return null;

                return new DtrArrangeTimeLogEntryViewModel
                {
                    Id = e.Id,
                    TimeDisplay = e.EntryTime.ToString("hh:mm tt") +
                        (e.EntryDate != date ? $" ({e.EntryDate:MMM dd})" : string.Empty),
                    TypeDisplay = EnumHelper.GetDisplayName(e.EntryType),
                    SourceDisplay = EnumHelper.GetDisplayName(e.Source)
                };
            }

            var assignedIds = new HashSet<Guid>();
            if (resolved.TimeInEntryId.HasValue)
                assignedIds.Add(resolved.TimeInEntryId.Value);
            if (resolved.TimeOutEntryId.HasValue)
                assignedIds.Add(resolved.TimeOutEntryId.Value);
            if (resolved.LunchOutEntryId.HasValue)
                assignedIds.Add(resolved.LunchOutEntryId.Value);
            if (resolved.LunchInEntryId.HasValue)
                assignedIds.Add(resolved.LunchInEntryId.Value);

            var allCandidates = arrangeDay.Concat(arrangeNext).ToList();
            var pool = allCandidates
                .Where(e => !assignedIds.Contains(e.Id))
                .Select(e => new DtrArrangeTimeLogEntryViewModel
                {
                    Id = e.Id,
                    TimeDisplay = e.EntryTime.ToString("hh:mm tt") +
                        (e.EntryDate != date ? $" ({e.EntryDate:MMM dd})" : string.Empty),
                    TypeDisplay = EnumHelper.GetDisplayName(e.EntryType),
                    SourceDisplay = EnumHelper.GetDisplayName(e.Source)
                })
                .ToList();

            var slots = layout switch
            {
                DtrScheduleLayout.Overnight => new List<DtrArrangeTimeLogSlotViewModel>
                {
                    new()
                    {
                        Key = "PmIn",
                        Label = "PM Arrival (Shift Start)",
                        Description = $"Evening time in on {date:MMM dd}",
                        Entry = ToVm(resolved.TimeInEntryId, arrangeDay)
                    },
                    new()
                    {
                        Key = "AmOut",
                        Label = "AM Departure (Shift End)",
                        Description = $"Next-day morning time out ({date.AddDays(1):MMM dd})",
                        Entry = ToVm(resolved.TimeOutEntryId, arrangeNext)
                    }
                },
                DtrScheduleLayout.SimpleDay => new List<DtrArrangeTimeLogSlotViewModel>
                {
                    new() { Key = "AmIn", Label = "AM Arrival (Time In)", Description = "First arrival", Entry = ToVm(resolved.TimeInEntryId, arrangeDay) },
                    new() { Key = "PmOut", Label = "PM Departure (Time Out)", Description = "End of day", Entry = ToVm(resolved.TimeOutEntryId, arrangeDay) }
                },
                _ => new List<DtrArrangeTimeLogSlotViewModel>
                {
                    new() { Key = "AmIn", Label = "AM Arrival (Time In)", Description = "First morning arrival", Entry = ToVm(resolved.TimeInEntryId, arrangeDay) },
                    new() { Key = "AmOut", Label = "AM Departure (Lunch Out)", Description = "Leave for lunch", Entry = ToVm(resolved.LunchOutEntryId, arrangeDay) },
                    new() { Key = "PmIn", Label = "PM Arrival (Lunch In)", Description = "Return from lunch", Entry = ToVm(resolved.LunchInEntryId, arrangeDay) },
                    new() { Key = "PmOut", Label = "PM Departure (Time Out)", Description = "End of day", Entry = ToVm(resolved.TimeOutEntryId, arrangeDay) }
                }
            };

            return new DtrArrangeTimeLogViewModel
            {
                EmployeeId = employeeId,
                Date = date,
                DateDisplay = date.ToDateTime(new TimeOnly(0, 0)).ToString("dddd, MMMM dd, yyyy"),
                ArrangeLayout = layout.ToString(),
                Slots = slots,
                PoolEntries = pool
            };
        }

        private async Task<List<TimeEntry>> LoadDayEntriesAsync(Guid employeeId, DateOnly date) =>
            await _db.TimeEntries
                .Where(te => te.EmployeeId == employeeId && te.EntryDate == date)
                .OrderBy(te => te.EntryTime)
                .ToListAsync();

        [HttpGet]
        public async Task<IActionResult> GetJustificationForm(Guid employeeId, DateTime date)
        {
            if (!await CanAccessEmployeeAsync(employeeId))
            {
                return Forbid();
            }

            var targetDate = DateOnly.FromDateTime(date);
            var form = await BuildJustificationFormAsync(employeeId, targetDate);
            return PartialView("Partials/_JustifyTimeLog", form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitJustification(DtrJustificationSubmitViewModel model)
        {
            if (!await CanAccessEmployeeAsync(model.EmployeeId))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(model.Justification))
            {
                return Json(new { success = false, message = "Justification is required." });
            }

            model.AmIn = ParseOptionalTime(Request.Form["AmIn"].FirstOrDefault()) ?? model.AmIn;
            model.LunchOut = ParseOptionalTime(Request.Form["LunchOut"].FirstOrDefault()) ?? model.LunchOut;
            model.LunchIn = ParseOptionalTime(Request.Form["LunchIn"].FirstOrDefault()) ?? model.LunchIn;
            model.PmOut = ParseOptionalTime(Request.Form["PmOut"].FirstOrDefault()) ?? model.PmOut;
            model.PmIn = ParseOptionalTime(Request.Form["PmIn"].FirstOrDefault()) ?? model.PmIn;
            model.AmOut = ParseOptionalTime(Request.Form["AmOut"].FirstOrDefault()) ?? model.AmOut;
            if (DateOnly.TryParse(Request.Form["RelatesToPreviousShiftDate"].FirstOrDefault(), out var prevShiftDate))
                model.RelatesToPreviousShiftDate = prevShiftDate;

            var hasRequestedTime = model.AmIn.HasValue || model.LunchOut.HasValue || model.LunchIn.HasValue ||
                model.PmOut.HasValue || model.PmIn.HasValue || model.AmOut.HasValue;
            if (!hasRequestedTime)
            {
                return Json(new { success = false, message = "Please provide at least one preferred time." });
            }

            var currentEmployee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            var dto = new DtrJustificationSubmitDto
            {
                EmployeeId = model.EmployeeId,
                Date = model.Date,
                AmIn = model.AmIn,
                LunchOut = model.LunchOut,
                LunchIn = model.LunchIn,
                PmOut = model.PmOut,
                PmIn = model.PmIn,
                AmOut = model.AmOut,
                RelatesToPreviousShiftDate = model.RelatesToPreviousShiftDate,
                Justification = model.Justification
            };

            var (success, message) = await _justificationService.SubmitAsync(
                dto,
                User.Identity?.Name ?? "DTR Justification",
                currentEmployee?.Id);

            return Json(new { success, message });
        }

        private async Task<bool> IsEmployeeOnlyUserAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return false;

            var roles = await _userManager.GetRolesAsync(user);
            var isEmployee = roles.Contains("Employee", StringComparer.OrdinalIgnoreCase);
            var isAdmin = roles.Contains("Admin", StringComparer.OrdinalIgnoreCase) ||
                         roles.Any(r => string.Equals(r, "HR", StringComparison.OrdinalIgnoreCase));

            return isEmployee && !isAdmin;
        }

        private async Task<Employee?> GetCurrentUserEmployeeAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            return await _db.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == user.Id && !e.IsDeleted && e.IsActive);
        }

        private async Task<DtrViewModel?> BuildDtrViewModelAsync(Guid employeeId, int month, int year)
        {
            await _scheduleChangeService.SyncApprovedChangesForEmployeeMonthAsync(employeeId, month, year);

            var employee = await _db.Employees
                .Include(e => e.Designations.Where(d => d.EffectiveTo == null))
                    .ThenInclude(d => d.Designation)
                .FirstOrDefaultAsync(e => e.Id == employeeId && !e.IsDeleted);

            if (employee == null) return null;

            var daysInMonth = DateTime.DaysInMonth(year, month);
            var startDate = new DateOnly(year, month, 1);
            var endDate = new DateOnly(year, month, daysInMonth);

            // DTR document is time-entry driven (no Attendance dependency)
            var timeEntries = await _db.TimeEntries
                .Where(te => te.EmployeeId == employeeId &&
                             te.EntryDate >= startDate &&
                             te.EntryDate <= endDate.AddDays(1))
                .OrderBy(te => te.EntryDate)
                .ThenBy(te => te.EntryTime)
                .AsNoTracking()
                .ToListAsync();

            var entriesByDate = timeEntries
                .GroupBy(te => te.EntryDate)
                .ToDictionary(g => g.Key, g => g.ToList());

            var payrollConfig = await _attendanceCalculation.GetActiveConfigurationAsync();
            var shiftsForDisplay = await _attendanceCalculation.GetEmployeeShiftTypesByDayAsync(employeeId, month, year, approvedOnly: false);
            var shiftsForAttendance = await _attendanceCalculation.GetEmployeeShiftTypesByDayAsync(employeeId, month, year, approvedOnly: true);

            var regularHoursStr = "_______________";
            var saturdaysHoursStr = "_______________";
            var primaryShift = shiftsForDisplay.Values
                .FirstOrDefault(s => !s.IsLeave && !s.IsFlexible && s.StartTime.HasValue && s.EndTime.HasValue);
            if (primaryShift != null)
            {
                regularHoursStr = FormatShiftWindow(primaryShift);
            }

            var saturdayShift = shiftsForDisplay
                .Where(kvp => new DateOnly(year, month, kvp.Key).DayOfWeek == DayOfWeek.Saturday)
                .Select(kvp => kvp.Value)
                .FirstOrDefault(s => !s.IsLeave && !s.IsFlexible && s.StartTime.HasValue && s.EndTime.HasValue);
            if (saturdayShift != null)
            {
                saturdaysHoursStr = FormatShiftWindow(saturdayShift);
            }

            var dayRows = new List<DtrDayRowViewModel>();
            int tardinessFrequency = 0;
            int undertimeFrequency = 0;
            int totalTardinessMinutes = 0;
            int totalUndertimeMinutes = 0;
            decimal totalCreditedDays = 0m;

            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateOnly(year, month, day);
                entriesByDate.TryGetValue(date, out var entries);
                entries ??= new List<TimeEntry>();
                var displayEntries = GetDtrDisplayEntries(entries).ToList();

                var hasDisplayLog = displayEntries.Any();

                var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
                shiftsForDisplay.TryGetValue(day, out var displayShift);
                shiftsForAttendance.TryGetValue(day, out var attendanceShift);
                var isScheduledLeave = displayShift != null &&
                                       !ShiftTypeScheduleHelper.IsUnassigned(displayShift) &&
                                       displayShift.IsLeave;
                var metricsShift = attendanceShift ?? displayShift;
                var layoutShift = displayShift ?? metricsShift;
                var scheduleContext = _attendanceCalculation.ResolveSchedule(payrollConfig, layoutShift);
                var assessAttendance = ShouldAssessAttendance(payrollConfig, date, metricsShift, scheduleContext);

                var row = new DtrDayRowViewModel { Day = day };
                ApplyShiftToRow(row, displayShift);

                if (!hasDisplayLog)
                {
                    if (isScheduledLeave)
                    {
                        row.IsDayOff = true;
                        row.SpecialDayLabel = ShiftTypeScheduleHelper.ToDtrSpecialLabel(displayShift!);
                        totalCreditedDays += ShiftTypeScheduleHelper.GetCreditedDays(displayShift);
                    }
                    else if (attendanceShift != null &&
                             !ShiftTypeScheduleHelper.IsUnassigned(attendanceShift) &&
                             !attendanceShift.IsLeave &&
                             assessAttendance)
                    {
                        row.IsDayOff = true;
                        row.SpecialDayLabel = "-- ABSENT --";
                    }
                }
                else if (displayShift?.IsFlexible == true)
                {
                    PopulateFlexibleDtrRow(row, displayEntries, displayShift);
                    var timeIn = GetFirstEntryTime(displayEntries, EntryType.TimeIn);
                    var lunchOut = GetFirstEntryTime(displayEntries, EntryType.LunchStart, EntryType.BreakStart);
                    var lunchIn = GetFirstEntryTime(displayEntries, EntryType.LunchEnd, EntryType.BreakEnd);
                    var timeOut = GetLastEntryTime(displayEntries, EntryType.TimeOut);

                    var attendanceDisplay = ComputeAttendanceDisplay(
                        payrollConfig, date, displayEntries, timeIn, lunchOut, lunchIn, timeOut, metricsShift);
                    ApplyAttendanceColumns(row, attendanceDisplay);

                    if (attendanceDisplay.Metrics != null)
                    {
                        AccumulateAttendanceTotals(
                            attendanceDisplay.Metrics,
                            assessAttendance,
                            ref tardinessFrequency,
                            ref undertimeFrequency,
                            ref totalTardinessMinutes,
                            ref totalUndertimeMinutes);
                    }

                    if (timeIn.HasValue && timeOut.HasValue)
                        totalCreditedDays += GetWorkedDayCredit(metricsShift);
                }
                else
                {
                    entriesByDate.TryGetValue(date.AddDays(1), out var nextDayRaw);
                    var nextDayDisplay = GetDtrDisplayEntries(nextDayRaw ?? new List<TimeEntry>()).ToList();
                    entriesByDate.TryGetValue(date.AddDays(-1), out var prevDayRaw);
                    var prevDayDisplay = GetDtrDisplayEntries(prevDayRaw ?? new List<TimeEntry>()).ToList();
                    shiftsForDisplay.TryGetValue(day - 1, out var prevDisplayShift);
                    shiftsForAttendance.TryGetValue(day - 1, out var prevAttendanceShift);
                    var prevScheduleContext = day > 1
                        ? _attendanceCalculation.ResolveSchedule(payrollConfig, prevDisplayShift)
                        : null;
                    var previousMetricsShift = prevAttendanceShift ?? prevDisplayShift;
                    var currentIsOvernight = DtrPunchResolutionService.IsOvernightSchedule(scheduleContext);
                    var previousWasOvernight = DtrPunchResolutionService.IsOvernightSchedule(prevScheduleContext);
                    var shiftResolved = DtrPunchResolutionService.ResolveShift(
                        scheduleContext, displayEntries, nextDayDisplay);
                    var previousShiftResolved = day > 1
                        ? DtrPunchResolutionService.ResolveShift(prevScheduleContext, prevDayDisplay, displayEntries)
                        : null;
                    var expectsLunch = scheduleContext?.HasLunchBreak == true && !currentIsOvernight;

                    row.ExpectsLunchBreak = expectsLunch;
                    row.AssessAttendance = assessAttendance;
                    row.UsesOvernightLayout = previousWasOvernight || currentIsOvernight;
                    ApplyDocumentRowTimes(
                        row,
                        shiftResolved,
                        previousShiftResolved,
                        previousWasOvernight,
                        currentIsOvernight,
                        displayEntries,
                        nextDayDisplay,
                        expectsLunch);
                    row.IsUserProvided = displayEntries.Concat(nextDayDisplay).Any(IsUserProvidedEntry);

                    var attendanceDisplay = ComputeCalendarAttendanceDisplay(
                        payrollConfig,
                        date,
                        displayEntries,
                        metricsShift,
                        previousMetricsShift,
                        scheduleContext,
                        shiftResolved,
                        previousShiftResolved,
                        currentIsOvernight,
                        previousWasOvernight);
                    ApplyAttendanceColumns(row, attendanceDisplay);

                    if (attendanceDisplay.Metrics != null)
                    {
                        AccumulateAttendanceTotals(
                            attendanceDisplay.Metrics,
                            assessAttendance,
                            ref tardinessFrequency,
                            ref undertimeFrequency,
                            ref totalTardinessMinutes,
                            ref totalUndertimeMinutes);
                    }

                    if (isScheduledLeave)
                    {
                        row.IsDayOff = true;
                        row.SpecialDayLabel = ShiftTypeScheduleHelper.ToDtrSpecialLabel(displayShift!);
                        row.ShowScheduledLeaveWithLogs = HasAnyDocumentTime(row);
                    }

                    if (ShouldCreditWorkedDay(scheduleContext, currentIsOvernight, shiftResolved))
                        totalCreditedDays += GetWorkedDayCredit(metricsShift);

                    if (isScheduledLeave)
                        totalCreditedDays += ShiftTypeScheduleHelper.GetCreditedDays(displayShift);
                }

                dayRows.Add(row);
            }

            var monthName = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
            var empName = $"{employee.Surname}, {employee.FirstName} {employee.MiddleName}".Trim();
            if (!string.IsNullOrEmpty(employee.NameExtension))
            {
                empName += " " + employee.NameExtension;
            }

            return new DtrViewModel
            {
                EmployeeName = empName,
                EmployeeIdNumber = employee.EmployeeIdNumber ?? "",
                MonthYear = monthName,
                Month = month,
                Year = year,
                DaysInMonth = daysInMonth,
                RegularDaysHours = regularHoursStr,
                SaturdaysHours = saturdaysHoursStr,
                DayRows = dayRows,
                TardinessMinutes = totalTardinessMinutes,
                UndertimeMinutes = totalUndertimeMinutes,
                TardinessFrequency = tardinessFrequency,
                UndertimeFrequency = undertimeFrequency,
                TotalDays = totalCreditedDays
            };
        }

        private const string ArrangePoolValidationNote = "Unassigned punch (not placed in DTR slots)";

        private static bool IsArrangePoolEntry(TimeEntry e)
        {
            if (e.Status != EntryStatus.Flagged ||
                string.IsNullOrEmpty(e.ValidationNotes) ||
                !e.ValidationNotes.Contains("Unassigned punch", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Evening punches pooled by an overnight arrange on the previous day still belong to this date's shift.
            return e.EntryTime < ArrangeNoon;
        }

        private static List<TimeEntry> GetDtrDisplayEntries(IEnumerable<TimeEntry> entries) =>
            entries.Where(e => !IsArrangePoolEntry(e)).ToList();

        private sealed class DtrAttendanceDisplay
        {
            public string TardinessHrs { get; init; } = "-";
            public string TardinessMins { get; init; } = "-";
            public string UndertimeHrs { get; init; } = "-";
            public string UndertimeMins { get; init; } = "-";
            public string CombinedHrs { get; init; } = "-";
            public string CombinedMins { get; init; } = "-";
            public DtrDailyAttendanceMetrics? Metrics { get; init; }
        }

        private static void ApplyAttendanceColumns(DtrDayRowViewModel row, DtrAttendanceDisplay display)
        {
            row.TardinessUndertimeHrs = display.CombinedHrs;
            row.TardinessUndertimeMins = display.CombinedMins;
            row.UndertimeHrs = display.UndertimeHrs;
            row.UndertimeMins = display.UndertimeMins;
        }

        private static void ApplyShiftToRow(DtrDayRowViewModel row, ShiftType? shift)
        {
            if (shift == null) return;
            row.ShiftCode = shift.Code;
            row.ShiftLabel = shift.Label;
            row.ShiftColorHex = shift.ColorHex;
        }

        private static void ApplyShiftToRow(DtrManagementRowViewModel row, ShiftType? shift)
        {
            if (shift == null) return;
            row.ShiftCode = shift.Code;
            row.ShiftLabel = shift.Label;
            row.ShiftColorHex = shift.ColorHex;
        }

        private DtrAttendanceDisplay ComputeAttendanceDisplay(
            PayrollConfiguration? config,
            DateOnly date,
            List<TimeEntry> displayEntries,
            TimeOnly? timeIn,
            TimeOnly? lunchOut,
            TimeOnly? lunchIn,
            TimeOnly? timeOut,
            ShiftType? scheduledShift = null)
        {
            if (scheduledShift?.IsLeave == true)
            {
                return new DtrAttendanceDisplay();
            }

            if (!timeIn.HasValue && !timeOut.HasValue)
            {
                return new DtrAttendanceDisplay();
            }

            var schedule = _attendanceCalculation.ResolveSchedule(config, scheduledShift);
            if (schedule == null || schedule.SkipAttendanceAssessment)
            {
                return new DtrAttendanceDisplay();
            }

            var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
            if (scheduledShift == null && config?.UseWeekendAsDayOff == true && isWeekend)
            {
                return new DtrAttendanceDisplay();
            }

            var metrics = _attendanceCalculation.CalculateDaily(
                config, timeIn, lunchOut, lunchIn, timeOut, scheduledShift);
            return BuildAttendanceDisplay(metrics);
        }

        // Overnight DTR rows use calendar-day columns: tardiness on shift-start evening, undertime on next-morning out.
        private DtrAttendanceDisplay ComputeCalendarAttendanceDisplay(
            PayrollConfiguration? config,
            DateOnly date,
            List<TimeEntry> displayEntries,
            ShiftType? metricsShift,
            ShiftType? previousMetricsShift,
            DtrDayScheduleContext? scheduleContext,
            DtrResolvedDayPunches shiftResolved,
            DtrResolvedDayPunches? previousShiftResolved,
            bool currentIsOvernight,
            bool previousWasOvernight)
        {
            if (metricsShift?.IsLeave == true)
            {
                if (previousWasOvernight && previousShiftResolved?.TimeOut.HasValue == true)
                {
                    var leaveUndertime = _attendanceCalculation.CalculateOvernightMorningUndertimeMinutes(
                        config,
                        previousShiftResolved.TimeOut,
                        previousMetricsShift);
                    return BuildAttendanceDisplay(new DtrDailyAttendanceMetrics { UndertimeMinutes = leaveUndertime });
                }

                return new DtrAttendanceDisplay();
            }

            if (!currentIsOvernight && !previousWasOvernight)
            {
                return ComputeAttendanceDisplay(
                    config,
                    date,
                    displayEntries,
                    shiftResolved.TimeIn,
                    shiftResolved.LunchOut,
                    shiftResolved.LunchIn,
                    shiftResolved.TimeOut,
                    metricsShift);
            }

            var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
            if (metricsShift == null && config?.UseWeekendAsDayOff == true && isWeekend &&
                !previousWasOvernight)
            {
                return new DtrAttendanceDisplay();
            }

            var tardiness = 0;
            var undertime = 0;

            if (currentIsOvernight &&
                scheduleContext != null &&
                !scheduleContext.SkipAttendanceAssessment)
            {
                tardiness = _attendanceCalculation.CalculateOvernightEveningTardinessMinutes(
                    config,
                    shiftResolved.TimeIn,
                    metricsShift);
            }

            if (previousWasOvernight && previousShiftResolved?.TimeOut.HasValue == true)
            {
                undertime = _attendanceCalculation.CalculateOvernightMorningUndertimeMinutes(
                    config,
                    previousShiftResolved.TimeOut,
                    previousMetricsShift);
            }

            return BuildAttendanceDisplay(new DtrDailyAttendanceMetrics
            {
                TardinessMinutes = tardiness,
                UndertimeMinutes = undertime
            });
        }

        private static DtrAttendanceDisplay BuildAttendanceDisplay(DtrDailyAttendanceMetrics metrics)
        {
            var (tardinessHrs, tardinessMins) = DtrAttendanceCalculationService.FormatHoursAndMinutes(metrics.TardinessMinutes);
            var (undertimeHrs, undertimeMins) = DtrAttendanceCalculationService.FormatHoursAndMinutes(metrics.UndertimeMinutes);
            var combinedMinutes = metrics.TardinessMinutes + metrics.UndertimeMinutes;
            var (combinedHrs, combinedMins) = DtrAttendanceCalculationService.FormatHoursAndMinutes(combinedMinutes);

            return new DtrAttendanceDisplay
            {
                TardinessHrs = tardinessHrs,
                TardinessMins = tardinessMins,
                UndertimeHrs = undertimeHrs,
                UndertimeMins = undertimeMins,
                CombinedHrs = combinedHrs,
                CombinedMins = combinedMins,
                Metrics = metrics
            };
        }

        private static bool HasAnyDocumentTime(DtrDayRowViewModel row) =>
            !string.IsNullOrWhiteSpace(row.AmArrival) ||
            !string.IsNullOrWhiteSpace(row.AmDeparture) ||
            !string.IsNullOrWhiteSpace(row.PmArrival) ||
            !string.IsNullOrWhiteSpace(row.PmDeparture);

        private static void AccumulateAttendanceTotals(
            DtrDailyAttendanceMetrics metrics,
            bool assessAttendance,
            ref int tardinessFrequency,
            ref int undertimeFrequency,
            ref int totalTardinessMinutes,
            ref int totalUndertimeMinutes)
        {
            if (metrics.TardinessMinutes > 0)
            {
                totalTardinessMinutes += metrics.TardinessMinutes;
                if (assessAttendance)
                    tardinessFrequency++;
            }

            if (metrics.UndertimeMinutes > 0)
            {
                totalUndertimeMinutes += metrics.UndertimeMinutes;
                undertimeFrequency++;
            }
        }

        private static bool ShouldCreditWorkedDay(
            DtrDayScheduleContext? scheduleContext,
            bool currentIsOvernight,
            DtrResolvedDayPunches shiftResolved)
        {
            if (!shiftResolved.HasCompletePair)
                return false;

            if (DtrPunchResolutionService.IsOvernightSchedule(scheduleContext))
                return currentIsOvernight;

            return scheduleContext == null || !scheduleContext.SkipAttendanceAssessment;
        }

        private static (string AmDeparture, string PmArrival) FormatLunchSlotTimes(
            bool expectsLunch,
            TimeOnly? lunchOut,
            TimeOnly? lunchIn,
            Func<TimeOnly?, string> formatSlot)
        {
            if (!expectsLunch)
                return ("—", "—");

            return (
                lunchOut.HasValue ? formatSlot(lunchOut) : string.Empty,
                lunchIn.HasValue ? formatSlot(lunchIn) : string.Empty);
        }

        private static bool ShouldAssessAttendance(
            PayrollConfiguration? config,
            DateOnly date,
            ShiftType? scheduledShift,
            DtrDayScheduleContext? scheduleContext)
        {
            if (scheduledShift?.IsLeave == true)
                return false;

            if (scheduleContext == null || scheduleContext.SkipAttendanceAssessment)
                return false;

            var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
            return scheduledShift != null || config?.UseWeekendAsDayOff != true || !isWeekend;
        }

        private async Task MarkAsArrangePoolEntryAsync(Guid entryId, string modifiedBy, DateTime now)
        {
            await _db.TimeEntries
                .Where(e => e.Id == entryId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.EntryType, EntryType.BreakStart)
                    .SetProperty(e => e.PairedEntryId, (Guid?)null)
                    .SetProperty(e => e.Status, EntryStatus.Flagged)
                    .SetProperty(e => e.ModifiedBy, modifiedBy)
                    .SetProperty(e => e.ModifiedDate, now)
                    .SetProperty(e => e.ValidationNotes, ArrangePoolValidationNote));
        }

        private static Guid? ParseOptionalGuid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
        }

        private static TimeOnly? ParseOptionalTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return TimeOnly.TryParse(value, out var time) ? time : null;
        }

        private static string FormatTimeOnly(TimeOnly t) => t.ToString("hh:mmtt").ToUpperInvariant();
        private static string FormatTimeSpan(TimeSpan t) => TimeOnly.FromTimeSpan(t).ToString("hh:mmtt").ToUpperInvariant();
        private static string FormatSlot(TimeOnly? t) => t.HasValue ? t.Value.ToString("hh:mm tt") : "-";

        private static string FormatShiftWindow(ShiftType shift)
        {
            if (!shift.StartTime.HasValue || !shift.EndTime.HasValue)
                return "_______________";

            return $"{FormatTimeSpan(shift.StartTime.Value)} - {FormatTimeSpan(shift.EndTime.Value)}";
        }

        private static void ApplyDocumentRowTimes(
            DtrDayRowViewModel row,
            DtrResolvedDayPunches shiftResolved,
            DtrResolvedDayPunches? previousShiftResolved,
            bool previousWasOvernight,
            bool currentIsOvernight,
            List<TimeEntry> dayEntries,
            List<TimeEntry> nextDayEntries,
            bool expectsLunch)
        {
            if (previousWasOvernight || currentIsOvernight)
            {
                row.AmArrival = string.Empty;
                row.AmDeparture = previousWasOvernight
                    ? FormatResolvedTime(previousShiftResolved?.TimeOut, dayEntries, nextDayEntries)
                    : string.Empty;
                row.PmArrival = currentIsOvernight
                    ? FormatResolvedTime(shiftResolved.TimeIn, dayEntries, nextDayEntries)
                    : string.Empty;
                row.PmDeparture = string.Empty;
                return;
            }

            row.AmArrival = FormatDtrSlotTime(dayEntries, EntryType.TimeIn, shiftResolved.TimeIn);
            (row.AmDeparture, row.PmArrival) = FormatLunchSlotTimes(
                expectsLunch,
                shiftResolved.LunchOut,
                shiftResolved.LunchIn,
                t => FormatDtrSlotTime(dayEntries, GetLunchEntryType(t, shiftResolved.LunchOut, shiftResolved.LunchIn), t));
            row.PmDeparture = FormatDtrSlotTime(dayEntries, EntryType.TimeOut, shiftResolved.TimeOut);
        }

        private static void ApplyManagementRowTimes(
            DtrManagementRowViewModel row,
            DtrResolvedDayPunches shiftResolved,
            DtrResolvedDayPunches? previousShiftResolved,
            bool previousWasOvernight,
            bool currentIsOvernight,
            List<TimeEntry> dayEntries,
            List<TimeEntry> nextDayEntries,
            bool expectsLunch,
            Func<TimeOnly?, string> formatSlot)
        {
            row.UsesOvernightLayout = previousWasOvernight || currentIsOvernight;
            row.ExpectsPreviousNightEnd = previousWasOvernight;
            row.ExpectsTonightStart = currentIsOvernight;

            if (previousWasOvernight || currentIsOvernight)
            {
                row.AmArrival = string.Empty;
                row.AmDeparture = previousWasOvernight && previousShiftResolved?.TimeOut != null
                    ? formatSlot(previousShiftResolved.TimeOut)
                    : string.Empty;
                row.PmArrival = currentIsOvernight && shiftResolved.TimeIn != null
                    ? formatSlot(shiftResolved.TimeIn)
                    : string.Empty;
                row.PmDeparture = string.Empty;
                return;
            }

            row.AmArrival = shiftResolved.TimeIn.HasValue ? formatSlot(shiftResolved.TimeIn) : string.Empty;
            (row.AmDeparture, row.PmArrival) = FormatLunchSlotTimes(
                expectsLunch,
                shiftResolved.LunchOut,
                shiftResolved.LunchIn,
                formatSlot);
            row.PmDeparture = shiftResolved.TimeOut.HasValue ? formatSlot(shiftResolved.TimeOut) : string.Empty;
        }

        private static string FormatResolvedTime(
            TimeOnly? time,
            IEnumerable<TimeEntry> dayEntries,
            IEnumerable<TimeEntry> nextDayEntries)
        {
            if (!time.HasValue)
                return string.Empty;

            var entry = dayEntries.Concat(nextDayEntries)
                .FirstOrDefault(e => e.EntryTime == time.Value);

            var suffix = entry != null && IsUserProvidedEntry(entry) ? "**" : string.Empty;
            return FormatTimeOnly(time.Value) + suffix;
        }

        private static decimal GetWorkedDayCredit(ShiftType? scheduledShift)
        {
            if (ShiftTypeScheduleHelper.CountsTowardDtrTotal(scheduledShift))
                return ShiftTypeScheduleHelper.GetCreditedDays(scheduledShift);

            // Worked day without a scheduled shift: credit one standard day.
            return 1m;
        }

        private static bool IsUserProvidedEntry(TimeEntry entry) =>
            entry.IsManualEntry || entry.Source == EntrySource.Manual;

        private static TimeOnly? GetFirstEntryTime(IEnumerable<TimeEntry> entries, params EntryType[] types)
        {
            return entries
                .Where(e => types.Contains(e.EntryType))
                .OrderBy(e => e.EntryTime)
                .Select(e => (TimeOnly?)e.EntryTime)
                .FirstOrDefault();
        }

        private static TimeOnly? GetLastEntryTime(IEnumerable<TimeEntry> entries, EntryType type)
        {
            return entries
                .Where(e => e.EntryType == type)
                .OrderByDescending(e => e.EntryTime)
                .Select(e => (TimeOnly?)e.EntryTime)
                .FirstOrDefault();
        }

        private static EntryType GetLunchEntryType(TimeOnly? time, TimeOnly? lunchOut, TimeOnly? lunchIn)
        {
            if (time.HasValue && lunchOut.HasValue && time.Value == lunchOut.Value)
                return EntryType.LunchStart;
            if (time.HasValue && lunchIn.HasValue && time.Value == lunchIn.Value)
                return EntryType.LunchEnd;
            return EntryType.LunchStart;
        }

        private static string FormatDtrSlotTime(
            IEnumerable<TimeEntry> entries,
            EntryType type,
            TimeOnly? time)
        {
            if (!time.HasValue)
                return string.Empty;

            var entry = entries
                .Where(e => e.EntryType == type && e.EntryTime == time.Value)
                .OrderBy(e => e.EntryTime)
                .FirstOrDefault();

            var suffix = entry != null && IsUserProvidedEntry(entry) ? "**" : string.Empty;
            return FormatTimeOnly(time.Value) + suffix;
        }

        private static void PopulateFlexibleDtrRow(
            DtrDayRowViewModel row,
            List<TimeEntry> displayEntries,
            ShiftType shift)
        {
            var timeIn = GetFirstEntryTime(displayEntries, EntryType.TimeIn);
            var lunchOut = GetFirstEntryTime(displayEntries, EntryType.LunchStart, EntryType.BreakStart);
            var lunchIn = GetFirstEntryTime(displayEntries, EntryType.LunchEnd, EntryType.BreakEnd);
            var timeOut = GetLastEntryTime(displayEntries, EntryType.TimeOut);

            row.IsUserProvided = displayEntries.Any(IsUserProvidedEntry);

            if (timeIn.HasValue && timeOut.HasValue)
            {
                row.ShowFlexibleOfficialTime = true;
                row.AmArrival = FormatDtrSlotTime(displayEntries, EntryType.TimeIn, timeIn);
                row.FlexibleOfficialTimeLabel = $"-- OFFICIAL TIME {shift.Hours:0}HR --";
                row.PmDeparture = FormatDtrSlotTime(displayEntries, EntryType.TimeOut, timeOut);
                return;
            }

            row.AmArrival = FormatDtrSlotTime(displayEntries, EntryType.TimeIn, timeIn);
            if (lunchOut.HasValue)
            {
                row.AmDeparture = FormatDtrSlotTime(
                    displayEntries,
                    displayEntries.Any(e => e.EntryType == EntryType.LunchStart && e.EntryTime == lunchOut.Value)
                        ? EntryType.LunchStart
                        : EntryType.BreakStart,
                    lunchOut);
            }
            else if (timeOut.HasValue && !timeIn.HasValue)
            {
                row.PmDeparture = FormatDtrSlotTime(displayEntries, EntryType.TimeOut, timeOut);
            }
            else if (timeOut.HasValue)
            {
                row.AmDeparture = FormatDtrSlotTime(displayEntries, EntryType.TimeOut, timeOut);
            }

            if (lunchIn.HasValue)
            {
                var lunchInType = displayEntries.Any(e => e.EntryType == EntryType.LunchEnd && e.EntryTime == lunchIn.Value)
                    ? EntryType.LunchEnd
                    : EntryType.BreakEnd;
                row.PmArrival = FormatDtrSlotTime(displayEntries, lunchInType, lunchIn);
            }
        }

        private async Task<bool> CanAccessEmployeeAsync(Guid employeeId)
        {
            var isEmployeeOnly = await IsEmployeeOnlyUserAsync();
            if (!isEmployeeOnly)
            {
                return true;
            }

            var employee = await GetCurrentUserEmployeeAsync();
            return employee != null && employee.Id == employeeId;
        }

        private static TimeEntry BuildManualEntry(
            Guid employeeId,
            DateOnly date,
            TimeOnly time,
            EntryType type,
            string justification,
            string createdBy)
        {
            return new TimeEntry
            {
                EmployeeId = employeeId,
                EntryDate = date,
                EntryTime = time,
                EntryType = type,
                Source = EntrySource.Manual,
                IsManualEntry = true,
                Status = EntryStatus.Valid,
                Notes = $"DTR Justification: {justification}",
                CreatedBy = createdBy,
                CreatedDate = DateTime.Now
            };
        }

        private async Task<DtrJustificationFormViewModel> BuildJustificationFormAsync(Guid employeeId, DateOnly date)
        {
            var latestRequest = await _db.TimeLogJustifications
                .Include(j => j.ApprovalRequest)
                .Where(j => j.EmployeeId == employeeId && j.EntryDate == date)
                .OrderByDescending(j => j.SubmittedOn)
                .FirstOrDefaultAsync();

            if (latestRequest != null &&
                ApprovalRequestStatusHelper.IsPending(
                    latestRequest.Status,
                    latestRequest.ApprovalRequest?.Status))
            {
                return new DtrJustificationFormViewModel
                {
                    EmployeeId = employeeId,
                    Date = date,
                    DateDisplay = date.ToDateTime(new TimeOnly(0, 0)).ToString("dddd, MMMM dd, yyyy"),
                    HasPendingRequest = true,
                    ExistingStatus = JustificationStatus.Pending,
                    StatusMessage = "Your justification for this date is pending approval. You will be notified once it is reviewed."
                };
            }

            var payrollConfig = await _attendanceCalculation.GetActiveConfigurationAsync();
            var scheduledShift = await _attendanceCalculation.GetEmployeeShiftTypeForDayAsync(employeeId, date);
            var schedule = _attendanceCalculation.ResolveSchedule(payrollConfig, scheduledShift);
            var layout = DtrPunchResolutionService.GetLayout(schedule);

            var dayEntries = await LoadDayEntriesAsync(employeeId, date);
            var nextDayEntries = await LoadDayEntriesAsync(employeeId, date.AddDays(1));

            var prevDate = date.AddDays(-1);
            var prevShift = await _attendanceCalculation.GetEmployeeShiftTypeForDayAsync(employeeId, prevDate);
            var prevSchedule = _attendanceCalculation.ResolveSchedule(payrollConfig, prevShift);
            var prevLayout = DtrPunchResolutionService.GetLayout(prevSchedule);
            var prevDayEntries = await LoadDayEntriesAsync(employeeId, prevDate);
            var prevOnlyResolved = DtrPunchResolutionService.ResolveShift(prevSchedule, prevDayEntries, dayEntries);

            var form = new DtrJustificationFormViewModel
            {
                EmployeeId = employeeId,
                Date = date,
                DateDisplay = date.ToDateTime(new TimeOnly(0, 0)).ToString("dddd, MMMM dd, yyyy"),
                ScheduleLayout = layout.ToString()
            };

            var biometricLogs = await _db.BiometricLogs
                .Where(b => b.EmployeeId == employeeId &&
                            DateOnly.FromDateTime(b.Timestamp) >= date &&
                            DateOnly.FromDateTime(b.Timestamp) <= date.AddDays(1))
                .OrderBy(b => b.Timestamp)
                .Select(b => b.Timestamp.ToString("MMM dd hh:mm tt"))
                .ToListAsync();

            if (scheduledShift?.IsFlexible == true)
            {
                form.BiometricLogs = biometricLogs;
                return form;
            }

            // Overnight shift end from the previous night can fall on this calendar morning (e.g. VL day).
            if (prevLayout == DtrScheduleLayout.Overnight &&
                prevOnlyResolved.TimeIn.HasValue &&
                !prevOnlyResolved.TimeOut.HasValue)
            {
                form.MissingSlots.Add(new DtrMissingSlotViewModel
                {
                    Key = "AmOut",
                    Label = "NO AM DEPARTURE",
                    Description = $"Shift end for {prevDate:MMM dd} night shift (this morning)"
                });
                form.ScheduleLayout = DtrScheduleLayout.Overnight.ToString();
                form.RelatesToPreviousShiftDate = prevDate;
                form.DateDisplay = $"{date:dddd, MMMM dd, yyyy} (shift end for {prevDate:MMM dd})";
            }

            if (scheduledShift?.IsLeave == true)
            {
                form.BiometricLogs = biometricLogs;

                // Overnight shift-end that lands on a leave/holiday morning is already in MissingSlots.
                if (form.RelatesToPreviousShiftDate.HasValue)
                    return form;

                // Leave / holiday / day off are not assessed for attendance, but staff may still
                // have worked (or need to record punches). Offer simple In/Out slots so justify works.
                form.ScheduleLayout = DtrScheduleLayout.SimpleDay.ToString();
                form.InfoMessage =
                    $"This day is scheduled as {scheduledShift.Code} ({scheduledShift.Label}). " +
                    "Attendance is not assessed, but you can still justify time logs if work was performed.";

                var leaveResolved = DtrPunchResolutionService.ResolveShift(null, dayEntries, nextDayEntries);
                if (!leaveResolved.TimeIn.HasValue)
                {
                    form.MissingSlots.Add(new DtrMissingSlotViewModel
                    {
                        Key = "AmIn",
                        Label = "TIME IN",
                        Description = "Preferred Time In"
                    });
                }

                if (!leaveResolved.TimeOut.HasValue)
                {
                    form.MissingSlots.Add(new DtrMissingSlotViewModel
                    {
                        Key = "PmOut",
                        Label = "TIME OUT",
                        Description = "Preferred Time Out"
                    });
                }

                // Both punches already exist — still allow optional corrections.
                if (!form.MissingSlots.Any())
                {
                    form.MissingSlots.Add(new DtrMissingSlotViewModel
                    {
                        Key = "AmIn",
                        Label = "TIME IN",
                        Description = "Preferred Time In (optional correction)"
                    });
                    form.MissingSlots.Add(new DtrMissingSlotViewModel
                    {
                        Key = "PmOut",
                        Label = "TIME OUT",
                        Description = "Preferred Time Out (optional correction)"
                    });
                }

                return form;
            }

            var resolved = DtrPunchResolutionService.ResolveShift(schedule, dayEntries, nextDayEntries);

            switch (layout)
            {
                case DtrScheduleLayout.Overnight:
                    if (!resolved.TimeIn.HasValue)
                    {
                        form.MissingSlots.Add(new DtrMissingSlotViewModel
                        {
                            Key = "PmIn",
                            Label = "NO PM ARRIVAL",
                            Description = "Preferred shift start (evening)"
                        });
                    }

                    if (!resolved.TimeOut.HasValue)
                    {
                        form.MissingSlots.Add(new DtrMissingSlotViewModel
                        {
                            Key = "AmOut",
                            Label = "NO AM DEPARTURE",
                            Description = $"Preferred shift end (next-day morning, {date.AddDays(1):MMM dd})"
                        });
                    }
                    break;

                case DtrScheduleLayout.SimpleDay:
                    if (!resolved.TimeIn.HasValue)
                    {
                        form.MissingSlots.Add(new DtrMissingSlotViewModel
                        {
                            Key = "AmIn",
                            Label = "NO AM IN",
                            Description = "Preferred Time In"
                        });
                    }

                    if (!resolved.TimeOut.HasValue)
                    {
                        form.MissingSlots.Add(new DtrMissingSlotViewModel
                        {
                            Key = "PmOut",
                            Label = "NO PM OUT",
                            Description = "Preferred Time Out"
                        });
                    }
                    break;

                default:
                    if (!resolved.TimeIn.HasValue)
                    {
                        form.MissingSlots.Add(new DtrMissingSlotViewModel
                        {
                            Key = "AmIn",
                            Label = "NO AM IN",
                            Description = "Preferred Time In"
                        });
                    }

                    if (!resolved.LunchOut.HasValue)
                    {
                        form.MissingSlots.Add(new DtrMissingSlotViewModel
                        {
                            Key = "LunchOut",
                            Label = "NO LUNCH OUT",
                            Description = "Preferred Lunch Out Time"
                        });
                    }

                    if (!resolved.LunchIn.HasValue)
                    {
                        form.MissingSlots.Add(new DtrMissingSlotViewModel
                        {
                            Key = "LunchIn",
                            Label = "NO LUNCH IN",
                            Description = "Preferred Lunch In Time"
                        });
                    }

                    if (!resolved.TimeOut.HasValue)
                    {
                        form.MissingSlots.Add(new DtrMissingSlotViewModel
                        {
                            Key = "PmOut",
                            Label = "NO PM OUT",
                            Description = "Preferred Time Out"
                        });
                    }
                    break;
            }

            form.BiometricLogs = biometricLogs;
            if (!form.MissingSlots.Any())
            {
                form.InfoMessage = layout == DtrScheduleLayout.Overnight
                    ? "Night shift punches look complete for this date. To fix shift end on the next morning (e.g. on a leave day), open Justify on that next calendar date."
                    : "No missing time entries were detected for this date.";
            }

            return form;
        }

        private async Task<List<DtrManagementRowViewModel>> BuildDtrManagementRowsAsync(Guid employeeId, int month, int year)
        {
            await _scheduleChangeService.SyncApprovedChangesForEmployeeMonthAsync(employeeId, month, year);

            var daysInMonth = DateTime.DaysInMonth(year, month);
            var startDate = new DateOnly(year, month, 1);
            var endDate = new DateOnly(year, month, daysInMonth);

            var timeEntries = await _db.TimeEntries
                .Where(te => te.EmployeeId == employeeId &&
                             te.EntryDate >= startDate &&
                             te.EntryDate <= endDate.AddDays(1))
                .OrderBy(te => te.EntryDate)
                .ThenBy(te => te.EntryTime)
                .AsNoTracking()
                .ToListAsync();

            var justifications = await _db.TimeLogJustifications
                .Where(j => j.EmployeeId == employeeId &&
                            j.EntryDate >= startDate &&
                            j.EntryDate <= endDate)
                .OrderByDescending(j => j.SubmittedOn)
                .AsNoTracking()
                .ToListAsync();

            var latestJustificationByDate = justifications
                .GroupBy(j => j.EntryDate)
                .ToDictionary(g => g.Key, g => g.First());

            var entriesByDate = timeEntries.GroupBy(te => te.EntryDate).ToDictionary(g => g.Key, g => g.ToList());
            var payrollConfig = await _attendanceCalculation.GetActiveConfigurationAsync();
            var shiftsForDisplay = await _attendanceCalculation.GetEmployeeShiftTypesByDayAsync(employeeId, month, year, approvedOnly: false);
            var shiftsForAttendance = await _attendanceCalculation.GetEmployeeShiftTypesByDayAsync(employeeId, month, year, approvedOnly: true);

            var scheduleChangeRequests = await _db.ScheduleChangeRequests
                .Include(r => r.ApprovalRequest)
                .Include(r => r.WorkSchedule)
                .Where(r => r.EmployeeId == employeeId &&
                            r.WorkSchedule.Month == month &&
                            r.WorkSchedule.Year == year)
                .AsNoTracking()
                .ToListAsync();

            var overtimeRequests = await _db.OvertimeRequests
                .Include(r => r.ApprovalRequest)
                .Where(r => r.EmployeeId == employeeId &&
                            r.StartDate <= endDate &&
                            r.EndDate >= startDate &&
                            !r.IsDeleted)
                .OrderByDescending(r => r.SubmittedOn)
                .AsNoTracking()
                .ToListAsync();

            var undertimeRequests = await _db.UndertimeRequests
                .Include(r => r.ApprovalRequest)
                .Where(r => r.EmployeeId == employeeId &&
                            r.DateOfUndertime >= startDate &&
                            r.DateOfUndertime <= endDate &&
                            !r.IsDeleted)
                .OrderByDescending(r => r.SubmittedOn)
                .AsNoTracking()
                .ToListAsync();

            var scheduleChangeApprovalIds = scheduleChangeRequests
                .Where(r => r.ApprovalRequestId.HasValue)
                .Select(r => r.ApprovalRequestId!.Value)
                .Distinct()
                .ToList();

            var justificationApprovalIds = justifications
                .Where(j => j.ApprovalRequestId.HasValue)
                .Select(j => j.ApprovalRequestId!.Value)
                .Distinct()
                .ToList();

            var overtimeApprovalIds = overtimeRequests
                .Where(r => r.ApprovalRequestId.HasValue)
                .Select(r => r.ApprovalRequestId!.Value)
                .Distinct()
                .ToList();

            var undertimeApprovalIds = undertimeRequests
                .Where(r => r.ApprovalRequestId.HasValue)
                .Select(r => r.ApprovalRequestId!.Value)
                .Distinct()
                .ToList();

            var allApprovalIds = scheduleChangeApprovalIds
                .Concat(justificationApprovalIds)
                .Concat(overtimeApprovalIds)
                .Concat(undertimeApprovalIds)
                .Distinct()
                .ToList();

            var approvalStatusById = allApprovalIds.Count == 0
                ? new Dictionary<int, ApprovalRequestStatus>()
                : await _db.ApprovalRequests
                    .AsNoTracking()
                    .Where(r => allApprovalIds.Contains(r.Id))
                    .Select(r => new { r.Id, r.Status })
                    .ToDictionaryAsync(x => x.Id, x => x.Status);

            var latestScheduleChangeByDay = scheduleChangeRequests
                .GroupBy(r => r.Day)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.SubmittedOn).First());

            var latestOvertimeByDate = new Dictionary<DateOnly, OvertimeRequest>();
            foreach (var request in overtimeRequests)
            {
                foreach (var day in request.EnumerateDaysInPeriod(startDate, endDate))
                {
                    if (!latestOvertimeByDate.ContainsKey(day))
                        latestOvertimeByDate[day] = request;
                }
            }

            var latestUndertimeByDate = undertimeRequests
                .GroupBy(r => r.DateOfUndertime)
                .ToDictionary(g => g.Key, g => g.First());

            var dayTypesByDate = await _workingCalendar.GetDayTypesForRangeAsync(startDate, endDate);
            var allDayTypes = await _db.DayTypes
                .AsNoTracking()
                .Where(d => d.IsActive && !d.IsDeleted)
                .ToListAsync();
            var holidayDates = await _db.WorkingCalendars
                .AsNoTracking()
                .Where(c => c.Date >= startDate &&
                            c.Date <= endDate &&
                            !c.IsDeleted &&
                            c.HolidayId != null)
                .Select(c => c.Date)
                .ToListAsync();
            var holidayDateSet = holidayDates.ToHashSet();

            var rows = new List<DtrManagementRowViewModel>();

            for (var day = 1; day <= daysInMonth; day++)
            {
                var date = new DateOnly(year, month, day);
                shiftsForDisplay.TryGetValue(day, out var displayShift);
                shiftsForAttendance.TryGetValue(day, out var attendanceShift);
                entriesByDate.TryGetValue(date, out var entries);
                entries ??= new List<TimeEntry>();

                var displayEntries = GetDtrDisplayEntries(entries).ToList();

                entriesByDate.TryGetValue(date.AddDays(1), out var nextDayRaw);
                var nextDayDisplay = GetDtrDisplayEntries(nextDayRaw ?? new List<TimeEntry>()).ToList();
                entriesByDate.TryGetValue(date.AddDays(-1), out var prevDayRaw);
                var prevDayDisplay = GetDtrDisplayEntries(prevDayRaw ?? new List<TimeEntry>()).ToList();

                var metricsShift = attendanceShift ?? displayShift;
                var layoutShift = displayShift ?? metricsShift;
                var scheduleContext = _attendanceCalculation.ResolveSchedule(payrollConfig, layoutShift);
                shiftsForDisplay.TryGetValue(day - 1, out var prevDisplayShift);
                shiftsForAttendance.TryGetValue(day - 1, out var prevAttendanceShift);
                var prevScheduleContext = day > 1
                    ? _attendanceCalculation.ResolveSchedule(payrollConfig, prevDisplayShift)
                    : null;
                var previousMetricsShift = prevAttendanceShift ?? prevDisplayShift;
                var currentIsOvernight = DtrPunchResolutionService.IsOvernightSchedule(scheduleContext);
                var previousWasOvernight = DtrPunchResolutionService.IsOvernightSchedule(prevScheduleContext);
                var shiftResolved = DtrPunchResolutionService.ResolveShift(
                    scheduleContext, displayEntries, nextDayDisplay);
                var previousShiftResolved = day > 1
                    ? DtrPunchResolutionService.ResolveShift(prevScheduleContext, prevDayDisplay, displayEntries)
                    : null;
                var expectsLunch = scheduleContext?.HasLunchBreak == true && !currentIsOvernight;
                var assessAttendance = ShouldAssessAttendance(payrollConfig, date, metricsShift, scheduleContext);
                dayTypesByDate.TryGetValue(date, out var calendarDayType);
                var dayType = DayTypeHelper.ResolveDayType(
                    metricsShift,
                    calendarDayType,
                    isHoliday: holidayDateSet.Contains(date) || DayTypeHelper.IsHolidayDayType(calendarDayType),
                    allDayTypes);

                var attendance = _attendanceComputation.Compute(new AttendanceComputationInput
                {
                    Date = date,
                    Config = payrollConfig,
                    Shift = metricsShift,
                    PreviousShift = previousMetricsShift,
                    Schedule = scheduleContext,
                    PreviousSchedule = prevScheduleContext,
                    ShiftResolved = shiftResolved,
                    PreviousShiftResolved = previousShiftResolved,
                    CurrentIsOvernight = currentIsOvernight,
                    PreviousWasOvernight = previousWasOvernight,
                    DayType = dayType,
                    AssessAttendance = assessAttendance
                });

                var (tardinessHrs, tardinessMins) = DtrAttendanceCalculationService.FormatHoursAndMinutes(attendance.TardinessMinutes);
                var (undertimeHrs, undertimeMins) = DtrAttendanceCalculationService.FormatHoursAndMinutes(attendance.UndertimeMinutes);

                var hasArrangeableEntries = displayEntries.Count > 0 ||
                    entries.Any(IsArrangePoolEntry) ||
                    (currentIsOvernight && nextDayDisplay.Any(e => e.EntryTime < ArrangeNoon));

                var mgmtRow = new DtrManagementRowViewModel
                {
                    Date = date,
                    DayNo = day,
                    DayName = date.DayOfWeek.ToString(),
                    HasTimeEntries = hasArrangeableEntries,
                    ExpectsLunchBreak = expectsLunch,
                    IsOvernightShiftDay = currentIsOvernight,
                    HasCompleteShiftPair = shiftResolved.HasCompletePair,
                    AssessAttendance = assessAttendance,
                    DayTypeCode = string.IsNullOrWhiteSpace(attendance.DayTypeCode) ? "—" : attendance.DayTypeCode!,
                    DayTypeName = attendance.DayTypeName ?? string.Empty,
                    WorkedHours = attendance.WorkedHours,
                    ActualPresenceHours = attendance.ActualPresenceHours,
                    NightDifferentialHours = attendance.NightDifferentialHours,
                    PotentialOvertimeHours = _overtimeEngine.ApplyPolicyToHours(
                        attendance.PotentialOvertimeHours, payrollConfig),
                    EarlyArrivalMinutes = attendance.EarlyArrivalMinutes,
                    WorkedHoursDisplay = attendance.WorkedHours > 0 ? attendance.WorkedHours.ToString("0.##") : "—",
                    NightDifferentialHoursDisplay = attendance.NightDifferentialHours > 0
                        ? attendance.NightDifferentialHours.ToString("0.##")
                        : "—",
                    TardinessHrs = tardinessHrs,
                    TardinessMins = tardinessMins,
                    UndertimeHrs = undertimeHrs,
                    UndertimeMins = undertimeMins
                };
                ApplyManagementRowTimes(
                    mgmtRow,
                    shiftResolved,
                    previousShiftResolved,
                    previousWasOvernight,
                    currentIsOvernight,
                    displayEntries,
                    nextDayDisplay,
                    expectsLunch,
                    FormatSlot);

                var hasCompletePunch = shiftResolved.HasCompletePair;

                // Require justification on scheduled work days when required punches are missing.
                latestJustificationByDate.TryGetValue(date, out var latestJustification);
                latestScheduleChangeByDay.TryGetValue(day, out var latestScheduleChange);
                latestOvertimeByDate.TryGetValue(date, out var latestOvertime);
                latestUndertimeByDate.TryGetValue(date, out var latestUndertime);

                JustificationStatus? effectiveJustificationStatus = latestJustification == null
                    ? null
                    : ApprovalRequestStatusHelper.ResolveEffectiveStatus(
                        latestJustification.Status,
                        latestJustification.ApprovalRequestId is int jApprovalId &&
                        approvalStatusById.TryGetValue(jApprovalId, out var jApprovalStatus)
                            ? jApprovalStatus
                            : null);

                var hasPendingJustification = effectiveJustificationStatus == JustificationStatus.Pending;
                var hasApprovedJustification = effectiveJustificationStatus == JustificationStatus.Approved;
                var hasRejectedJustification = effectiveJustificationStatus == JustificationStatus.Rejected;

                var justificationRequired = assessAttendance && !hasPendingJustification &&
                    (currentIsOvernight
                        ? !shiftResolved.HasCompletePair
                        : (expectsLunch
                            ? !(shiftResolved.TimeIn.HasValue && shiftResolved.LunchOut.HasValue &&
                                shiftResolved.LunchIn.HasValue && shiftResolved.TimeOut.HasValue)
                            : !(shiftResolved.TimeIn.HasValue && shiftResolved.TimeOut.HasValue)));

                var justificationNote = entries
                    .Where(e => e.IsManualEntry && !string.IsNullOrWhiteSpace(e.Notes) && e.Notes.Contains("DTR Justification"))
                    .Select(e => e.Notes!)
                    .FirstOrDefault();

                string timeLogJustificationText;
                if (hasPendingJustification)
                {
                    timeLogJustificationText = "⏳ Pending approval";
                }
                else if (hasRejectedJustification)
                {
                    timeLogJustificationText = "✗ Rejected — resubmit required";
                }
                else if (hasApprovedJustification && hasCompletePunch)
                {
                    timeLogJustificationText = "✓ Approved";
                }
                else if (hasApprovedJustification && !hasCompletePunch)
                {
                    timeLogJustificationText = "⚠ Justification required";
                }
                else if (justificationRequired)
                {
                    timeLogJustificationText = "⚠ Justification required";
                }
                else if (!string.IsNullOrWhiteSpace(justificationNote))
                {
                    timeLogJustificationText = justificationNote;
                }
                else
                {
                    timeLogJustificationText = "-";
                }

                JustificationStatus? effectiveScheduleChangeStatus = latestScheduleChange == null
                    ? null
                    : ApprovalRequestStatusHelper.ResolveEffectiveStatus(
                        latestScheduleChange.Status,
                        latestScheduleChange.ApprovalRequestId is int scApprovalId &&
                        approvalStatusById.TryGetValue(scApprovalId, out var scApprovalStatus)
                            ? scApprovalStatus
                            : null);

                string scheduleChangeRemarks = "-";
                if (latestScheduleChange != null)
                {
                    var reason = latestScheduleChange.Reason?.Trim();
                    var reviewNotes = latestScheduleChange.ReviewNotes?.Trim();

                    scheduleChangeRemarks = effectiveScheduleChangeStatus switch
                    {
                        JustificationStatus.Pending =>
                            string.IsNullOrWhiteSpace(reason)
                                ? "Pending schedule change request."
                                : reason!,
                        JustificationStatus.Approved =>
                            !string.IsNullOrWhiteSpace(reviewNotes)
                                ? reviewNotes!
                                : (!string.IsNullOrWhiteSpace(reason) ? reason! : "Schedule change approved."),
                        JustificationStatus.Rejected =>
                            !string.IsNullOrWhiteSpace(reviewNotes)
                                ? reviewNotes!
                                : (!string.IsNullOrWhiteSpace(reason) ? reason! : "Schedule change rejected."),
                        _ => !string.IsNullOrWhiteSpace(reason) ? reason! : "-"
                    };
                }

                OvertimeStatus? effectiveOvertimeStatus = latestOvertime == null
                    ? null
                    : ApprovalRequestStatusHelper.ResolveEffectiveStatus(
                        latestOvertime.Status,
                        latestOvertime.ApprovalRequestId is int otApprovalId &&
                        approvalStatusById.TryGetValue(otApprovalId, out var otApprovalStatus)
                            ? otApprovalStatus
                            : null);

                string? overtimeHoursDisplay = null;
                string overtimeRemarks = "-";
                string overtimeStatusLabel = "—";
                decimal? overtimeRequestHours = null;

                if (latestOvertime != null)
                {
                    var start = latestOvertime.ApprovedStartTime ?? latestOvertime.RequestedStartTime;
                    var end = latestOvertime.ApprovedEndTime ?? latestOvertime.RequestedEndTime;
                    var hours = OvertimeRequest.ComputeHours(
                        start,
                        end,
                        latestOvertime.EffectiveHasLunchBreak,
                        latestOvertime.EffectiveLunchStart,
                        latestOvertime.EffectiveLunchEnd,
                        latestOvertime.EffectiveBreakMinutes);
                    overtimeRequestHours = hours;
                    overtimeHoursDisplay = $"{start:h:mm tt} – {end:h:mm tt} ({hours:0.##}h)";

                    var reason = latestOvertime.Reason?.Trim();
                    var reviewNotes = latestOvertime.ReviewNotes?.Trim();
                    overtimeRemarks = effectiveOvertimeStatus switch
                    {
                        OvertimeStatus.Pending =>
                            string.IsNullOrWhiteSpace(reason) ? "Pending overtime request." : reason!,
                        OvertimeStatus.Approved when latestOvertime.IsProcessedToPayroll =>
                            !string.IsNullOrWhiteSpace(reviewNotes) ? reviewNotes! : "Imported to payroll.",
                        OvertimeStatus.Approved =>
                            !string.IsNullOrWhiteSpace(reviewNotes)
                                ? reviewNotes!
                                : (!string.IsNullOrWhiteSpace(reason) ? reason! : "Approved — pending payroll import."),
                        OvertimeStatus.Rejected =>
                            !string.IsNullOrWhiteSpace(reviewNotes)
                                ? reviewNotes!
                                : (!string.IsNullOrWhiteSpace(reason) ? reason! : "Overtime rejected."),
                        _ => !string.IsNullOrWhiteSpace(reason) ? reason! : "-"
                    };
                }

                overtimeStatusLabel = BuildOvertimeStatusLabel(
                    effectiveOvertimeStatus,
                    latestOvertime?.IsProcessedToPayroll == true,
                    overtimeRequestHours);

                mgmtRow.JustificationRequired = justificationRequired;
                mgmtRow.TimeLogJustification = timeLogJustificationText;
                mgmtRow.JustificationStatus = effectiveJustificationStatus;
                mgmtRow.PendingJustificationId = hasPendingJustification ? latestJustification!.Id : null;
                mgmtRow.LatestJustificationRequestId = latestJustification?.Id;
                mgmtRow.ScheduleChangeStatus = effectiveScheduleChangeStatus;
                mgmtRow.PendingScheduleChangeId = effectiveScheduleChangeStatus == JustificationStatus.Pending
                    ? latestScheduleChange?.Id : null;
                mgmtRow.ScheduleChangeRemarks = scheduleChangeRemarks;
                mgmtRow.LatestScheduleChangeRequestId = latestScheduleChange?.Id;
                mgmtRow.OvertimeStatus = effectiveOvertimeStatus;
                mgmtRow.PendingOvertimeRequestId = effectiveOvertimeStatus == OvertimeStatus.Pending
                    ? latestOvertime?.Id : null;
                mgmtRow.LatestOvertimeRequestId = latestOvertime?.Id;
                mgmtRow.OvertimeRemarks = overtimeRemarks;
                mgmtRow.OvertimeHoursDisplay = overtimeHoursDisplay;
                mgmtRow.OvertimeStatusLabel = overtimeStatusLabel;
                mgmtRow.IsOvertimeProcessedToPayroll = latestOvertime?.IsProcessedToPayroll == true;

                mgmtRow.ComputedUndertimeMinutes = attendance.UndertimeMinutes;

                JustificationStatus? effectiveUndertimeStatus = latestUndertime == null
                    ? null
                    : ApprovalRequestStatusHelper.ResolveEffectiveStatus(
                        latestUndertime.Status,
                        latestUndertime.ApprovalRequestId is int utApprovalId &&
                        approvalStatusById.TryGetValue(utApprovalId, out var utApprovalStatus)
                            ? utApprovalStatus
                            : null);

                if (latestUndertime != null)
                {
                    mgmtRow.LatestUndertimeRequestId = latestUndertime.Id;
                    mgmtRow.UndertimeStatus = effectiveUndertimeStatus;
                    mgmtRow.PendingUndertimeRequestId = effectiveUndertimeStatus == JustificationStatus.Pending
                        ? latestUndertime.Id : null;
                    mgmtRow.UndertimeHoursDisplay =
                        $"{latestUndertime.DurationHours:0.##}h filed (total {latestUndertime.TotalHours:0.##}h)";
                    var purpose = latestUndertime.Purpose?.Trim();
                    mgmtRow.UndertimeRemarks = effectiveUndertimeStatus switch
                    {
                        JustificationStatus.Pending =>
                            string.IsNullOrWhiteSpace(purpose) ? "Pending undertime request." : purpose!,
                        JustificationStatus.Approved =>
                            string.IsNullOrWhiteSpace(latestUndertime.ReviewNotes?.Trim())
                                ? (string.IsNullOrWhiteSpace(purpose) ? "Undertime approved." : purpose!)
                                : latestUndertime.ReviewNotes!.Trim(),
                        JustificationStatus.Rejected =>
                            string.IsNullOrWhiteSpace(latestUndertime.ReviewNotes?.Trim())
                                ? (string.IsNullOrWhiteSpace(purpose) ? "Undertime rejected." : purpose!)
                                : latestUndertime.ReviewNotes!.Trim(),
                        _ => string.IsNullOrWhiteSpace(purpose) ? "Undertime form filed." : purpose!
                    };
                    mgmtRow.UndertimeStatusLabel = BuildUndertimeStatusLabel(
                        effectiveUndertimeStatus,
                        latestUndertime.DurationHours);
                }

                ApplyShiftToRow(mgmtRow, displayShift);
                rows.Add(mgmtRow);
            }

            return rows;
        }

        private static string BuildUndertimeStatusLabel(
            JustificationStatus? status,
            decimal durationHours)
        {
            var hoursSuffix = durationHours > 0 ? $" ({durationHours:0.##}h)" : string.Empty;

            return status switch
            {
                JustificationStatus.Pending => $"Pending Approval{hoursSuffix}",
                JustificationStatus.Approved => $"Approved{hoursSuffix}",
                JustificationStatus.Rejected => $"Rejected{hoursSuffix}",
                _ => "—"
            };
        }

        private static string BuildOvertimeStatusLabel(
            OvertimeStatus? status,
            bool importedToPayroll,
            decimal? requestHours)
        {
            static string WithHours(string label, decimal hours) =>
                hours > 0 ? $"{label} ({hours:0.##}h)" : label;

            if (status == OvertimeStatus.Pending)
                return WithHours("Pending Approval", requestHours ?? 0m);

            if (status == OvertimeStatus.Approved && importedToPayroll)
                return WithHours("Imported to Payroll", requestHours ?? 0m);

            if (status == OvertimeStatus.Approved)
                return WithHours("Approved", requestHours ?? 0m);

            if (status == OvertimeStatus.Rejected)
                return requestHours is > 0 ? $"Rejected ({requestHours:0.##}h)" : "Rejected";

            return "—";
        }
    }
}