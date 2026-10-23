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
    public class ExchangeRestDayRequestController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly DtrAttendanceCalculationService _attendanceCalculation;
        private readonly IExchangeRestDayRequestService _service;
        private readonly IApprovalWorkflowService _workflowService;

        public ExchangeRestDayRequestController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            DtrAttendanceCalculationService attendanceCalculation,
            IExchangeRestDayRequestService service,
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
            ViewBag.ActiveLink = "ExchangeRestDayRequest";

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

            ViewBag.ActiveMenu = "ExchangeRestDayRequest";
            ViewBag.ActiveLink = "ExchangeRestDayRequest";
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

            return PartialView("Partials/_RequestExchangeRestDay", await BuildFormAsync(employee));
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
                isLeave = shift.IsLeave,
                display = FormatShiftDisplay(shift.Code, shift.Label)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(ExchangeRestDayRequestFormViewModel model)
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

            if (!model.FirstRestShiftTypeId.HasValue ||
                !model.SecondRestShiftTypeId.HasValue ||
                !model.FirstWorkShiftTypeId.HasValue ||
                !model.SecondWorkShiftTypeId.HasValue)
                return Json(new { success = false, message = "Schedule details are incomplete. Select rest days that form a valid WORK ↔ OFF exchange." });

            var currentEmployee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            var dto = new ExchangeRestDaySubmitDto
            {
                OrganizationStructureId = model.OrganizationStructureId,
                DateFiled = model.DateFiled,
                FirstEmployeeId = model.FirstEmployeeId,
                SecondEmployeeId = model.SecondEmployeeId.Value,
                FirstRestDate = model.FirstRestDate,
                SecondRestDate = model.SecondRestDate,
                FirstRestShiftTypeId = model.FirstRestShiftTypeId.Value,
                SecondRestShiftTypeId = model.SecondRestShiftTypeId.Value,
                FirstWorkShiftTypeId = model.FirstWorkShiftTypeId.Value,
                SecondWorkShiftTypeId = model.SecondWorkShiftTypeId.Value,
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

            return PartialView("Partials/_ProcessExchangeRestDay", BuildProcessViewModel(request, status));
        }

        [HttpGet("/ExchangeRestDayRequest/Review/{id:guid}")]
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
        public async Task<IActionResult> Process(ExchangeRestDayProcessViewModel model)
        {
            var loaded = await LoadRequestAsync(model.Id);
            if (loaded == null || !await CanReviewRequestAsync(loaded))
                return Json(new { success = false, message = "You are not allowed to process this request." });

            var user = await _userManager.GetUserAsync(User);
            var actorEmployeeId = (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;

            var (success, message) = await _service.ProcessAsync(
                model.Id,
                model.ReviewAction,
                model.ReviewNotes,
                user?.Id ?? string.Empty,
                User.Identity?.Name ?? "system",
                actorEmployeeId,
                await IsAdminOrHrAsync());

            return Json(new { success, message });
        }

        [HttpGet]
        public async Task<IActionResult> Print(Guid id)
        {
            var request = await LoadRequestAsync(id);
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

            var vm = new ExchangeRestDayRequestPrintViewModel
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
                FirstRestDate = request.FirstRestDate,
                FirstRestShiftDisplay = FormatShiftDisplay(
                    request.FirstRestShiftType.Code, request.FirstRestShiftType.Label),
                FirstWorkShiftDisplay = FormatShiftDisplay(
                    request.FirstWorkShiftType.Code, request.FirstWorkShiftType.Label),
                SecondEmployeeName = FormatEmployeeName(request.SecondEmployee),
                SecondEmployeeIdNumber = request.SecondEmployee.EmployeeIdNumber ?? string.Empty,
                SecondRestDate = request.SecondRestDate,
                SecondRestShiftDisplay = FormatShiftDisplay(
                    request.SecondRestShiftType.Code, request.SecondRestShiftType.Label),
                SecondWorkShiftDisplay = FormatShiftDisplay(
                    request.SecondWorkShiftType.Code, request.SecondWorkShiftType.Label)
            };

            return View(vm);
        }

        private async Task<List<ExchangeRestDayRequestListViewModel>> LoadListAsync(
            Guid? employeeId,
            Guid? actorEmployeeId,
            bool allowAdminOverride)
        {
            var query = _db.ExchangeRestDayRequests
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
                .Select(r => new ExchangeRestDayRequestListViewModel
                {
                    Id = r.Id,
                    DepartmentName = r.OrganizationStructure != null ? r.OrganizationStructure.Name : string.Empty,
                    DateFiled = r.DateFiled,
                    FirstEmployeeId = r.FirstEmployeeId,
                    FirstEmployeeName = $"{r.FirstEmployee.Surname}, {r.FirstEmployee.FirstName}",
                    SecondEmployeeId = r.SecondEmployeeId,
                    SecondEmployeeName = $"{r.SecondEmployee.Surname}, {r.SecondEmployee.FirstName}",
                    FirstRestDate = r.FirstRestDate,
                    SecondRestDate = r.SecondRestDate,
                    FirstRestShiftCode = r.FirstRestShiftType.Code,
                    SecondRestShiftCode = r.SecondRestShiftType.Code,
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

        private async Task<ExchangeRestDayRequestFormViewModel> BuildFormAsync(Employee employee)
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
                .Select(e => new ExchangeRestDayEmployeeOptionViewModel
                {
                    Id = e.Id,
                    Name = $"{e.Surname}, {e.FirstName}",
                    EmployeeIdNumber = e.EmployeeIdNumber ?? string.Empty
                })
                .ToListAsync();

            var workflow = await _workflowService.GetActiveWorkflowAsync(WorkflowProcessType.ExchangeRestDay);

            return new ExchangeRestDayRequestFormViewModel
            {
                FirstEmployeeId = employee.Id,
                FirstEmployeeName = $"{employee.Surname}, {employee.FirstName}",
                FirstEmployeeIdNumber = employee.EmployeeIdNumber ?? string.Empty,
                OrganizationStructureId = orgUnitId,
                DepartmentName = departmentName,
                DateFiled = DateOnly.FromDateTime(DateTime.Today),
                FirstRestDate = DateOnly.FromDateTime(DateTime.Today),
                SecondRestDate = DateOnly.FromDateTime(DateTime.Today),
                ColleagueOptions = colleagues,
                RequiresApproval = workflow != null
            };
        }

        private async Task<ExchangeRestDayRequest?> LoadRequestAsync(Guid id)
        {
            return await _db.ExchangeRestDayRequests
                .AsNoTracking()
                .Include(r => r.FirstEmployee)
                .Include(r => r.SecondEmployee)
                .Include(r => r.OrganizationStructure)
                .Include(r => r.FirstRestShiftType)
                .Include(r => r.SecondRestShiftType)
                .Include(r => r.FirstWorkShiftType)
                .Include(r => r.SecondWorkShiftType)
                .Include(r => r.ApprovalRequest)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        }

        private static ExchangeRestDayProcessViewModel BuildProcessViewModel(
            ExchangeRestDayRequest request,
            JustificationStatus reviewAction)
        {
            return new ExchangeRestDayProcessViewModel
            {
                Id = request.Id,
                ReviewAction = reviewAction,
                DepartmentName = request.OrganizationStructure?.Name ?? string.Empty,
                DateFiled = request.DateFiled,
                Reason = request.Reason,
                FirstEmployeeName = FormatEmployeeName(request.FirstEmployee),
                FirstEmployeeIdNumber = request.FirstEmployee.EmployeeIdNumber ?? string.Empty,
                FirstRestDate = request.FirstRestDate,
                FirstRestShiftDisplay = FormatShiftDisplay(
                    request.FirstRestShiftType.Code, request.FirstRestShiftType.Label),
                FirstWorkShiftDisplay = FormatShiftDisplay(
                    request.FirstWorkShiftType.Code, request.FirstWorkShiftType.Label),
                SecondEmployeeName = FormatEmployeeName(request.SecondEmployee),
                SecondEmployeeIdNumber = request.SecondEmployee.EmployeeIdNumber ?? string.Empty,
                SecondRestDate = request.SecondRestDate,
                SecondRestShiftDisplay = FormatShiftDisplay(
                    request.SecondRestShiftType.Code, request.SecondRestShiftType.Label),
                SecondWorkShiftDisplay = FormatShiftDisplay(
                    request.SecondWorkShiftType.Code, request.SecondWorkShiftType.Label),
                ApprovalRequestId = request.ApprovalRequestId,
                SubmittedBy = request.SubmittedBy ?? string.Empty,
                SubmittedOn = request.SubmittedOn
            };
        }

        private async Task<bool> CanReviewRequestAsync(ExchangeRestDayRequest request)
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

        private async Task<bool> CanViewRequestAsync(ExchangeRestDayRequest request)
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
