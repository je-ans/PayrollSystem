using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Services;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;
using PayrollSystem.ViewModels.WorkSchedule;

namespace PayrollSystem.Controllers
{
    [Authorize]
    public class MyScheduleController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWorkScheduleService _scheduleService;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IScheduleChangeRequestService _scheduleChangeService;

        public MyScheduleController(
            ApplicationDbContext db,
            IWorkScheduleService scheduleService,
            IApprovalWorkflowService workflowService,
            UserManager<ApplicationUser> userManager,
            IScheduleChangeRequestService scheduleChangeService)
        {
            _db = db;
            _scheduleService = scheduleService;
            _workflowService = workflowService;
            _userManager = userManager;
            _scheduleChangeService = scheduleChangeService;
        }

        public async Task<IActionResult> Index(int? year, int? organizationStructureId)
        {
            ViewBag.ActiveMenu = "EmployeeSchedule";

            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            if (employee == null)
                return RedirectToAction("AccessDenied", "Account");

            var orgUnitIds = await EmployeeOrganizationHelper.GetActiveOrganizationStructureIdsAsync(_db, employee.Id);
            var primaryOrgUnitId = await EmployeeOrganizationHelper.GetPrimaryOrganizationStructureIdAsync(_db, employee.Id);

            var now = DateTime.Now;
            var resolvedOrgUnitId = organizationStructureId is > 0 && orgUnitIds.Contains(organizationStructureId.Value)
                ? organizationStructureId.Value
                : primaryOrgUnitId ?? orgUnitIds.FirstOrDefault();

            var model = await _scheduleService.GetOverviewViewModelAsync(
                year ?? now.Year,
                resolvedOrgUnitId > 0 ? resolvedOrgUnitId : null);

            model.OrganizationUnits = model.OrganizationUnits
                .Where(u => orgUnitIds.Contains(u.Id))
                .ToList();
            model.IsReadOnly = true;
            model.DetailController = "MySchedule";

            return View("~/Views/WorkSchedule/Index.cshtml", model);
        }

        public async Task<IActionResult> Detail(int? month, int? year, int? organizationStructureId)
        {
            ViewBag.ActiveMenu = "EmployeeSchedule";

            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            if (employee == null)
                return RedirectToAction("AccessDenied", "Account");

            var orgUnitIds = await EmployeeOrganizationHelper.GetActiveOrganizationStructureIdsAsync(_db, employee.Id);
            var primaryOrgUnitId = await EmployeeOrganizationHelper.GetPrimaryOrganizationStructureIdAsync(_db, employee.Id);
            if (orgUnitIds.Count == 0)
            {
                ViewBag.IsReadOnly = true;
                ViewBag.CurrentEmployeeId = employee.Id;
                ViewBag.DetailController = "MySchedule";
                return View("~/Views/WorkSchedule/Detail.cshtml", new WorkScheduleViewModel
                {
                    Month = month ?? DateTime.Now.Month,
                    Year = year ?? DateTime.Now.Year
                });
            }

            var now = DateTime.Now;
            var resolvedMonth = month ?? now.Month;
            var resolvedYear = year ?? now.Year;
            var requestedOrgId = organizationStructureId is > 0 ? organizationStructureId.Value : 0;

            var isWorkflowApproverAccess = requestedOrgId > 0 &&
                await _workflowService.IsPendingApproverForWorkScheduleAsync(
                    employee.Id,
                    resolvedMonth,
                    resolvedYear,
                    requestedOrgId);

            int resolvedOrgUnitId;
            if (requestedOrgId > 0 && (orgUnitIds.Contains(requestedOrgId) || isWorkflowApproverAccess))
                resolvedOrgUnitId = requestedOrgId;
            else
                resolvedOrgUnitId = primaryOrgUnitId ?? orgUnitIds[0];

            var model = await _scheduleService.GetViewModelAsync(
                resolvedMonth,
                resolvedYear,
                resolvedOrgUnitId);

            if (model.ScheduleId > 0)
            {
                await _scheduleChangeService.SyncApprovedChangesForWorkScheduleAsync(model.ScheduleId);
                model = await _scheduleService.GetViewModelAsync(
                    resolvedMonth,
                    resolvedYear,
                    resolvedOrgUnitId);
            }

            var scheduleOrgId = model.OrganizationStructureId;
            var scheduleOrgDisplay = model.OrganizationUnitDisplay;

            model.OrganizationUnits = model.OrganizationUnits
                .Where(u => orgUnitIds.Contains(u.Id))
                .ToList();

            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, User);
            access.EmployeeId ??= employee.Id;

            WorkScheduleAccessHelper.ApplyToViewModel(model, access);

            if (isWorkflowApproverAccess && scheduleOrgId > 0)
            {
                WorkScheduleAccessHelper.PreserveOrganizationContext(
                    model, scheduleOrgId, scheduleOrgDisplay);
            }

            await WorkScheduleAccessHelper.ApplyWorkflowApproverFlagsAsync(model, access, _workflowService);

            model.IsEditable = false;
            model.CanSubmit = false;

            if (access.CanEditOrgUnit(model.OrganizationStructureId) &&
                model.Status == WorkScheduleStatus.Approved)
            {
                model.CanReopen = true;
            }
            else
            {
                model.CanReopen = false;
            }

            if (model.ApprovalRequestId.HasValue)
                ViewBag.ApprovalTimeline = await _workflowService.GetTimelineAsync(model.ApprovalRequestId.Value);

            ViewBag.IsReadOnly = true;
            if (isWorkflowApproverAccess)
                ViewBag.WorkflowSuccessRedirect = Url.Action("Index", "PendingApproval");
            ViewBag.CurrentEmployeeId = employee.Id;
            ViewBag.DetailController = "MySchedule";
            return View("~/Views/WorkSchedule/Detail.cshtml", model);
        }

        public async Task<IActionResult> Print(int? month, int? year, int? organizationStructureId)
        {
            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            if (employee == null)
                return RedirectToAction("AccessDenied", "Account");

            var orgUnitIds = await EmployeeOrganizationHelper.GetActiveOrganizationStructureIdsAsync(_db, employee.Id);
            if (orgUnitIds.Count == 0)
                return NotFound();

            var primaryOrgUnitId = await EmployeeOrganizationHelper.GetPrimaryOrganizationStructureIdAsync(_db, employee.Id);
            var resolvedOrgUnitId = organizationStructureId is > 0 && orgUnitIds.Contains(organizationStructureId.Value)
                ? organizationStructureId.Value
                : primaryOrgUnitId ?? orgUnitIds[0];

            var now = DateTime.Now;
            var model = await _scheduleService.GetViewModelAsync(
                month ?? now.Month,
                year ?? now.Year,
                resolvedOrgUnitId);

            model.OrganizationUnits = model.OrganizationUnits
                .Where(u => orgUnitIds.Contains(u.Id))
                .ToList();

            return View("~/Views/WorkSchedule/Print.cshtml", model);
        }

        public async Task<IActionResult> ChangeRequests(JustificationStatus? status = null)
        {
            ViewBag.ActiveMenu = "EmployeeSchedule";

            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            if (employee == null)
                return RedirectToAction("AccessDenied", "Account");

            var query = _db.ScheduleChangeRequests
                .AsNoTracking()
                .Include(r => r.CurrentShiftType)
                .Include(r => r.RequestedShiftType)
                .Include(r => r.WorkSchedule)
                .Where(r => r.EmployeeId == employee.Id);

            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            var items = await query
                .OrderByDescending(r => r.SubmittedOn)
                .Select(r => new ScheduleChangeRequestListViewModel
                {
                    Id = r.Id,
                    EmployeeId = r.EmployeeId,
                    EmployeeName = employee.FullName,
                    EmployeeIdNumber = employee.EmployeeIdNumber ?? "",
                    Month = r.WorkSchedule.Month,
                    Year = r.WorkSchedule.Year,
                    Day = r.Day,
                    CurrentShiftCode = r.CurrentShiftType.Code,
                    RequestedShiftCode = r.RequestedShiftType.Code,
                    Reason = r.Reason,
                    Status = r.Status,
                    ApprovalRequestId = r.ApprovalRequestId,
                    SubmittedBy = r.SubmittedBy ?? "",
                    SubmittedOn = r.SubmittedOn
                })
                .ToListAsync();

            ViewBag.FilterStatus = status;
            ViewBag.PendingCount = await _db.ScheduleChangeRequests
                .CountAsync(r => r.EmployeeId == employee.Id && r.Status == JustificationStatus.Pending);

            return View(items);
        }
    }
}
