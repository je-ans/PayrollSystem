using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Models.Workflow;
using PayrollSystem.Services;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    [Authorize]
    public class UndertimeRequestController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPayrollTimeCardFromDtrService _dtrService;
        private readonly IUndertimeRequestService _service;
        private readonly IApprovalWorkflowService _workflowService;

        public UndertimeRequestController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IPayrollTimeCardFromDtrService dtrService,
            IUndertimeRequestService service,
            IApprovalWorkflowService workflowService)
        {
            _db = db;
            _userManager = userManager;
            _dtrService = dtrService;
            _service = service;
            _workflowService = workflowService;
        }

        public async Task<IActionResult> Index(JustificationStatus? status = null)
        {
            if (!await IsAdminOrHrAsync())
                return Forbid();

            ViewBag.ActiveMenu = "AttendanceMonitoring";
            ViewBag.ActiveLink = "UndertimeRequest";

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

            ViewBag.ActiveMenu = "UndertimeRequest";
            ViewBag.ActiveLink = "UndertimeRequest";
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

            return PartialView("Partials/_RequestUndertime",
                await BuildFormAsync(employee.Id, employee.FirstName, employee.Surname, employee.EmployeeIdNumber));
        }

        [HttpGet]
        public async Task<IActionResult> SuggestedHours(Guid employeeId, DateOnly date)
        {
            if (!await EmployeePortalHelper.CanAccessEmployeeAsync(_userManager, _db, User, employeeId))
                return Forbid();

            var minutes = await _dtrService.GetUndertimeMinutesForDateAsync(employeeId, date);
            decimal? hours = minutes > 0 ? Math.Round(minutes / 60m, 2) : null;

            return Json(new { success = true, hours, minutes });
        }

        [HttpGet]
        public async Task<IActionResult> RequestForm(Guid employeeId, DateOnly? date = null, decimal? suggestedHours = null)
        {
            if (!await EmployeePortalHelper.CanAccessEmployeeAsync(_userManager, _db, User, employeeId))
                return Forbid();

            var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
            if (employee == null)
                return NotFound();

            var undertimeDate = date ?? DateOnly.FromDateTime(DateTime.Today);
            var blockingMessage = await GetBlockingDuplicateMessageAsync(employeeId, undertimeDate);
            if (blockingMessage != null)
            {
                return PartialView("Partials/_RequestUndertime", new UndertimeRequestFormViewModel
                {
                    EmployeeId = employeeId,
                    EmployeeName = $"{employee.Surname}, {employee.FirstName}",
                    EmployeeIdNumber = employee.EmployeeIdNumber ?? string.Empty,
                    DateFiled = DateOnly.FromDateTime(DateTime.Today),
                    DateOfUndertime = undertimeDate,
                    HasExistingRequest = true,
                    StatusMessage = blockingMessage
                });
            }

            return PartialView("Partials/_RequestUndertime",
                await BuildFormAsync(
                    employee.Id,
                    employee.FirstName,
                    employee.Surname,
                    employee.EmployeeIdNumber,
                    undertimeDate,
                    suggestedHours));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(UndertimeRequestFormViewModel model)
        {
            if (!await EmployeePortalHelper.CanAccessEmployeeAsync(_userManager, _db, User, model.EmployeeId))
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

            if (model.TotalHours < model.DurationHours)
                return Json(new { success = false, message = "Total no. of hours cannot be less than duration." });

            var suggestedMinutes = model.SuggestedMinutes;
            if (!suggestedMinutes.HasValue)
            {
                suggestedMinutes = await _dtrService.GetUndertimeMinutesForDateAsync(
                    model.EmployeeId, model.DateOfUndertime);
            }

            var currentEmployee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            var dto = new UndertimeSubmitDto
            {
                EmployeeId = model.EmployeeId,
                DateFiled = model.DateFiled,
                DateOfUndertime = model.DateOfUndertime,
                DurationHours = model.DurationHours!.Value,
                TotalHours = model.TotalHours!.Value,
                Purpose = model.Purpose,
                ComputedUndertimeMinutesAtSubmit = suggestedMinutes > 0 ? suggestedMinutes : null
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

            return PartialView("Partials/_ProcessUndertime", BuildProcessViewModel(request, status));
        }

        [HttpGet("/UndertimeRequest/Review/{id:guid}")]
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
        public async Task<IActionResult> Process(UndertimeProcessViewModel model)
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

            var (position, department) = await ResolveEmployeePositionAndDepartmentAsync(request.EmployeeId);

            var vm = new UndertimeRequestPrintViewModel
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
                EmployeeName = $"{request.Employee.Surname}, {request.Employee.FirstName}",
                EmployeeIdNumber = request.Employee.EmployeeIdNumber ?? string.Empty,
                DateFiled = request.DateFiled,
                PositionName = position,
                DepartmentName = department,
                DateOfUndertime = request.DateOfUndertime,
                DurationHours = request.DurationHours,
                TotalHours = request.TotalHours,
                Purpose = request.Purpose,
                SubmittedBy = request.SubmittedBy ?? "-",
                SubmittedOn = request.SubmittedOn,
                Status = ApprovalRequestStatusHelper.ResolveEffectiveStatus(
                    request.Status,
                    request.ApprovalRequest?.Status),
                ReviewedByName = request.ReviewedBy?.UserName,
                ReviewedOn = request.ReviewedOn,
                ReviewNotes = request.ReviewNotes,
                ComputedUndertimeMinutesAtSubmit = request.ComputedUndertimeMinutesAtSubmit,
                IsHistoricalDate = request.DateOfUndertime < request.DateFiled
            };

            return View(vm);
        }

        private async Task<List<UndertimeRequestListViewModel>> LoadListAsync(
            Guid? employeeId,
            Guid? actorEmployeeId,
            bool allowAdminOverride)
        {
            var query = _db.UndertimeRequests
                .AsNoTracking()
                .Include(r => r.Employee)
                .Include(r => r.ApprovalRequest)
                .Where(r => !r.IsDeleted);

            if (employeeId.HasValue)
                query = query.Where(r => r.EmployeeId == employeeId.Value);

            var requests = await query
                .OrderByDescending(r => r.SubmittedOn)
                .ToListAsync();

            var items = requests.Select(r => new UndertimeRequestListViewModel
            {
                Id = r.Id,
                EmployeeId = r.EmployeeId,
                EmployeeName = $"{r.Employee.Surname}, {r.Employee.FirstName}",
                EmployeeIdNumber = r.Employee.EmployeeIdNumber ?? string.Empty,
                DateFiled = r.DateFiled,
                DateOfUndertime = r.DateOfUndertime,
                DurationHours = r.DurationHours,
                TotalHours = r.TotalHours,
                Purpose = r.Purpose,
                Status = ApprovalRequestStatusHelper.ResolveEffectiveStatus(r.Status, r.ApprovalRequest?.Status),
                ApprovalRequestId = r.ApprovalRequestId,
                ApprovalWorkflowStatus = r.ApprovalRequest?.Status,
                CurrentStepOrder = r.ApprovalRequest?.CurrentStepOrder,
                SubmittedBy = r.SubmittedBy ?? "-",
                SubmittedOn = r.SubmittedOn
            }).ToList();

            foreach (var item in items.Where(i =>
                         i.Status == JustificationStatus.Pending && i.ApprovalRequestId.HasValue))
            {
                item.CanReview = await _workflowService.CanActorActOnCurrentStepAsync(
                    item.ApprovalRequestId!.Value,
                    actorEmployeeId,
                    allowAdminOverride);
            }

            return items;
        }

        private async Task<UndertimeRequestFormViewModel> BuildFormAsync(
            Guid employeeId,
            string firstName,
            string surname,
            string? employeeIdNumber,
            DateOnly? undertimeDate = null,
            decimal? suggestedHours = null)
        {
            var date = undertimeDate ?? DateOnly.FromDateTime(DateTime.Today);
            decimal? hours = null;
            int minutes = 0;

            if (suggestedHours.HasValue && suggestedHours.Value > 0)
            {
                hours = Math.Round(suggestedHours.Value, 2);
                minutes = (int)Math.Round(hours.Value * 60m);
            }
            else
            {
                minutes = await _dtrService.GetUndertimeMinutesForDateAsync(employeeId, date);
                if (minutes > 0)
                    hours = Math.Round(minutes / 60m, 2);
            }

            var workflow = await _workflowService.GetActiveWorkflowAsync(WorkflowProcessType.Undertime);

            return new UndertimeRequestFormViewModel
            {
                EmployeeId = employeeId,
                EmployeeName = $"{surname}, {firstName}",
                EmployeeIdNumber = employeeIdNumber ?? string.Empty,
                DateFiled = DateOnly.FromDateTime(DateTime.Today),
                DateOfUndertime = date,
                DurationHours = hours,
                TotalHours = hours,
                SuggestedHours = hours,
                SuggestedMinutes = minutes > 0 ? minutes : null,
                RequiresApproval = workflow != null
            };
        }

        private async Task<UndertimeRequest?> LoadRequestAsync(Guid id)
        {
            return await _db.UndertimeRequests
                .Include(r => r.Employee)
                .Include(r => r.ApprovalRequest)
                .Include(r => r.ReviewedBy)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        }

        private static UndertimeProcessViewModel BuildProcessViewModel(
            UndertimeRequest request,
            JustificationStatus? reviewAction = null)
        {
            return new UndertimeProcessViewModel
            {
                Id = request.Id,
                ReviewAction = reviewAction ?? JustificationStatus.Approved,
                EmployeeName = $"{request.Employee.Surname}, {request.Employee.FirstName}",
                EmployeeIdNumber = request.Employee.EmployeeIdNumber ?? string.Empty,
                DateFiled = request.DateFiled,
                DateOfUndertime = request.DateOfUndertime,
                DurationHours = request.DurationHours,
                TotalHours = request.TotalHours,
                Purpose = request.Purpose,
                ComputedUndertimeMinutesAtSubmit = request.ComputedUndertimeMinutesAtSubmit,
                IsHistoricalDate = request.DateOfUndertime < request.DateFiled,
                ApprovalRequestId = request.ApprovalRequestId,
                CurrentStatus = ApprovalRequestStatusHelper.ResolveEffectiveStatus(
                    request.Status,
                    request.ApprovalRequest?.Status),
                SubmittedBy = request.SubmittedBy ?? "-",
                SubmittedOn = request.SubmittedOn,
                ReviewedOn = request.ReviewedOn
            };
        }

        private async Task<bool> CanReviewRequestAsync(UndertimeRequest request)
        {
            if (!request.ApprovalRequestId.HasValue)
                return false;

            var actorEmployeeId = (await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User))?.Id;
            return await _workflowService.CanActorActOnCurrentStepAsync(
                request.ApprovalRequestId.Value,
                actorEmployeeId,
                await IsAdminOrHrAsync());
        }

        private async Task<bool> CanViewRequestAsync(UndertimeRequest request)
        {
            if (await EmployeePortalHelper.CanAccessEmployeeAsync(_userManager, _db, User, request.EmployeeId))
                return true;

            return await IsAdminOrHrAsync();
        }

        private async Task<string?> GetBlockingDuplicateMessageAsync(Guid employeeId, DateOnly dateOfUndertime)
        {
            var existing = await _db.UndertimeRequests
                .AsNoTracking()
                .Include(r => r.ApprovalRequest)
                .Where(r => r.EmployeeId == employeeId &&
                            r.DateOfUndertime == dateOfUndertime &&
                            !r.IsDeleted)
                .ToListAsync();

            if (existing.Any(r =>
                    ApprovalRequestStatusHelper.IsPending(r.Status, r.ApprovalRequest?.Status)))
                return "An undertime request for this date is already pending approval.";

            if (existing.Any(r =>
                    r.Status == JustificationStatus.Approved ||
                    r.ApprovalRequest?.Status == ApprovalRequestStatus.Approved))
                return "An approved undertime request already exists for this date.";

            return null;
        }

        private async Task<(string position, string department)> ResolveEmployeePositionAndDepartmentAsync(Guid employeeId)
        {
            var designations = await EmployeeOrganizationHelper.GetActiveDesignationNamesAsync(
                _db, new[] { employeeId });
            designations.TryGetValue(employeeId, out var position);

            var orgUnitId = await EmployeeOrganizationHelper.GetPrimaryOrganizationStructureIdAsync(_db, employeeId);
            var department = orgUnitId.HasValue
                ? await _db.OrganizationStructures
                    .AsNoTracking()
                    .Where(o => o.Id == orgUnitId.Value)
                    .Select(o => o.Name)
                    .FirstOrDefaultAsync() ?? string.Empty
                : string.Empty;

            return (position ?? string.Empty, department);
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
    }
}
