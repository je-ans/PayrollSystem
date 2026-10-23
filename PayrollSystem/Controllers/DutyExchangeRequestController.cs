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
    public class DutyExchangeRequestController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly DtrAttendanceCalculationService _attendanceCalculation;
        private readonly IDutyExchangeRequestService _service;
        private readonly IApprovalWorkflowService _workflowService;

        public DutyExchangeRequestController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            DtrAttendanceCalculationService attendanceCalculation,
            IDutyExchangeRequestService service,
            IApprovalWorkflowService workflowService)
        {
            _db = db;
            _userManager = userManager;
            _attendanceCalculation = attendanceCalculation;
            _service = service;
            _workflowService = workflowService;
        }

        public async Task<IActionResult> Index(JustificationStatus? status = null)
        {
            if (!await IsAdminOrHrAsync())
                return Forbid();

            ViewBag.ActiveMenu = "AttendanceMonitoring";
            ViewBag.ActiveLink = "DutyExchangeRequest";

            var actorEmployeeId = (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;
            var itemsAll = await LoadListAsync(null, actorEmployeeId, allowAdminOverride: true);

            ViewBag.PendingCount = itemsAll.Count(i => i.Status == JustificationStatus.Pending);
            ViewBag.ApprovedCount = itemsAll.Count(i => i.Status == JustificationStatus.Approved);
            ViewBag.RejectedCount = itemsAll.Count(i => i.Status == JustificationStatus.Rejected);
            ViewBag.TotalCount = itemsAll.Count;

            var items = status.HasValue
                ? itemsAll.Where(i => i.Status == status.Value).ToList()
                : itemsAll;

            ViewBag.FilterStatus = status;
            return View(items);
        }

        public async Task<IActionResult> MyRequests()
        {
            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            if (employee == null)
                return RedirectToAction("AccessDenied", "Account");

            ViewBag.ActiveMenu = "DutyExchangeRequest";
            ViewBag.ActiveLink = "DutyExchangeRequest";
            ViewBag.IsEmployeePortal = true;

            var items = await LoadListAsync(employee.Id, employee.Id, allowAdminOverride: false);
            ViewBag.TotalCount = items.Count;
            return View("Index", items);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            if (employee == null)
                return Forbid();

            return PartialView("Partials/_RequestDutyExchange", await BuildFormAsync(employee));
        }

        [HttpGet]
        public async Task<IActionResult> EmployeeSchedule(Guid employeeId, DateOnly date)
        {
            if (!await CanAccessRequestPartyAsync(employeeId))
                return Forbid();

            var shift = await _attendanceCalculation.GetEmployeeShiftTypeForDayAsync(employeeId, date, approvedOnly: true)
                        ?? await _attendanceCalculation.GetEmployeeShiftTypeForDayAsync(employeeId, date, approvedOnly: false);

            if (shift == null)
                return Json(new { success = false, message = "No schedule found for this date." });

            return Json(new
            {
                success = true,
                shiftTypeId = shift.Id,
                code = shift.Code,
                label = shift.Label,
                display = FormatShiftDisplay(shift.Code, shift.Label)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(DutyExchangeRequestFormViewModel model)
        {
            if (!await EmployeePortalHelper.CanAccessEmployeeAsync(_userManager, _db, User, model.FirstEmployeeId))
                return Json(new { success = false, message = "You are not allowed to submit this request." });

            if (!ModelState.IsValid)
            {
                var validationMessage = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e))
                    ?? "Please complete all required fields.";
                return Json(new { success = false, message = validationMessage });
            }

            if (!model.SecondEmployeeId.HasValue)
                return Json(new { success = false, message = "Please select the second employee." });

            if (!model.FirstOriginalShiftTypeId.HasValue ||
                !model.FirstExchangedShiftTypeId.HasValue ||
                !model.SecondOriginalShiftTypeId.HasValue ||
                !model.SecondExchangedShiftTypeId.HasValue)
                return Json(new { success = false, message = "Schedule details are incomplete. Please select dates with assigned shifts." });

            var currentEmployee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            var dto = new DutyExchangeSubmitDto
            {
                OrganizationStructureId = model.OrganizationStructureId,
                DateFiled = model.DateFiled,
                FirstEmployeeId = model.FirstEmployeeId,
                FirstOriginalDate = model.FirstOriginalDate,
                FirstOriginalShiftTypeId = model.FirstOriginalShiftTypeId.Value,
                FirstExchangedDate = model.FirstExchangedDate,
                FirstExchangedShiftTypeId = model.FirstExchangedShiftTypeId.Value,
                SecondEmployeeId = model.SecondEmployeeId.Value,
                SecondOriginalDate = model.SecondOriginalDate,
                SecondOriginalShiftTypeId = model.SecondOriginalShiftTypeId.Value,
                SecondExchangedDate = model.SecondExchangedDate,
                SecondExchangedShiftTypeId = model.SecondExchangedShiftTypeId.Value,
                Reason = model.Reason
            };

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

            return PartialView("Partials/_ProcessDutyExchange", BuildProcessViewModel(request, status));
        }

        [HttpGet("/DutyExchangeRequest/Review/{id:guid}")]
        public async Task<IActionResult> Review(Guid id)
        {
            var request = await LoadRequestAsync(id);
            if (request == null)
                return NotFound();

            if (!await CanViewRequestAsync(request))
                return Forbid();

            var vm = BuildProcessViewModel(request, JustificationStatus.Approved);
            vm.ReviewNotes = request.ReviewNotes ?? string.Empty;
            vm.CurrentStatus = ApprovalRequestStatusHelper.ResolveEffectiveStatus(
                request.Status,
                request.ApprovalRequest?.Status);
            vm.ReviewedOn = request.ReviewedOn;

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
        public async Task<IActionResult> Process(DutyExchangeProcessViewModel model)
        {
            var request = await _db.DutyExchangeRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == model.Id && !r.IsDeleted);

            if (request == null)
                return Json(new { success = false, message = "Request not found." });

            var loaded = await LoadRequestAsync(model.Id);
            if (loaded == null || !await CanReviewRequestAsync(loaded))
                return Json(new { success = false, message = "You are not allowed to process this request." });

            var user = await _userManager.GetUserAsync(User);
            var actorEmployeeId = (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;
            var allowAdminOverride = await IsAdminOrHrAsync();

            var (success, message) = await _service.ProcessAsync(
                model.Id,
                model.ReviewAction,
                model.ReviewNotes,
                user?.Id ?? string.Empty,
                User.Identity?.Name ?? "system",
                actorEmployeeId,
                allowAdminOverride);

            return Json(new { success, message });
        }

        [HttpGet]
        public async Task<IActionResult> Print(Guid id)
        {
            var request = await _db.DutyExchangeRequests
                .AsNoTracking()
                .Include(r => r.FirstEmployee)
                .Include(r => r.SecondEmployee)
                .Include(r => r.OrganizationStructure)
                .Include(r => r.FirstOriginalShiftType)
                .Include(r => r.FirstExchangedShiftType)
                .Include(r => r.SecondOriginalShiftType)
                .Include(r => r.SecondExchangedShiftType)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

            if (request == null)
                return NotFound();

            if (!await CanViewRequestAsync(request))
                return Forbid();

            var config = await _db.PayrollConfigurations
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.EffectiveDate)
                .FirstOrDefaultAsync();

            var department = request.OrganizationStructure?.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(department))
            {
                var orgUnitId = await EmployeeOrganizationHelper.GetPrimaryOrganizationStructureIdAsync(
                    _db, request.FirstEmployeeId);
                if (orgUnitId.HasValue)
                {
                    department = await _db.OrganizationStructures
                        .AsNoTracking()
                        .Where(o => o.Id == orgUnitId.Value)
                        .Select(o => o.Name)
                        .FirstOrDefaultAsync() ?? string.Empty;
                }
            }

            var vm = new DutyExchangeRequestPrintViewModel
            {
                CompanyName = string.IsNullOrWhiteSpace(config?.CompanyName) ? "Company" : config.CompanyName,
                CompanyAddress = string.Join(", ", new[]
                {
                    config?.CompanyAddress1,
                    config?.CompanyAddress2,
                    config?.CompanyCity,
                    config?.CompanyProvince,
                    config?.CompanyZipCode
                }.Where(s => !string.IsNullOrWhiteSpace(s))),
                CompanyLogoUrl = config?.CompanyLogoPath,
                DepartmentName = department,
                DateFiled = request.DateFiled,
                Reason = request.Reason,
                SubmittedBy = request.SubmittedBy ?? "-",
                SubmittedOn = request.SubmittedOn,
                FirstEmployeeName = FormatEmployeeName(request.FirstEmployee),
                FirstEmployeeIdNumber = request.FirstEmployee.EmployeeIdNumber ?? string.Empty,
                FirstOriginalDate = request.FirstOriginalDate,
                FirstOriginalShiftDisplay = FormatShiftDisplay(
                    request.FirstOriginalShiftType.Code, request.FirstOriginalShiftType.Label),
                FirstExchangedDate = request.FirstExchangedDate,
                FirstExchangedShiftDisplay = FormatShiftDisplay(
                    request.FirstExchangedShiftType.Code, request.FirstExchangedShiftType.Label),
                SecondEmployeeName = FormatEmployeeName(request.SecondEmployee),
                SecondEmployeeIdNumber = request.SecondEmployee.EmployeeIdNumber ?? string.Empty,
                SecondOriginalDate = request.SecondOriginalDate,
                SecondOriginalShiftDisplay = FormatShiftDisplay(
                    request.SecondOriginalShiftType.Code, request.SecondOriginalShiftType.Label),
                SecondExchangedDate = request.SecondExchangedDate,
                SecondExchangedShiftDisplay = FormatShiftDisplay(
                    request.SecondExchangedShiftType.Code, request.SecondExchangedShiftType.Label)
            };

            return View(vm);
        }

        private async Task<List<DutyExchangeRequestListViewModel>> LoadListAsync(
            Guid? employeeId,
            Guid? actorEmployeeId,
            bool allowAdminOverride)
        {
            var query = _db.DutyExchangeRequests
                .AsNoTracking()
                .Include(r => r.ApprovalRequest)
                .Where(r => !r.IsDeleted);

            if (employeeId.HasValue)
            {
                query = query.Where(r =>
                    r.FirstEmployeeId == employeeId.Value ||
                    r.SecondEmployeeId == employeeId.Value);
            }

            var items = await query
                .OrderByDescending(r => r.SubmittedOn)
                .Select(r => new DutyExchangeRequestListViewModel
                {
                    Id = r.Id,
                    DepartmentName = r.OrganizationStructure != null ? r.OrganizationStructure.Name : string.Empty,
                    DateFiled = r.DateFiled,
                    FirstEmployeeId = r.FirstEmployeeId,
                    FirstEmployeeName = $"{r.FirstEmployee.Surname}, {r.FirstEmployee.FirstName}",
                    SecondEmployeeId = r.SecondEmployeeId,
                    SecondEmployeeName = $"{r.SecondEmployee.Surname}, {r.SecondEmployee.FirstName}",
                    FirstOriginalDate = r.FirstOriginalDate,
                    SecondOriginalDate = r.SecondOriginalDate,
                    FirstOriginalShiftCode = r.FirstOriginalShiftType.Code,
                    SecondOriginalShiftCode = r.SecondOriginalShiftType.Code,
                    Reason = r.Reason,
                    Status = r.Status,
                    ApprovalRequestId = r.ApprovalRequestId,
                    ApprovalWorkflowStatus = r.ApprovalRequest != null ? (ApprovalRequestStatus?)r.ApprovalRequest.Status : null,
                    CurrentStepOrder = r.ApprovalRequest != null ? r.ApprovalRequest.CurrentStepOrder : null,
                    SubmittedBy = r.SubmittedBy ?? "-",
                    SubmittedOn = r.SubmittedOn
                })
                .ToListAsync();

            foreach (var item in items)
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

            foreach (var item in items.Where(i => i.Status == JustificationStatus.Pending && i.ApprovalRequestId.HasValue))
            {
                item.CanReview = await _workflowService.CanActorActOnCurrentStepAsync(
                    item.ApprovalRequestId!.Value,
                    actorEmployeeId,
                    allowAdminOverride);
            }

            return items;
        }

        private async Task<DutyExchangeRequestFormViewModel> BuildFormAsync(Employee employee)
        {
            var orgUnitId = await EmployeeOrganizationHelper.GetPrimaryOrganizationStructureIdAsync(_db, employee.Id);
            var departmentName = orgUnitId.HasValue
                ? await _db.OrganizationStructures
                    .AsNoTracking()
                    .Where(o => o.Id == orgUnitId.Value)
                    .Select(o => o.Name)
                    .FirstOrDefaultAsync() ?? string.Empty
                : string.Empty;

            var colleaguesQuery = _db.Employees
                .AsNoTracking()
                .Where(e => !e.IsDeleted && e.IsActive && e.Id != employee.Id);

            if (orgUnitId.HasValue)
                colleaguesQuery = colleaguesQuery.FilterByActiveOrganizationUnit(_db, orgUnitId.Value);

            var colleagues = await colleaguesQuery
                .OrderBy(e => e.Surname).ThenBy(e => e.FirstName)
                .Select(e => new DutyExchangeEmployeeOptionViewModel
                {
                    Id = e.Id,
                    Name = $"{e.Surname}, {e.FirstName}",
                    EmployeeIdNumber = e.EmployeeIdNumber ?? string.Empty
                })
                .ToListAsync();

            var workflow = await _workflowService.GetActiveWorkflowAsync(WorkflowProcessType.DutyExchange);

            return new DutyExchangeRequestFormViewModel
            {
                FirstEmployeeId = employee.Id,
                FirstEmployeeName = $"{employee.Surname}, {employee.FirstName}",
                FirstEmployeeIdNumber = employee.EmployeeIdNumber ?? string.Empty,
                OrganizationStructureId = orgUnitId,
                DepartmentName = departmentName,
                DateFiled = DateOnly.FromDateTime(DateTime.Today),
                FirstOriginalDate = DateOnly.FromDateTime(DateTime.Today),
                SecondOriginalDate = DateOnly.FromDateTime(DateTime.Today),
                ColleagueOptions = colleagues,
                RequiresApproval = workflow != null
            };
        }

        private async Task<DutyExchangeRequest?> LoadRequestAsync(Guid id)
        {
            return await _db.DutyExchangeRequests
                .AsNoTracking()
                .Include(r => r.FirstEmployee)
                .Include(r => r.SecondEmployee)
                .Include(r => r.OrganizationStructure)
                .Include(r => r.FirstOriginalShiftType)
                .Include(r => r.FirstExchangedShiftType)
                .Include(r => r.SecondOriginalShiftType)
                .Include(r => r.SecondExchangedShiftType)
                .Include(r => r.ApprovalRequest)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        }

        private static DutyExchangeProcessViewModel BuildProcessViewModel(
            DutyExchangeRequest request,
            JustificationStatus reviewAction)
        {
            return new DutyExchangeProcessViewModel
            {
                Id = request.Id,
                ReviewAction = reviewAction,
                DepartmentName = request.OrganizationStructure?.Name ?? string.Empty,
                DateFiled = request.DateFiled,
                Reason = request.Reason,
                FirstEmployeeName = FormatEmployeeName(request.FirstEmployee),
                FirstEmployeeIdNumber = request.FirstEmployee.EmployeeIdNumber ?? string.Empty,
                FirstOriginalDate = request.FirstOriginalDate,
                FirstOriginalShiftDisplay = FormatShiftDisplay(
                    request.FirstOriginalShiftType.Code, request.FirstOriginalShiftType.Label),
                FirstExchangedDate = request.FirstExchangedDate,
                FirstExchangedShiftDisplay = FormatShiftDisplay(
                    request.FirstExchangedShiftType.Code, request.FirstExchangedShiftType.Label),
                SecondEmployeeName = FormatEmployeeName(request.SecondEmployee),
                SecondEmployeeIdNumber = request.SecondEmployee.EmployeeIdNumber ?? string.Empty,
                SecondOriginalDate = request.SecondOriginalDate,
                SecondOriginalShiftDisplay = FormatShiftDisplay(
                    request.SecondOriginalShiftType.Code, request.SecondOriginalShiftType.Label),
                SecondExchangedDate = request.SecondExchangedDate,
                SecondExchangedShiftDisplay = FormatShiftDisplay(
                    request.SecondExchangedShiftType.Code, request.SecondExchangedShiftType.Label),
                ApprovalRequestId = request.ApprovalRequestId,
                SubmittedBy = request.SubmittedBy ?? string.Empty,
                SubmittedOn = request.SubmittedOn
            };
        }

        private async Task<bool> CanReviewRequestAsync(DutyExchangeRequest request)
        {
            if (!request.ApprovalRequestId.HasValue)
                return await IsAdminOrHrAsync();

            var actorEmployeeId = (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;
            return await _workflowService.CanActorActOnCurrentStepAsync(
                request.ApprovalRequestId.Value,
                actorEmployeeId,
                allowAdminOverride: await IsAdminOrHrAsync());
        }

        private async Task<bool> CanAccessRequestPartyAsync(Guid employeeId)
        {
            if (!await EmployeePortalHelper.IsEmployeeOnlyUserAsync(_userManager, User))
                return true;

            var current = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            if (current == null)
                return false;

            if (current.Id == employeeId)
                return true;

            var orgUnitId = await EmployeeOrganizationHelper.GetPrimaryOrganizationStructureIdAsync(_db, current.Id);
            if (!orgUnitId.HasValue)
                return false;

            return await _db.Employees
                .AsNoTracking()
                .Where(e => e.Id == employeeId)
                .FilterByActiveOrganizationUnit(_db, orgUnitId.Value)
                .AnyAsync();
        }

        private async Task<bool> CanViewRequestAsync(DutyExchangeRequest request)
        {
            if (await IsAdminOrHrAsync())
                return true;

            if (!await EmployeePortalHelper.IsEmployeeOnlyUserAsync(_userManager, User))
                return true;

            var current = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            if (current == null)
                return false;

            return current.Id == request.FirstEmployeeId || current.Id == request.SecondEmployeeId;
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

        private static string FormatEmployeeName(Employee employee) =>
            $"{employee.Surname}, {employee.FirstName}";

        private static string FormatShiftDisplay(string code, string label) =>
            string.IsNullOrWhiteSpace(label) ? code : $"{code} — {label}";
    }
}
