using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Services;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels.WorkSchedule;

namespace PayrollSystem.Controllers
{
    [Authorize]
    public class WorkScheduleController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWorkScheduleService _service;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IScheduleChangeRequestService _scheduleChangeService;

        public WorkScheduleController(
            ApplicationDbContext db,
            IWorkScheduleService service,
            IApprovalWorkflowService workflowService,
            UserManager<ApplicationUser> userManager,
            IScheduleChangeRequestService scheduleChangeService)
        {
            _db = db;
            _service = service;
            _workflowService = workflowService;
            _userManager = userManager;
            _scheduleChangeService = scheduleChangeService;
        }

        public async Task<IActionResult> Index(int? year, int? organizationStructureId)
        {
            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            if (!access.CanAccessWorkScheduleModule)
                return RedirectToAction("AccessDenied", "Account");

            if (!access.HasGlobalView && access.CanManageSchedule)
                ViewBag.ActiveMenu = "EmployeeSchedule";
            else
            {
                ViewBag.ActiveMenu = "AttendanceMonitoring";
                ViewBag.ActiveLink = "WorkSchedule";
            }

            var now = DateTime.Now;
            var vm = await _service.GetOverviewViewModelAsync(
                year ?? now.Year,
                organizationStructureId);

            var orgBeforeFilter = vm.OrganizationStructureId;
            WorkScheduleAccessHelper.ApplyToOverviewViewModel(vm, access);

            if (vm.OrganizationStructureId != orgBeforeFilter && vm.OrganizationStructureId > 0)
            {
                vm = await _service.GetOverviewViewModelAsync(year ?? now.Year, vm.OrganizationStructureId);
                WorkScheduleAccessHelper.ApplyToOverviewViewModel(vm, access);
            }

            if (vm.OrganizationStructureId > 0 && !access.CanViewOrgUnit(vm.OrganizationStructureId))
            {
                if (!access.IsAdmin && !access.HasGlobalView)
                {
                    var fallbackOrgId = access.ViewOrgUnitIds.FirstOrDefault();
                    if (fallbackOrgId > 0 && fallbackOrgId != vm.OrganizationStructureId)
                    {
                        return RedirectToAction(nameof(Index), new
                        {
                            year = year ?? now.Year,
                            organizationStructureId = fallbackOrgId
                        });
                    }
                }

                return RedirectToAction("AccessDenied", "Account");
            }

            vm.DetailController = "WorkSchedule";
            return View(vm);
        }

        public async Task<IActionResult> Detail(int? month, int? year, int? organizationStructureId)
        {
            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);

            var now = DateTime.Now;
            var resolvedMonth = month ?? now.Month;
            var resolvedYear = year ?? now.Year;
            var resolvedOrgId = organizationStructureId ?? 0;

            var isWorkflowApproverAccess = access.EmployeeId.HasValue &&
                resolvedOrgId > 0 &&
                await _workflowService.IsPendingApproverForWorkScheduleAsync(
                    access.EmployeeId.Value,
                    resolvedMonth,
                    resolvedYear,
                    resolvedOrgId);

            var hasOrgAssignmentAccess = access.EmployeeId.HasValue &&
                await WorkScheduleAccessHelper.HasActiveOrgAssignmentAsync(
                    _db, access.EmployeeId.Value, resolvedOrgId);

            if (access.CanAccessWorkScheduleModule &&
                !access.IsAdmin &&
                !access.HasGlobalView &&
                resolvedOrgId > 0 &&
                !access.CanViewOrgUnit(resolvedOrgId) &&
                !isWorkflowApproverAccess &&
                !hasOrgAssignmentAccess)
            {
                var fallbackOrgId = access.ViewOrgUnitIds.FirstOrDefault();
                if (fallbackOrgId > 0 && fallbackOrgId != resolvedOrgId)
                {
                    return RedirectToAction(nameof(Detail), new
                    {
                        month = resolvedMonth,
                        year = resolvedYear,
                        organizationStructureId = fallbackOrgId
                    });
                }
            }

            var canViewDetail = access.CanAccessWorkScheduleModule ||
                isWorkflowApproverAccess ||
                hasOrgAssignmentAccess;

            if (!canViewDetail)
                return RedirectToAction("AccessDenied", "Account");

            if (!access.HasGlobalView && access.CanManageSchedule)
                ViewBag.ActiveMenu = "EmployeeSchedule";
            else if (!access.CanAccessWorkScheduleModule)
                ViewBag.ActiveMenu = "PendingApprovals";
            else
            {
                ViewBag.ActiveMenu = "AttendanceMonitoring";
                ViewBag.ActiveLink = "WorkSchedule";
            }

            var vm = await _service.GetViewModelAsync(
                resolvedMonth,
                resolvedYear,
                organizationStructureId);

            if (vm.ScheduleId > 0)
            {
                await _scheduleChangeService.SyncApprovedChangesForWorkScheduleAsync(vm.ScheduleId);
                vm = await _service.GetViewModelAsync(
                    resolvedMonth,
                    resolvedYear,
                    organizationStructureId);
            }

            if (vm.OrganizationStructureId > 0 &&
                !access.CanViewOrgUnit(vm.OrganizationStructureId) &&
                !isWorkflowApproverAccess &&
                !hasOrgAssignmentAccess)
                return RedirectToAction("AccessDenied", "Account");

            var scheduleOrgId = vm.OrganizationStructureId;
            var scheduleOrgDisplay = vm.OrganizationUnitDisplay;

            WorkScheduleAccessHelper.ApplyToViewModel(vm, access);

            if (resolvedOrgId > 0 &&
                (access.CanViewOrgUnit(resolvedOrgId) ||
                 isWorkflowApproverAccess ||
                 hasOrgAssignmentAccess ||
                 access.IsAdmin ||
                 access.HasGlobalView))
            {
                var display = vm.OrganizationUnits.FirstOrDefault(u => u.Id == resolvedOrgId)?.DisplayText
                    ?? scheduleOrgDisplay;
                WorkScheduleAccessHelper.PreserveOrganizationContext(vm, resolvedOrgId, display);
            }
            else if ((isWorkflowApproverAccess || hasOrgAssignmentAccess) && scheduleOrgId > 0)
            {
                WorkScheduleAccessHelper.PreserveOrganizationContext(
                    vm, scheduleOrgId, scheduleOrgDisplay);
            }

            await WorkScheduleAccessHelper.ApplyWorkflowApproverFlagsAsync(vm, access, _workflowService);

            if (isWorkflowApproverAccess)
            {
                vm.IsEditable = false;
                vm.CanSubmit = false;
            }

            if (!access.CanAccessWorkScheduleModule)
            {
                vm.IsEditable = false;
                vm.CanSubmit = false;
                if (!access.CanEditOrgUnit(vm.OrganizationStructureId))
                    vm.CanReopen = false;

                ViewBag.IsReadOnly = true;
                ViewBag.WorkflowSuccessRedirect = Url.Action("Index", "PendingApproval");
            }
            else if (!access.CanEditOrgUnit(vm.OrganizationStructureId))
            {
                vm.IsEditable = false;
                vm.CanSubmit = false;
                vm.CanReopen = false;
            }
            else if (vm.Status == WorkScheduleStatus.Approved)
            {
                vm.CanReopen = true;
            }

            if (vm.ApprovalRequestId.HasValue)
                ViewBag.ApprovalTimeline = await _workflowService.GetTimelineAsync(vm.ApprovalRequestId.Value);

            if (vm.OrganizationStructureId > 0)
            {
                ViewBag.CanAssignFunctionalRoles = access.CanAssignFunctionalRolesForOrg(vm.OrganizationStructureId);
                ViewBag.UnitHasScheduler = await OrganizationScopeHelper.HasActiveFunctionalRoleHolderAsync(
                    _db,
                    vm.OrganizationStructureId,
                    OrganizationFunctionalRole.Scheduler);
            }

            ViewBag.DetailController = hasOrgAssignmentAccess && !access.CanAccessWorkScheduleModule
                ? "MySchedule"
                : "WorkSchedule";
            return View(vm);
        }

        public async Task<IActionResult> Print(int? month, int? year, int? organizationStructureId)
        {
            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            if (!access.CanAccessWorkScheduleModule)
                return RedirectToAction("AccessDenied", "Account");

            var now = DateTime.Now;
            var vm = await _service.GetViewModelAsync(
                month ?? now.Month,
                year ?? now.Year,
                organizationStructureId);

            if (vm.OrganizationStructureId > 0 && !access.CanViewOrgUnit(vm.OrganizationStructureId))
                return RedirectToAction("AccessDenied", "Account");

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveEntry([FromBody] SaveEntryDto dto)
        {
            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            var orgUnitId = await ResolveOrgUnitIdAsync(dto.OrganizationStructureId, access, access.EditOrgUnitIds);

            if (orgUnitId <= 0)
                return DenyJson("Organization unit is required.");

            if (!access.CanEditOrgUnit(orgUnitId))
                return DenyJson("You are not allowed to edit this schedule.");

            dto.OrganizationStructureId = orgUnitId;

            if (!ModelState.IsValid)
                return BadRequest(new SaveEntryResult { Success = false, Error = "Invalid data." });

            var result = await _service.SaveEntryAsync(dto, User.Identity?.Name ?? "system");
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit([FromBody] ScheduleWorkflowDto dto)
        {
            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            var orgUnitId = await ResolveOrgUnitIdAsync(dto.OrganizationStructureId, access, access.SubmitOrgUnitIds);

            if (orgUnitId <= 0)
                return DenyJson("Organization unit is required.");

            if (!access.CanSubmitOrgUnit(orgUnitId))
                return DenyJson("You are not allowed to submit this schedule.");

            dto.OrganizationStructureId = orgUnitId;

            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            var (success, error) = await _service.SubmitAsync(
                dto,
                User.Identity?.Name ?? "system",
                employee?.Id);
            return success ? Ok(new { success = true }) : BadRequest(new { error });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve([FromBody] ScheduleWorkflowDto dto)
        {
            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            var orgUnitId = await ResolveOrgUnitIdAsync(dto.OrganizationStructureId, access, access.ApproveOrgUnitIds);

            if (orgUnitId <= 0)
                return DenyJson("Organization unit is required.");

            dto.OrganizationStructureId = orgUnitId;

            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            if (!await CanProcessApprovalAsync(dto, orgUnitId, access, employee?.Id))
                return DenyJson("You are not allowed to approve this schedule.");

            var allowOverride = access.IsAdmin || access.HasGlobalApprove;
            var (success, error) = await _service.ApproveAsync(
                dto,
                User.Identity?.Name ?? "system",
                employee?.Id,
                allowOverride);
            return success ? Ok(new { success = true }) : BadRequest(new { error });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject([FromBody] ScheduleRejectDto dto)
        {
            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            var orgUnitId = await ResolveOrgUnitIdAsync(dto.OrganizationStructureId, access, access.ApproveOrgUnitIds);

            if (orgUnitId <= 0)
                return DenyJson("Organization unit is required.");

            dto.OrganizationStructureId = orgUnitId;

            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            if (!await CanProcessApprovalAsync(dto, orgUnitId, access, employee?.Id))
                return DenyJson("You are not allowed to reject this schedule.");

            var allowOverride = access.IsAdmin || access.HasGlobalApprove;
            var (success, error) = await _service.RejectAsync(
                dto,
                User.Identity?.Name ?? "system",
                employee?.Id,
                allowOverride);
            return success ? Ok(new { success = true }) : BadRequest(new { error });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reopen([FromBody] ScheduleWorkflowDto dto)
        {
            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            var orgUnitId = await ResolveOrgUnitIdAsync(dto.OrganizationStructureId, access, access.EditOrgUnitIds);

            if (orgUnitId <= 0)
                return DenyJson("Organization unit is required.");

            if (!access.CanEditOrgUnit(orgUnitId))
                return DenyJson("You are not allowed to reopen this schedule.");

            dto.OrganizationStructureId = orgUnitId;

            var (success, error) = await _service.ReopenAsync(dto, User.Identity?.Name ?? "system");
            return success ? Ok(new { success = true }) : BadRequest(new { error });
        }

        private async Task<int> ResolveOrgUnitIdAsync(
            int requestedOrgUnitId,
            WorkScheduleAccess access,
            HashSet<int> roleScopedOrgUnitIds)
        {
            if (requestedOrgUnitId > 0)
                return requestedOrgUnitId;

            if (roleScopedOrgUnitIds.Count == 1)
                return roleScopedOrgUnitIds.First();

            if (access.EmployeeId.HasValue)
            {
                var assignmentIds = await EmployeeOrganizationHelper
                    .GetActiveOrganizationStructureIdsAsync(_db, access.EmployeeId.Value);

                var matchedRoleUnit = assignmentIds.FirstOrDefault(id => roleScopedOrgUnitIds.Contains(id));
                if (matchedRoleUnit > 0)
                    return matchedRoleUnit;

                return assignmentIds.FirstOrDefault();
            }

            return 0;
        }

        private async Task<bool> CanProcessApprovalAsync(
            ScheduleWorkflowDto dto,
            int orgUnitId,
            WorkScheduleAccess access,
            Guid? actorEmployeeId)
        {
            if (access.CanApproveOrgUnit(orgUnitId))
                return true;

            var schedule = await _db.WorkSchedules
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Month == dto.Month &&
                    s.Year == dto.Year &&
                    s.OrganizationStructureId == orgUnitId);

            if (schedule?.ApprovalRequestId == null || !actorEmployeeId.HasValue)
                return false;

            return await _workflowService.CanActorActOnCurrentStepAsync(
                schedule.ApprovalRequestId.Value,
                actorEmployeeId);
        }

        private IActionResult DenyJson(string message) =>
            new JsonResult(new { success = false, error = message })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
    }
}
