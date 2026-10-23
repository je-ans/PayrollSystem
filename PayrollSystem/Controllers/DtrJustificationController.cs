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
    public class DtrJustificationController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ITimeLogJustificationService _service;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly DtrAttendanceCalculationService _attendanceCalculation;

        public DtrJustificationController(
            ApplicationDbContext db,
            ITimeLogJustificationService service,
            IApprovalWorkflowService workflowService,
            UserManager<ApplicationUser> userManager,
            DtrAttendanceCalculationService attendanceCalculation)
        {
            _db = db;
            _service = service;
            _workflowService = workflowService;
            _userManager = userManager;
            _attendanceCalculation = attendanceCalculation;
        }

        public async Task<IActionResult> Index(JustificationStatus? status = JustificationStatus.Pending)
        {
            if (!await CanAccessJustificationQueueAsync())
                return Forbid();

            ViewBag.ActiveMenu = "AttendanceMonitoring";
            ViewBag.ActiveLink = "DtrJustification";

            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            var actorEmployeeId = access.EmployeeId ??
                (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;

            var query = _db.TimeLogJustifications
                .AsNoTracking()
                .Include(j => j.Employee)
                .Include(j => j.ReviewedBy)
                .Include(j => j.ApprovalRequest)
                .AsQueryable();

            var requests = await query
                .OrderByDescending(j => j.SubmittedOn)
                .ToListAsync();

            var itemsAll = new List<DtrJustificationListViewModel>();
            foreach (var j in requests)
            {
                itemsAll.Add(new DtrJustificationListViewModel
                {
                    Id = j.Id,
                    EmployeeId = j.EmployeeId,
                    EmployeeName = $"{j.Employee.Surname}, {j.Employee.FirstName}",
                    EmployeeIdNumber = j.Employee.EmployeeIdNumber ?? "",
                    EntryDate = j.EntryDate,
                    Justification = j.Justification,
                    RequestedTimes = await FormatRequestedTimesAsync(j),
                    Status = j.Status,
                    ApprovalRequestId = j.ApprovalRequestId,
                    ApprovalWorkflowStatus = j.ApprovalRequest != null ? (ApprovalRequestStatus?)j.ApprovalRequest.Status : null,
                    CurrentStepOrder = j.ApprovalRequest != null ? (int?)j.ApprovalRequest.CurrentStepOrder : null,
                    SubmittedBy = j.SubmittedBy ?? "-",
                    SubmittedOn = j.SubmittedOn,
                    ReviewedByName = j.ReviewedBy?.UserName,
                    ReviewedOn = j.ReviewedOn
                });
            }

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
                item.CanReview = await _workflowService.CanActorActOnCurrentStepAsync(
                    item.ApprovalRequestId!.Value,
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
        public async Task<IActionResult> Process(Guid id, JustificationStatus status)
        {
            if (status != JustificationStatus.Approved && status != JustificationStatus.Rejected)
                return BadRequest("Invalid review status.");

            var request = await LoadRequestAsync(id);
            if (request == null || request.Status != JustificationStatus.Pending)
                return NotFound();

            if (!await CanReviewRequestAsync(request))
                return Forbid();

            var vm = new DtrJustificationProcessViewModel
            {
                Id = request.Id,
                ReviewAction = status,
                EmployeeName = $"{request.Employee.Surname}, {request.Employee.FirstName}",
                EmployeeIdNumber = request.Employee.EmployeeIdNumber ?? "",
                OrganizationUnitDisplay = await GetOrganizationUnitDisplayAsync(request.EmployeeId),
                EntryDate = request.EntryDate,
                DateDisplay = request.EntryDate.ToDateTime(new TimeOnly(0, 0)).ToString("dddd, MMMM dd, yyyy"),
                Justification = request.Justification,
                RequestedTimes = await FormatRequestedTimesAsync(request),
                ApprovalRequestId = request.ApprovalRequestId
            };

            var biometricLogs = await _db.BiometricLogs
                .Where(b => b.EmployeeId == request.EmployeeId && DateOnly.FromDateTime(b.Timestamp) == request.EntryDate)
                .OrderBy(b => b.Timestamp)
                .Select(b => b.Timestamp.ToString("hh:mm tt"))
                .ToListAsync();
            vm.BiometricLogs = biometricLogs;
            vm.Summary = await BuildProcessSummaryAsync(request);

            return PartialView("Partials/_ProcessJustification", vm);
        }

        [HttpGet("/DtrJustification/Review/{id:guid}")]
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
                    !await CanAccessJustificationQueueAsync())
                    return Forbid();
            }
            else if (!await CanAccessJustificationQueueAsync() &&
                     !await EmployeePortalHelper.CanAccessEmployeeAsync(_userManager, _db, User, request.EmployeeId))
            {
                return Forbid();
            }

            var vm = new DtrJustificationProcessViewModel
            {
                Id = request.Id,
                EmployeeName = $"{request.Employee.Surname}, {request.Employee.FirstName}",
                EmployeeIdNumber = request.Employee.EmployeeIdNumber ?? "",
                OrganizationUnitDisplay = await GetOrganizationUnitDisplayAsync(request.EmployeeId),
                EntryDate = request.EntryDate,
                DateDisplay = request.EntryDate.ToDateTime(new TimeOnly(0, 0)).ToString("dddd, MMMM dd, yyyy"),
                Justification = request.Justification,
                RequestedTimes = await FormatRequestedTimesAsync(request),
                ReviewNotes = request.ReviewNotes ?? string.Empty,
                ApprovalRequestId = request.ApprovalRequestId,
                CurrentStatus = request.Status,
                SubmittedBy = request.SubmittedBy ?? string.Empty,
                SubmittedOn = request.SubmittedOn,
                ReviewedOn = request.ReviewedOn
            };

            var biometricLogs = await _db.BiometricLogs
                .Where(b => b.EmployeeId == request.EmployeeId && DateOnly.FromDateTime(b.Timestamp) == request.EntryDate)
                .OrderBy(b => b.Timestamp)
                .Select(b => b.Timestamp.ToString("hh:mm tt"))
                .ToListAsync();
            vm.BiometricLogs = biometricLogs;

            if (request.ApprovalRequestId.HasValue)
                vm.ApprovalTimeline = await _workflowService.GetTimelineAsync(request.ApprovalRequestId.Value);

            if (request.Status == JustificationStatus.Pending && await CanReviewRequestAsync(request))
            {
                vm.CanApprove = true;
                vm.CanReject = true;
            }

            return PartialView("Review", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(DtrJustificationProcessViewModel model)
        {
            var request = await _db.TimeLogJustifications
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == model.Id);

            if (request == null)
                return Json(new { success = false, message = "Justification request not found." });

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
                actorEmployeeId,
                access.IsAdmin || access.HasGlobalChangeProcess);

            return Json(new { success, message });
        }

        private async Task<TimeLogJustification?> LoadRequestAsync(Guid id)
        {
            return await _db.TimeLogJustifications
                .AsNoTracking()
                .Include(j => j.Employee)
                    .ThenInclude(e => e.ApplicationUser)
                .FirstOrDefaultAsync(j => j.Id == id);
        }

        private async Task<ProcessRequestSummaryViewModel> BuildProcessSummaryAsync(TimeLogJustification request)
        {
            var designations = await EmployeeOrganizationHelper.GetActiveDesignationNamesAsync(
                _db, new[] { request.EmployeeId });
            designations.TryGetValue(request.EmployeeId, out var designation);

            return EmployeeDisplayHelper.BuildSummary(
                request.Employee,
                designation,
                await GetOrganizationUnitDisplayAsync(request.EmployeeId),
                contextDateDisplay: request.EntryDate.ToString("dddd, MMMM dd, yyyy"),
                contextDateLabel: "Date covered");
        }

        private async Task<bool> CanReviewRequestAsync(TimeLogJustification request)
        {
            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            var actorEmployeeId = access.EmployeeId ??
                (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;

            if (!request.ApprovalRequestId.HasValue)
            {
                return await IsAdminOrHrAsync();
            }

            return await _workflowService.CanActorActOnCurrentStepAsync(
                request.ApprovalRequestId.Value,
                actorEmployeeId,
                allowAdminOverride: access.IsAdmin || access.HasGlobalChangeProcess);
        }

        private async Task<bool> CanAccessJustificationQueueAsync()
        {
            if (await IsAdminOrHrAsync())
                return true;

            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            if (access.IsAdmin || access.HasGlobalChangeView || access.HasGlobalChangeProcess)
                return true;

            var actorEmployeeId = access.EmployeeId ??
                (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;
            if (!actorEmployeeId.HasValue)
                return false;

            var orgIds = await EmployeeOrganizationHelper.GetActiveFunctionalRoleOrgUnitIdsAsync(
                _db, actorEmployeeId.Value, OrganizationFunctionalRole.DtrApprover);
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

        private async Task<string> FormatRequestedTimesAsync(TimeLogJustification request)
        {
            var shift = await _attendanceCalculation.GetEmployeeShiftTypeForDayAsync(request.EmployeeId, request.EntryDate);
            var config = await _attendanceCalculation.GetActiveConfigurationAsync();
            var schedule = _attendanceCalculation.ResolveSchedule(config, shift);
            var layout = DtrPunchResolutionService.GetLayout(schedule);
            return DtrPunchResolutionService.FormatJustificationRequestedTimes(request, layout);
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
