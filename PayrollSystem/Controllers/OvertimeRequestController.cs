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
    public class OvertimeRequestController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IOvertimeRequestService _service;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly DtrAttendanceCalculationService _attendance;
        private readonly UserManager<ApplicationUser> _userManager;

        public OvertimeRequestController(
            ApplicationDbContext db,
            IOvertimeRequestService service,
            IApprovalWorkflowService workflowService,
            DtrAttendanceCalculationService attendance,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _service = service;
            _workflowService = workflowService;
            _attendance = attendance;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(OvertimeStatus? status = OvertimeStatus.Pending)
        {
            if (!await CanAccessOvertimeQueueAsync())
            {
                if (await EmployeePortalHelper.IsEmployeeOnlyUserAsync(_userManager, User))
                    return RedirectToAction(nameof(MyRequests));
                return Forbid();
            }

            ViewBag.ActiveMenu = "AttendanceMonitoring";
            ViewBag.ActiveLink = "OvertimeRequest";

            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            var actorEmployeeId = access.EmployeeId ??
                (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;

            var requests = await _db.OvertimeRequests
                .AsNoTracking()
                .Include(r => r.Employee)
                .Include(r => r.ReviewedBy)
                .Include(r => r.ApprovalRequest)
                .Include(r => r.DayType)
                .OrderByDescending(r => r.SubmittedOn)
                .ToListAsync();

            var itemsAll = requests.Select(MapListItem).ToList();

            foreach (var item in itemsAll)
            {
                if (item.ApprovalWorkflowStatus.HasValue)
                {
                    item.Status = item.ApprovalWorkflowStatus.Value switch
                    {
                        ApprovalRequestStatus.Approved => OvertimeStatus.Approved,
                        ApprovalRequestStatus.Rejected => OvertimeStatus.Rejected,
                        _ => OvertimeStatus.Pending
                    };
                }
            }

            var pendingCount = itemsAll.Count(i => i.Status == OvertimeStatus.Pending);
            var items = status.HasValue
                ? itemsAll.Where(i => i.Status == status.Value).ToList()
                : itemsAll;

            foreach (var item in items.Where(i => i.Status == OvertimeStatus.Pending && i.ApprovalRequestId.HasValue))
            {
                item.CanReview = await _workflowService.CanActorActOnCurrentStepAsync(
                    item.ApprovalRequestId!.Value,
                    actorEmployeeId,
                    allowAdminOverride: access.IsAdmin || access.HasGlobalChangeProcess);
            }

            ViewBag.FilterStatus = status;
            ViewBag.PendingCount = pendingCount;
            ViewBag.ApprovedCount = itemsAll.Count(i => i.Status == OvertimeStatus.Approved);
            ViewBag.RejectedCount = itemsAll.Count(i => i.Status == OvertimeStatus.Rejected);
            ViewBag.TotalCount = itemsAll.Count;
            ViewBag.IsEmployeePortal = false;
            return View(items);
        }

        public async Task<IActionResult> MyRequests(OvertimeStatus? status = null)
        {
            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            if (employee == null)
                return RedirectToAction("AccessDenied", "Account");

            ViewBag.ActiveMenu = "OvertimeRequest";
            ViewBag.ActiveLink = "OvertimeRequest";
            ViewBag.IsEmployeePortal = true;

            var query = _db.OvertimeRequests
                .AsNoTracking()
                .Include(r => r.ApprovalRequest)
                .Include(r => r.ReviewedBy)
                .Include(r => r.DayType)
                .Where(r => r.EmployeeId == employee.Id);

            var requests = await query
                .OrderByDescending(r => r.SubmittedOn)
                .ToListAsync();

            var itemsAll = requests.Select(r =>
            {
                var item = MapListItem(r);
                item.EmployeeName = $"{employee.Surname}, {employee.FirstName}";
                item.EmployeeIdNumber = employee.EmployeeIdNumber ?? "";
                return item;
            }).ToList();

            foreach (var item in itemsAll)
            {
                if (item.ApprovalWorkflowStatus.HasValue)
                {
                    item.Status = item.ApprovalWorkflowStatus.Value switch
                    {
                        ApprovalRequestStatus.Approved => OvertimeStatus.Approved,
                        ApprovalRequestStatus.Rejected => OvertimeStatus.Rejected,
                        _ => OvertimeStatus.Pending
                    };
                }
            }

            var pendingCount = itemsAll.Count(i => i.Status == OvertimeStatus.Pending);
            var items = status.HasValue
                ? itemsAll.Where(i => i.Status == status.Value).ToList()
                : itemsAll;

            ViewBag.FilterStatus = status;
            ViewBag.PendingCount = pendingCount;
            ViewBag.ApprovedCount = itemsAll.Count(i => i.Status == OvertimeStatus.Approved);
            ViewBag.RejectedCount = itemsAll.Count(i => i.Status == OvertimeStatus.Rejected);
            ViewBag.TotalCount = itemsAll.Count;
            return View("Index", items);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            if (employee == null)
                return Forbid();

            var startDate = DateOnly.FromDateTime(DateTime.Today);
            return PartialView("Partials/_RequestOvertime",
                await BuildRequestFormAsync(employee.Id, $"{employee.Surname}, {employee.FirstName}", startDate, startDate));
        }

        [HttpGet]
        public async Task<IActionResult> RequestForm(Guid employeeId, DateOnly? date = null)
        {
            if (!await EmployeePortalHelper.CanAccessEmployeeAsync(_userManager, _db, User, employeeId))
                return Forbid();

            var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
            if (employee == null)
                return NotFound();

            var startDate = date ?? DateOnly.FromDateTime(DateTime.Today);
            var endDate = startDate;

            var overlapping = await _db.OvertimeRequests
                .Include(r => r.ApprovalRequest)
                .Where(r => r.EmployeeId == employeeId &&
                            !r.IsDeleted &&
                            r.StartDate <= endDate &&
                            r.EndDate >= startDate)
                .ToListAsync();

            if (overlapping.Any(r =>
                    ApprovalRequestStatusHelper.IsPending(r.Status, r.ApprovalRequest?.Status)))
            {
                return PartialView("Partials/_RequestOvertime", new OvertimeRequestFormViewModel
                {
                    EmployeeId = employeeId,
                    EmployeeName = $"{employee.Surname}, {employee.FirstName}",
                    StartDate = startDate,
                    EndDate = endDate,
                    HasPendingRequest = true,
                    StatusMessage = "A pending overtime request already overlaps this date."
                });
            }

            return PartialView("Partials/_RequestOvertime",
                await BuildRequestFormAsync(
                    employee.Id,
                    $"{employee.Surname}, {employee.FirstName}",
                    startDate,
                    endDate));
        }

        private async Task<OvertimeRequestFormViewModel> BuildRequestFormAsync(
            Guid employeeId,
            string employeeName,
            DateOnly startDate,
            DateOnly endDate)
        {
            var config = await _attendance.GetActiveConfigurationAsync();

            return new OvertimeRequestFormViewModel
            {
                EmployeeId = employeeId,
                EmployeeName = employeeName,
                StartDate = startDate,
                EndDate = endDate,
                HasLunchBreak = false,
                RequiresApproval = config?.RequireOvertimeApproval ?? true
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(OvertimeRequestFormViewModel model)
        {
            if (!await EmployeePortalHelper.CanAccessEmployeeAsync(_userManager, _db, User, model.EmployeeId))
                return Json(new { success = false, message = "You are not allowed to submit a request for this employee." });

            if (!ModelState.IsValid)
            {
                var validationMessage = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e))
                    ?? "Please correct the highlighted fields.";
                return Json(new { success = false, message = validationMessage });
            }

            var user = await _userManager.GetUserAsync(User);
            var currentEmployee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);

            if (!model.RequestedStartTime.HasValue || !model.RequestedEndTime.HasValue)
                return Json(new { success = false, message = "Start time and end time are required." });

            var (success, message) = await _service.SubmitAsync(
                new OvertimeSubmitDto
                {
                    EmployeeId = model.EmployeeId,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    RequestedStartTime = model.RequestedStartTime.Value,
                    RequestedEndTime = model.RequestedEndTime.Value,
                    HasLunchBreak = model.HasLunchBreak,
                    LunchStart = model.LunchStart,
                    LunchEnd = model.LunchEnd,
                    Reason = model.Reason ?? string.Empty
                },
                User.Identity?.Name ?? "system",
                user?.Id,
                currentEmployee?.Id);

            return Json(new { success, message });
        }

        [HttpGet]
        public async Task<IActionResult> Process(Guid id, OvertimeStatus status)
        {
            if (status != OvertimeStatus.Approved && status != OvertimeStatus.Rejected)
                return BadRequest("Invalid review status.");

            var request = await LoadRequestAsync(id);
            if (request == null ||
                !ApprovalRequestStatusHelper.IsPending(request.Status, request.ApprovalRequest?.Status))
                return NotFound();

            if (!await CanReviewRequestAsync(request))
                return Forbid();

            var vm = await BuildProcessViewModelAsync(request, status);
            return PartialView("Partials/_ProcessOvertime", vm);
        }

        [HttpGet("/OvertimeRequest/Review/{id:guid}")]
        public async Task<IActionResult> Review(Guid id)
        {
            var request = await LoadRequestAsync(id);
            if (request == null)
                return NotFound();

            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            var actorEmployeeId = access.EmployeeId ??
                (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;

            if (request.ApprovalRequestId.HasValue)
            {
                var canAct = await _workflowService.CanActorActOnCurrentStepAsync(
                    request.ApprovalRequestId.Value,
                    actorEmployeeId,
                    allowAdminOverride: access.IsAdmin || access.HasGlobalChangeProcess);

                if (!canAct &&
                    !await EmployeePortalHelper.CanAccessEmployeeAsync(_userManager, _db, User, request.EmployeeId) &&
                    !await CanAccessOvertimeQueueAsync())
                    return Forbid();
            }
            else if (!await CanAccessOvertimeQueueAsync() &&
                     !await EmployeePortalHelper.CanAccessEmployeeAsync(_userManager, _db, User, request.EmployeeId))
            {
                return Forbid();
            }

            var vm = await BuildProcessViewModelAsync(request);
            vm.CurrentStatus = ApprovalRequestStatusHelper.ResolveEffectiveStatus(
                request.Status, request.ApprovalRequest?.Status);
            vm.ReviewNotes = request.ReviewNotes ?? string.Empty;
            vm.SubmittedBy = request.SubmittedBy ?? string.Empty;
            vm.SubmittedOn = request.SubmittedOn;
            vm.ReviewedOn = request.ReviewedOn;
            vm.IsProcessedToPayroll = request.IsProcessedToPayroll;
            vm.ApprovedStartTime = request.ApprovedStartTime;
            vm.ApprovedEndTime = request.ApprovedEndTime;
            vm.ApprovedHasLunchBreak = request.ApprovedHasLunchBreak ?? request.HasLunchBreak;
            vm.ApprovedLunchStart = request.ApprovedLunchStart ?? request.LunchStart;
            vm.ApprovedLunchEnd = request.ApprovedLunchEnd ?? request.LunchEnd;

            if (request.ApprovalRequestId.HasValue)
                vm.ApprovalTimeline = await _workflowService.GetTimelineAsync(request.ApprovalRequestId.Value);

            if (ApprovalRequestStatusHelper.IsPending(request.Status, request.ApprovalRequest?.Status) &&
                await CanReviewRequestAsync(request))
            {
                vm.CanApprove = true;
                vm.CanReject = true;
            }

            return PartialView("Review", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(OvertimeRequestProcessViewModel model)
        {
            var request = await _db.OvertimeRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == model.Id);

            if (request == null)
                return Json(new { success = false, message = "Overtime request not found." });

            if (!await CanReviewRequestAsync(request))
                return Json(new { success = false, message = "You are not allowed to process this request." });

            var user = await _userManager.GetUserAsync(User);
            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            var actorEmployeeId = access.EmployeeId ??
                (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;

            var (success, message) = await _service.ProcessAsync(
                model.Id,
                model.ReviewAction,
                model.ReviewNotes,
                user?.Id ?? "",
                User.Identity?.Name ?? "system",
                approvedStartTime: null,
                approvedEndTime: null,
                approvedHasLunchBreak: null,
                approvedLunchStart: null,
                approvedLunchEnd: null,
                actorEmployeeId,
                access.IsAdmin || access.HasGlobalChangeProcess);

            return Json(new { success, message });
        }

        [HttpGet]
        public async Task<IActionResult> Print(Guid id)
        {
            var request = await LoadRequestAsync(id);
            if (request == null)
                return NotFound();

            if (!await CanAccessOvertimeQueueAsync() &&
                !await EmployeePortalHelper.CanAccessEmployeeAsync(_userManager, _db, User, request.EmployeeId))
                return Forbid();

            var config = await _attendance.GetActiveConfigurationAsync();
            var designations = await EmployeeOrganizationHelper.GetActiveDesignationNamesAsync(
                _db, new[] { request.EmployeeId });
            designations.TryGetValue(request.EmployeeId, out var designation);

            var start = request.ApprovedStartTime ?? request.RequestedStartTime;
            var end = request.ApprovedEndTime ?? request.RequestedEndTime;
            var netPerDay = OvertimeRequest.ComputeHours(
                start,
                end,
                request.EffectiveHasLunchBreak,
                request.EffectiveLunchStart,
                request.EffectiveLunchEnd,
                request.EffectiveBreakMinutes);
            var grossPerDay = OvertimeRequest.ComputeGrossHours(start, end);
            var timeDisplay = FormatTimeRange(start, end);
            var lunchDisplay = request.LunchBreakDisplay;
            var effectiveStatus = ApprovalRequestStatusHelper.ResolveEffectiveStatus(
                request.Status, request.ApprovalRequest?.Status);

            var addressParts = new[]
            {
                config?.CompanyAddress1,
                config?.CompanyAddress2,
                config?.CompanyCity,
                config?.CompanyProvince,
                config?.CompanyZipCode
            }.Where(s => !string.IsNullOrWhiteSpace(s));

            var vm = new OvertimeRequestPrintViewModel
            {
                Id = request.Id,
                CompanyName = string.IsNullOrWhiteSpace(config?.CompanyName)
                    ? "Company"
                    : config!.CompanyName,
                CompanyAddress = string.Join(", ", addressParts),
                CompanyLogoUrl = config?.CompanyLogoPath,
                EmployeeName = $"{request.Employee.Surname}, {request.Employee.FirstName}",
                EmployeeIdNumber = request.Employee.EmployeeIdNumber ?? "",
                Designation = designation,
                OrganizationUnitDisplay = await GetOrganizationUnitDisplayAsync(request.EmployeeId),
                DateRangeDisplay = OvertimeRequest.FormatDateRange(request.StartDate, request.EndDate),
                DailyTimeDisplay = timeDisplay,
                LunchBreakDisplay = lunchDisplay,
                GrossHoursPerDay = grossPerDay,
                NetHoursPerDay = netPerDay,
                DayCount = request.DayCount,
                TotalNetHours = netPerDay * request.DayCount,
                Reason = request.Reason,
                StatusDisplay = EnumHelper.GetDisplayName(effectiveStatus),
                SubmittedBy = request.SubmittedBy ?? "-",
                SubmittedOn = request.SubmittedOn,
                ReviewedByName = request.ReviewedBy?.UserName,
                ReviewedOn = request.ReviewedOn,
                ReviewNotes = request.ReviewNotes,
                DayTypeName = request.DayType?.Name,
                Days = request.EnumerateDays().Select(d => new OvertimeRequestPrintDayRow
                {
                    Date = d,
                    DayName = d.ToString("dddd"),
                    TimeDisplay = timeDisplay,
                    LunchBreakDisplay = lunchDisplay,
                    NetHours = netPerDay
                }).ToList()
            };

            return View(vm);
        }

        private static OvertimeRequestListViewModel MapListItem(OvertimeRequest r)
        {
            return new OvertimeRequestListViewModel
            {
                Id = r.Id,
                EmployeeId = r.EmployeeId,
                EmployeeName = r.Employee != null
                    ? $"{r.Employee.Surname}, {r.Employee.FirstName}"
                    : "",
                EmployeeIdNumber = r.Employee?.EmployeeIdNumber ?? "",
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                DateRangeDisplay = OvertimeRequest.FormatDateRange(r.StartDate, r.EndDate),
                RequestedTimes = FormatTimeRange(r.RequestedStartTime, r.RequestedEndTime),
                ApprovedTimes = r.ApprovedStartTime.HasValue && r.ApprovedEndTime.HasValue
                    ? FormatTimeRange(r.ApprovedStartTime.Value, r.ApprovedEndTime.Value)
                    : null,
                HasLunchBreak = r.HasLunchBreak,
                LunchBreakDisplay = OvertimeRequest.FormatLunchBreak(
                    r.HasLunchBreak, r.LunchStart, r.LunchEnd, r.BreakMinutes),
                HoursPerDay = r.HoursPerDay,
                DayCount = r.DayCount,
                RequestedHours = r.RequestedHours,
                ApprovedHours = r.ApprovedHours,
                Reason = r.Reason,
                Status = r.Status,
                ApprovalRequestId = r.ApprovalRequestId,
                ApprovalWorkflowStatus = r.ApprovalRequest != null
                    ? (ApprovalRequestStatus?)r.ApprovalRequest.Status
                    : null,
                CurrentStepOrder = r.ApprovalRequest?.CurrentStepOrder,
                IsProcessedToPayroll = r.IsProcessedToPayroll,
                SubmittedBy = r.SubmittedBy ?? "-",
                SubmittedOn = r.SubmittedOn,
                ReviewedByName = r.ReviewedBy?.UserName,
                ReviewedOn = r.ReviewedOn,
                DayTypeName = r.DayType?.Name
            };
        }

        private static string FormatTimeRange(TimeOnly start, TimeOnly end) =>
            $"{start.ToString("h:mm tt")} – {end.ToString("h:mm tt")}";

        private async Task<OvertimeRequestProcessViewModel> BuildProcessViewModelAsync(
            OvertimeRequest request,
            OvertimeStatus? reviewAction = null)
        {
            var vm = new OvertimeRequestProcessViewModel
            {
                Id = request.Id,
                ReviewAction = reviewAction ?? OvertimeStatus.Pending,
                EmployeeName = $"{request.Employee.Surname}, {request.Employee.FirstName}",
                EmployeeIdNumber = request.Employee.EmployeeIdNumber ?? "",
                OrganizationUnitDisplay = await GetOrganizationUnitDisplayAsync(request.EmployeeId),
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                DateDisplay = OvertimeRequest.FormatDateRange(request.StartDate, request.EndDate),
                RequestedTimes = FormatTimeRange(request.RequestedStartTime, request.RequestedEndTime),
                HasLunchBreak = request.HasLunchBreak,
                LunchBreakDisplay = OvertimeRequest.FormatLunchBreak(
                    request.HasLunchBreak, request.LunchStart, request.LunchEnd, request.BreakMinutes),
                GrossHoursPerDay = request.GrossHoursPerDay,
                HoursPerDay = request.HoursPerDay,
                DayCount = request.DayCount,
                RequestedHours = request.RequestedHours,
                Reason = request.Reason,
                DayTypeName = request.DayType?.Name,
                ApprovalRequestId = request.ApprovalRequestId
            };

            vm.Summary = await BuildProcessSummaryAsync(request);
            return vm;
        }

        private async Task<OvertimeRequest?> LoadRequestAsync(Guid id)
        {
            return await _db.OvertimeRequests
                .AsNoTracking()
                .Include(r => r.Employee)
                    .ThenInclude(e => e.ApplicationUser)
                .Include(r => r.ApprovalRequest)
                .Include(r => r.ReviewedBy)
                .Include(r => r.DayType)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        private async Task<ProcessRequestSummaryViewModel> BuildProcessSummaryAsync(OvertimeRequest request)
        {
            var designations = await EmployeeOrganizationHelper.GetActiveDesignationNamesAsync(
                _db, new[] { request.EmployeeId });
            designations.TryGetValue(request.EmployeeId, out var designation);

            return EmployeeDisplayHelper.BuildSummary(
                request.Employee,
                designation,
                await GetOrganizationUnitDisplayAsync(request.EmployeeId),
                contextDateDisplay: OvertimeRequest.FormatDateRange(request.StartDate, request.EndDate),
                contextDateLabel: "OT dates");
        }

        private async Task<bool> CanReviewRequestAsync(OvertimeRequest request)
        {
            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            var actorEmployeeId = access.EmployeeId ??
                (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;

            if (!request.ApprovalRequestId.HasValue)
                return await IsAdminOrHrAsync();

            return await _workflowService.CanActorActOnCurrentStepAsync(
                request.ApprovalRequestId.Value,
                actorEmployeeId,
                allowAdminOverride: access.IsAdmin || access.HasGlobalChangeProcess);
        }

        private async Task<bool> CanAccessOvertimeQueueAsync()
        {
            if (await IsAdminOrHrAsync())
                return true;

            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            if (access.IsAdmin || access.HasGlobalChangeView || access.HasGlobalChangeProcess)
                return true;

            if (PermissionHelper.HasPermission(User, "OVERTIME_REQUEST_VIEW"))
                return true;

            var actorEmployeeId = access.EmployeeId ??
                (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;
            if (!actorEmployeeId.HasValue)
                return false;

            var orgIds = await EmployeeOrganizationHelper.GetActiveFunctionalRoleOrgUnitIdsAsync(
                _db, actorEmployeeId.Value, OrganizationFunctionalRole.DtrApprover);
            if (orgIds.Count > 0)
                return true;

            orgIds = await EmployeeOrganizationHelper.GetActiveFunctionalRoleOrgUnitIdsAsync(
                _db, actorEmployeeId.Value, OrganizationFunctionalRole.ScheduleApprover);
            return orgIds.Count > 0;
        }

        private async Task<bool> IsAdminOrHrAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return false;

            var roles = await _userManager.GetRolesAsync(user);
            return roles.Contains("Admin", StringComparer.OrdinalIgnoreCase) ||
                   roles.Any(r => string.Equals(r, "HR", StringComparison.OrdinalIgnoreCase));
        }

        private async Task<string> GetOrganizationUnitDisplayAsync(Guid employeeId)
        {
            var orgUnitId = await EmployeeOrganizationHelper.GetPrimaryOrganizationStructureIdAsync(_db, employeeId);
            if (!orgUnitId.HasValue)
                return string.Empty;

            return await _db.OrganizationStructures
                .AsNoTracking()
                .Where(o => o.Id == orgUnitId.Value)
                .Select(o => o.Name)
                .FirstOrDefaultAsync() ?? string.Empty;
        }
    }
}
