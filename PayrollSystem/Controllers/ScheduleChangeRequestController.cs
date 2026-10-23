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
    public class ScheduleChangeRequestController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IScheduleChangeRequestService _service;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ScheduleChangeRequestController(
            ApplicationDbContext db,
            IScheduleChangeRequestService service,
            IApprovalWorkflowService workflowService,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _service = service;
            _workflowService = workflowService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(JustificationStatus? status = JustificationStatus.Pending)
        {
            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);

            if (!access.CanViewChangeRequestQueue)
            {
                if (await EmployeePortalHelper.IsEmployeeOnlyUserAsync(_userManager, User))
                    return RedirectToAction("ChangeRequests", "MySchedule");
                return Forbid();
            }

            ViewBag.ActiveMenu = "AttendanceMonitoring";
            ViewBag.ActiveLink = "ScheduleChangeRequest";

            var actorEmployeeId = access.EmployeeId ?? (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;

            var query = _db.ScheduleChangeRequests
                .AsNoTracking()
                .Include(r => r.Employee)
                .Include(r => r.CurrentShiftType)
                .Include(r => r.RequestedShiftType)
                .Include(r => r.WorkSchedule)
                .Include(r => r.ApprovalRequest)
                .AsQueryable();

            if (!access.IsAdmin && !access.HasGlobalChangeProcess && !access.HasGlobalChangeView)
            {
                var orgUnitIds = access.ChangeProcessOrgUnitIds.ToList();
                query = query.Where(r => orgUnitIds.Contains(r.WorkSchedule.OrganizationStructureId));
            }

            var itemsAll = await query
                .OrderByDescending(r => r.SubmittedOn)
                .Select(r => new ScheduleChangeRequestListViewModel
                {
                    Id = r.Id,
                    EmployeeId = r.EmployeeId,
                    EmployeeName = $"{r.Employee.Surname}, {r.Employee.FirstName}",
                    EmployeeIdNumber = r.Employee.EmployeeIdNumber ?? "",
                    Month = r.WorkSchedule.Month,
                    Year = r.WorkSchedule.Year,
                    Day = r.Day,
                    CurrentShiftCode = r.CurrentShiftType.Code,
                    RequestedShiftCode = r.RequestedShiftType.Code,
                    Reason = r.Reason,
                    Status = r.Status,
                    ApprovalRequestId = r.ApprovalRequestId,
                    ApprovalWorkflowStatus = r.ApprovalRequest != null ? (ApprovalRequestStatus?)r.ApprovalRequest.Status : null,
                    CurrentStepOrder = r.ApprovalRequest != null ? (int?)r.ApprovalRequest.CurrentStepOrder : null,
                    SubmittedBy = r.SubmittedBy ?? "",
                    SubmittedOn = r.SubmittedOn
                })
                .ToListAsync();

            // Derive displayed status from workflow when available
            foreach (var item in itemsAll)
            {
                if (item.ApprovalWorkflowStatus.HasValue)
                {
                    item.Status = item.ApprovalWorkflowStatus.Value switch
                    {
                        ApprovalRequestStatus.Approved => JustificationStatus.Approved,
                        ApprovalRequestStatus.Rejected => JustificationStatus.Rejected,
                        _ => JustificationStatus.Pending
                    };
                }
            }

            var pendingCount = itemsAll.Count(i => i.Status == JustificationStatus.Pending);

            var items = status.HasValue
                ? itemsAll.Where(i => i.Status == status.Value).ToList()
                : itemsAll;

            foreach (var item in items.Where(i => i.Status == JustificationStatus.Pending && i.ApprovalRequestId.HasValue))
            {
                if (!item.ApprovalRequestId.HasValue)
                    continue;

                item.CanReview = await _workflowService.CanActorActOnCurrentStepAsync(
                    item.ApprovalRequestId.Value,
                    actorEmployeeId,
                    allowAdminOverride: access.IsAdmin || access.HasGlobalChangeProcess);
            }

            ViewBag.FilterStatus = status;
            ViewBag.PendingCount = pendingCount;
            ViewBag.ApprovedCount = itemsAll.Count(i => i.Status == JustificationStatus.Approved);
            ViewBag.RejectedCount = itemsAll.Count(i => i.Status == JustificationStatus.Rejected);
            ViewBag.TotalCount = itemsAll.Count;
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> RequestForm(Guid employeeId, int month, int year, int day)
        {
            if (!await EmployeePortalHelper.CanAccessEmployeeAsync(_userManager, _db, User, employeeId))
                return Forbid();

            var schedule = await EmployeeOrganizationHelper.FindWorkScheduleForEmployeeAsync(
                _db, employeeId, month, year, approvedOnly: true);

            if (schedule == null || schedule.Status != WorkScheduleStatus.Approved)
            {
                return PartialView("Partials/_RequestScheduleChange", new ScheduleChangeRequestFormViewModel
                {
                    EmployeeId = employeeId,
                    Month = month,
                    Year = year,
                    Day = day,
                    HasPendingRequest = true,
                    StatusMessage = schedule == null
                        ? "No approved work schedule exists for this month yet."
                        : "Schedule change requests are only available after the monthly schedule is approved."
                });
            }

            var entry = await _db.ScheduleEntries
                .Include(e => e.ShiftType)
                .FirstOrDefaultAsync(e => e.WorkScheduleId == schedule.Id && e.EmployeeId == employeeId && e.Day == day);

            var latestApprovedChange = await _db.ScheduleChangeRequests
                .AsNoTracking()
                .Include(r => r.ApprovalRequest)
                .Include(r => r.RequestedShiftType)
                .Include(r => r.WorkSchedule)
                .Where(r => r.EmployeeId == employeeId &&
                            r.Day == day &&
                            r.WorkSchedule.Month == month &&
                            r.WorkSchedule.Year == year)
                .Where(r =>
                    (r.ApprovalRequest != null && r.ApprovalRequest.Status == ApprovalRequestStatus.Approved) ||
                    r.Status == JustificationStatus.Approved)
                .OrderByDescending(r => r.SubmittedOn)
                .ThenByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            var displayShift = latestApprovedChange?.RequestedShiftType ?? entry?.ShiftType;

            var pendingRequests = await _db.ScheduleChangeRequests
                .Include(r => r.ApprovalRequest)
                .Include(r => r.WorkSchedule)
                .Where(r =>
                    r.EmployeeId == employeeId &&
                    r.Day == day &&
                    r.WorkSchedule.Month == month &&
                    r.WorkSchedule.Year == year)
                .ToListAsync();

            var hasPending = pendingRequests.Any(r =>
                ApprovalRequestStatusHelper.IsPending(r.Status, r.ApprovalRequest?.Status));

            if (hasPending)
            {
                return PartialView("Partials/_RequestScheduleChange", new ScheduleChangeRequestFormViewModel
                {
                    EmployeeId = employeeId,
                    Month = month,
                    Year = year,
                    Day = day,
                    HasPendingRequest = true,
                    StatusMessage = "A pending schedule change request already exists for this day."
                });
            }

            var shiftTypes = await _db.ShiftTypes
                .Where(s => s.IsActive)
                .OrderBy(s => s.SortOrder)
                .Select(s => new ShiftTypeOptionViewModel { Id = s.Id, Code = s.Code, Label = s.Label, ColorHex = s.ColorHex })
                .ToListAsync();

            var date = new DateOnly(year, month, day);

            return PartialView("Partials/_RequestScheduleChange", new ScheduleChangeRequestFormViewModel
            {
                EmployeeId = employeeId,
                Month = month,
                Year = year,
                Day = day,
                DateDisplay = date.ToString("dddd, MMMM dd, yyyy"),
                CurrentShiftTypeId = displayShift?.Id,
                CurrentShiftCode = displayShift?.Code ?? "—",
                CurrentShiftLabel = displayShift?.Label ?? "Not assigned",
                CurrentShiftColorHex = displayShift?.ColorHex ?? "#6b7280",
                ShiftTypes = shiftTypes
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(ScheduleChangeRequestFormViewModel model)
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

            var dto = new ScheduleChangeSubmitDto
            {
                EmployeeId = model.EmployeeId,
                Month = model.Month,
                Year = model.Year,
                Day = model.Day,
                RequestedShiftTypeId = model.RequestedShiftTypeId,
                Reason = model.Reason ?? string.Empty
            };

            var currentEmployee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            var (success, message) = await _service.SubmitAsync(
                dto,
                User.Identity?.Name ?? "system",
                currentEmployee?.Id);
            return Json(new { success, message });
        }

        [HttpGet]
        public async Task<IActionResult> Process(Guid id, JustificationStatus status)
        {
            if (status != JustificationStatus.Approved && status != JustificationStatus.Rejected)
                return BadRequest("Invalid review action.");

            var request = await LoadRequestAsync(id);

            if (request == null ||
                !ApprovalRequestStatusHelper.IsPending(request.Status, request.ApprovalRequest?.Status))
                return NotFound();

            if (!await CanReviewRequestAsync(request))
                return Forbid();

            var vm = new ScheduleChangeProcessViewModel
            {
                Id = request.Id,
                ReviewAction = status,
                EmployeeName = $"{request.Employee.Surname}, {request.Employee.FirstName}",
                EmployeeIdNumber = request.Employee.EmployeeIdNumber ?? "",
                OrganizationUnitDisplay = request.WorkSchedule.OrganizationStructure?.Name ?? string.Empty,
                DateDisplay = new DateOnly(request.WorkSchedule.Year, request.WorkSchedule.Month, request.Day)
                    .ToString("dddd, MMMM dd, yyyy"),
                CurrentShiftCode = request.CurrentShiftType.Code,
                CurrentShiftLabel = request.CurrentShiftType.Label,
                CurrentShiftColorHex = request.CurrentShiftType.ColorHex,
                RequestedShiftCode = request.RequestedShiftType.Code,
                RequestedShiftLabel = request.RequestedShiftType.Label,
                RequestedShiftColorHex = request.RequestedShiftType.ColorHex,
                Reason = request.Reason,
                ApprovalRequestId = request.ApprovalRequestId
            };

            vm.Summary = await BuildProcessSummaryAsync(request);

            return PartialView("Partials/_ProcessScheduleChange", vm);
        }

        [HttpGet("/ScheduleChangeRequest/Review/{id:guid}")]
        public async Task<IActionResult> Review(Guid id)
        {
            var request = await LoadRequestAsync(id);
            if (request == null)
                return NotFound();

            await _service.SyncApprovedChangesForEmployeeMonthAsync(
                request.EmployeeId,
                request.WorkSchedule.Month,
                request.WorkSchedule.Year);
            await _service.SyncApprovedChangesForWorkScheduleAsync(request.WorkScheduleId);
            request = await LoadRequestAsync(id);
            if (request == null)
                return NotFound();

            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);

            var actorEmployeeId = access.EmployeeId ?? (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;

            // View permission rules:
            // - If this request is routed via approval workflow, allow if the actor can act on the current step.
            // - Otherwise, allow queue viewers and employees for their own requests.
            if (request.ApprovalRequestId.HasValue)
            {
                var canAct = await _workflowService.CanActorActOnCurrentStepAsync(
                    request.ApprovalRequestId.Value,
                    actorEmployeeId,
                    allowAdminOverride: access.IsAdmin || access.HasGlobalChangeProcess);

                if (!canAct &&
                    !await EmployeePortalHelper.CanAccessEmployeeAsync(_userManager, _db, User, request.EmployeeId))
                    return Forbid();
            }
            else
            {
                if (!access.CanViewChangeRequestQueue &&
                    !await EmployeePortalHelper.CanAccessEmployeeAsync(_userManager, _db, User, request.EmployeeId))
                    return Forbid();
            }

            var vm = new ScheduleChangeProcessViewModel
            {
                Id = request.Id,
                EmployeeName = $"{request.Employee.Surname}, {request.Employee.FirstName}",
                EmployeeIdNumber = request.Employee.EmployeeIdNumber ?? "",
                OrganizationUnitDisplay = request.WorkSchedule.OrganizationStructure?.Name ?? string.Empty,
                DateDisplay = new DateOnly(request.WorkSchedule.Year, request.WorkSchedule.Month, request.Day)
                    .ToString("dddd, MMMM dd, yyyy"),
                CurrentShiftCode = request.CurrentShiftType.Code,
                CurrentShiftLabel = request.CurrentShiftType.Label,
                CurrentShiftColorHex = request.CurrentShiftType.ColorHex,
                RequestedShiftCode = request.RequestedShiftType.Code,
                RequestedShiftLabel = request.RequestedShiftType.Label,
                RequestedShiftColorHex = request.RequestedShiftType.ColorHex,
                Reason = request.Reason,
                ReviewNotes = request.ReviewNotes ?? string.Empty,
                ApprovalRequestId = request.ApprovalRequestId,
                CurrentStatus = ApprovalRequestStatusHelper.ResolveEffectiveStatus(
                    request.Status,
                    request.ApprovalRequest?.Status),
                SubmittedBy = request.SubmittedBy ?? string.Empty,
                SubmittedOn = request.SubmittedOn,
                ReviewedOn = request.ReviewedOn
            };

            if (request.ApprovalRequestId.HasValue)
                vm.ApprovalTimeline = await _workflowService.GetTimelineAsync(request.ApprovalRequestId.Value);

            if (ApprovalRequestStatusHelper.IsPending(request.Status, request.ApprovalRequest?.Status) &&
                await CanReviewRequestAsync(request))
            {
                vm.CanApprove = true;
                vm.CanReject = true;
            }

            // Render as a partial so it can be shown inside the modal.
            return PartialView("Review", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(ScheduleChangeProcessViewModel model)
        {
            var request = await _db.ScheduleChangeRequests
                .AsNoTracking()
                .Include(r => r.WorkSchedule)
                .FirstOrDefaultAsync(r => r.Id == model.Id);

            if (request == null)
                return Json(new { success = false, message = "Request not found." });

            if (!await CanReviewRequestAsync(request))
                return Json(new { success = false, message = "You are not allowed to process this request." });

            var user = await _userManager.GetUserAsync(User);
            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            var actorEmployeeId = access.EmployeeId ?? (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;
            var (success, message) = await _service.ProcessAsync(
                model.Id,
                model.ReviewAction,
                model.ReviewNotes,
                user?.Id ?? "",
                User.Identity?.Name ?? "system",
                actorEmployeeId,
                access.IsAdmin || access.HasGlobalChangeProcess);

            return Json(new { success, message });
        }

        private async Task<ScheduleChangeRequest?> LoadRequestAsync(Guid id)
        {
            return await _db.ScheduleChangeRequests
                .AsNoTracking()
                .Include(r => r.Employee)
                    .ThenInclude(e => e.ApplicationUser)
                .Include(r => r.CurrentShiftType)
                .Include(r => r.RequestedShiftType)
                .Include(r => r.WorkSchedule)
                    .ThenInclude(s => s.OrganizationStructure)
                .Include(r => r.ApprovalRequest)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        private async Task<ProcessRequestSummaryViewModel> BuildProcessSummaryAsync(ScheduleChangeRequest request)
        {
            var designations = await EmployeeOrganizationHelper.GetActiveDesignationNamesAsync(
                _db, new[] { request.EmployeeId });
            designations.TryGetValue(request.EmployeeId, out var designation);

            return EmployeeDisplayHelper.BuildSummary(
                request.Employee,
                designation,
                request.WorkSchedule.OrganizationStructure?.Name ?? string.Empty,
                contextDateDisplay: new DateOnly(
                    request.WorkSchedule.Year,
                    request.WorkSchedule.Month,
                    request.Day).ToString("dddd, MMMM dd, yyyy"),
                contextDateLabel: "Schedule date");
        }

        private async Task<bool> CanReviewRequestAsync(ScheduleChangeRequest request)
        {
            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            var actorEmployeeId = access.EmployeeId ?? (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;
            if (!request.ApprovalRequestId.HasValue)
                return access.CanProcessChangeRequestsForOrgUnit(request.WorkSchedule.OrganizationStructureId);

            return await _workflowService.CanActorActOnCurrentStepAsync(
                request.ApprovalRequestId.Value,
                actorEmployeeId,
                allowAdminOverride: access.IsAdmin || access.HasGlobalChangeProcess);
        }
    }
}
