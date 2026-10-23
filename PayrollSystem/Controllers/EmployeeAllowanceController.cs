using System.Globalization;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Services;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    public class EmployeeAllowanceController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly IAllowanceService _allowanceService;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<EmployeeAllowanceController> _logger;

        public EmployeeAllowanceController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IAllowanceService allowanceService,
            AuditHelper auditHelper,
            ILogger<EmployeeAllowanceController> logger)
        {
            _userManager = userManager;
            _db = db;
            _allowanceService = allowanceService;
            _auditHelper = auditHelper;
            _logger = logger;
        }

        private async Task LogAuditTrailAsync(string action, object? oldData, object? newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "Allowance", "Allowances", oldData, newData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit trail for {Action}. OldData: {OldData}, NewData: {NewData}",
                    action, oldData, newData);
            }
        }

        private JsonResult ValidationErrorResponse()
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return Json(new
            {
                success = false,
                message = "Validation failed",
                errors = errors
            });
        }

        private List<Guid> ParseEmployeeIdsFromRequest(List<Guid>? modelEmployeeIds)
        {
            var fromForm = Request.Form["EmployeeIds"]
                .Select(v => Guid.TryParse(v, out var id) ? id : (Guid?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            return fromForm.Count > 0 ? fromForm : (modelEmployeeIds ?? new List<Guid>());
        }

        private static AllowanceSaveRequest ToSaveRequest(AllowanceEditViewModel vm, List<Guid> employeeIds)
        {
            return new AllowanceSaveRequest
            {
                Id = vm.Id,
                AllowanceName = vm.AllowanceName,
                Code = vm.Code,
                AllowanceTypeId = vm.AllowanceTypeId,
                Amount = vm.Amount,
                Frequency = vm.Frequency,
                IsTaxable = vm.IsTaxable,
                IsMandatory = vm.IsMandatory,
                EffectiveDate = vm.EffectiveDate,
                EndDate = vm.EndDate,
                Description = vm.Description,
                IsActive = vm.IsActive,
                ApplyAllActiveEmployees = vm.ApplyAllActiveEmployees,
                EmployeeIds = employeeIds
            };
        }

        private async Task PrepareManageViewBagAsync()
        {
            var employees = await _db.Employees
                .Where(e => e.IsActive && !e.IsDeleted)
                .OrderBy(e => e.Surname)
                .ThenBy(e => e.FirstName)
                .Select(e => new { e.Id, Name = e.FullName, e.EmployeeIdNumber })
                .ToListAsync();

            ViewBag.Employees = employees;
            ViewBag.ActiveEmployeeCount = employees.Count;

            var allowanceTypes = await _db.AllowanceTypes
                .Where(at => at.IsActive && !at.IsDeleted)
                .OrderBy(at => at.Name)
                .Select(at => new { at.Id, at.Name })
                .ToListAsync();
            ViewBag.AllowanceTypes = new SelectList(allowanceTypes, "Id", "Name");
        }

        [Permission("EMPLOYEEALLOWANCE_VIEW")]
        public async Task<IActionResult> Index()
        {
            ViewBag.Employees = await _db.Employees
                .Where(e => !e.IsDeleted)
                .OrderBy(e => e.Surname).ThenBy(e => e.FirstName)
                .Select(e => new { e.Id, e.FullName })
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Designations = await _db.Designations
                .Where(d => !d.IsDeleted && d.IsActive)
                .OrderBy(d => d.DesignationName)
                .Select(d => new { d.Id, d.DesignationName })
                .AsNoTracking()
                .ToListAsync();

            ViewBag.OrganizationUnits = await _db.OrganizationStructures
                .Where(o => o.IsActive)
                .OrderBy(o => o.Name)
                .Select(o => new { o.Id, o.Name })
                .AsNoTracking()
                .ToListAsync();

            ViewBag.AllowanceTypes = await _db.AllowanceTypes
                .Where(at => at.IsActive && !at.IsDeleted)
                .OrderBy(at => at.Name)
                .Select(at => new { at.Id, at.Name })
                .AsNoTracking()
                .ToListAsync();

            return View();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Permission("EMPLOYEEALLOWANCE_VIEW")]
        public async Task<IActionResult> GetEmployeeAllowancesJson()
        {
            var draw = HttpContext.Request.Form["draw"].FirstOrDefault() ?? "1";
            _ = int.TryParse(HttpContext.Request.Form["start"].FirstOrDefault(), out var start);
            _ = int.TryParse(HttpContext.Request.Form["length"].FirstOrDefault(), out var length);
            if (length == 0) length = 25;

            var sortColIdx = HttpContext.Request.Form["order[0][column]"].FirstOrDefault() ?? "0";
            var sortColumn = HttpContext.Request.Form[$"columns[{sortColIdx}][data]"].FirstOrDefault();
            var sortDir = HttpContext.Request.Form["order[0][dir]"].FirstOrDefault() ?? "asc";
            var searchValue = HttpContext.Request.Form["search[value]"].FirstOrDefault();

            var filterEmployeeId = HttpContext.Request.Form["employeeId"].FirstOrDefault();
            var filterFullName = HttpContext.Request.Form["fullName"].FirstOrDefault();
            var filterDesignationId = HttpContext.Request.Form["designationId"].FirstOrDefault();
            var filterOrganizationStructureId = HttpContext.Request.Form["organizationStructureId"].FirstOrDefault();
            var filterAllowanceTypeId = HttpContext.Request.Form["allowanceTypeId"].FirstOrDefault();
            var filterStatus = HttpContext.Request.Form["status"].FirstOrDefault();

            var recordsTotal = await _db.Allowances.CountAsync(a => !a.IsDeleted);

            var query = _db.Allowances
                .Where(a => !a.IsDeleted)
                .Include(a => a.AllowanceType)
                .Include(a => a.EmployeeAssignments.Where(ea => !ea.IsDeleted))
                    .ThenInclude(ea => ea.Employee)
                        .ThenInclude(e => e!.Designations.Where(d => d.EffectiveTo == null))
                        .ThenInclude(d => d.Designation)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filterEmployeeId))
            {
                var empId = filterEmployeeId.ToLower();
                query = query.Where(a => a.EmployeeAssignments.Any(ea =>
                    !ea.IsDeleted && ea.Employee.EmployeeIdNumber.ToLower().Contains(empId)));
            }

            if (!string.IsNullOrWhiteSpace(filterFullName))
            {
                var name = filterFullName.ToLower();
                query = query.Where(a => a.EmployeeAssignments.Any(ea =>
                    !ea.IsDeleted &&
                    ((ea.Employee.FirstName + " " + ea.Employee.MiddleName + " " + ea.Employee.Surname).ToLower().Contains(name) ||
                     (ea.Employee.FirstName + " " + ea.Employee.Surname).ToLower().Contains(name))));
            }

            if (!string.IsNullOrWhiteSpace(filterDesignationId) && int.TryParse(filterDesignationId, out var desId))
                query = query.Where(a => a.EmployeeAssignments.Any(ea =>
                    !ea.IsDeleted && ea.Employee.Designations.Any(d => d.DesignationId == desId && d.EffectiveTo == null)));

            if (!string.IsNullOrWhiteSpace(filterOrganizationStructureId) && int.TryParse(filterOrganizationStructureId, out var orgUnitId))
                query = query.Where(a => a.EmployeeAssignments.Any(ea =>
                    !ea.IsDeleted && _db.EmployeeOrganizationAssignments.Any(a =>
                        a.EmployeeId == ea.EmployeeId &&
                        a.OrganizationStructureId == orgUnitId &&
                        (a.EffectiveTo == null || a.EffectiveTo >= DateTime.Today))));

            if (!string.IsNullOrWhiteSpace(filterAllowanceTypeId) && int.TryParse(filterAllowanceTypeId, out var atId))
                query = query.Where(a => a.AllowanceTypeId == atId);

            if (!string.IsNullOrWhiteSpace(filterStatus) && bool.TryParse(filterStatus, out var isActive))
                query = query.Where(a => a.IsActive == isActive);

            if (!string.IsNullOrWhiteSpace(searchValue))
            {
                var s = searchValue.ToLower();
                query = query.Where(a =>
                    (a.AllowanceName != null && a.AllowanceName.ToLower().Contains(s)) ||
                    (a.AllowanceType != null && a.AllowanceType.Name.ToLower().Contains(s)) ||
                    (a.Code != null && a.Code.ToLower().Contains(s)) ||
                    a.EmployeeAssignments.Any(ea => !ea.IsDeleted && (
                        ea.Employee.EmployeeIdNumber.ToLower().Contains(s) ||
                        ea.Employee.FirstName.ToLower().Contains(s) ||
                        ea.Employee.Surname.ToLower().Contains(s) ||
                        (ea.Employee.MiddleName != null && ea.Employee.MiddleName.ToLower().Contains(s)))));
            }

            var recordsFiltered = await query.CountAsync();

            query = sortColumn switch
            {
                "allowanceName" => sortDir == "asc"
                    ? query.OrderBy(a => a.AllowanceName)
                    : query.OrderByDescending(a => a.AllowanceName),
                "allowanceTypeName" => sortDir == "asc"
                    ? query.OrderBy(a => a.AllowanceType != null ? a.AllowanceType.Name : "")
                    : query.OrderByDescending(a => a.AllowanceType != null ? a.AllowanceType.Name : ""),
                "code" => sortDir == "asc"
                    ? query.OrderBy(a => a.Code)
                    : query.OrderByDescending(a => a.Code),
                "amount" => sortDir == "asc"
                    ? query.OrderBy(a => a.Amount)
                    : query.OrderByDescending(a => a.Amount),
                "frequency" => sortDir == "asc"
                    ? query.OrderBy(a => a.Frequency)
                    : query.OrderByDescending(a => a.Frequency),
                "isTaxable" => sortDir == "asc"
                    ? query.OrderBy(a => a.IsTaxable)
                    : query.OrderByDescending(a => a.IsTaxable),
                "isMandatory" => sortDir == "asc"
                    ? query.OrderBy(a => a.IsMandatory)
                    : query.OrderByDescending(a => a.IsMandatory),
                "effectiveDate" => sortDir == "asc"
                    ? query.OrderBy(a => a.EffectiveDate)
                    : query.OrderByDescending(a => a.EffectiveDate),
                "endDate" => sortDir == "asc"
                    ? query.OrderBy(a => a.EndDate)
                    : query.OrderByDescending(a => a.EndDate),
                "isActive" => sortDir == "asc"
                    ? query.OrderBy(a => a.IsActive)
                    : query.OrderByDescending(a => a.IsActive),
                "employeeCount" => sortDir == "asc"
                    ? query.OrderBy(a => a.EmployeeAssignments.Count(ea => !ea.IsDeleted && ea.IsActive))
                    : query.OrderByDescending(a => a.EmployeeAssignments.Count(ea => !ea.IsDeleted && ea.IsActive)),
                _ => query.OrderBy(a => a.AllowanceName)
            };

            var page = await query
                .Skip(start)
                .Take(length)
                .Select(a => new
                {
                    id = a.Id,
                    allowanceName = a.AllowanceName,
                    allowanceTypeName = a.AllowanceType != null ? a.AllowanceType.Name : "",
                    code = a.Code,
                    amount = a.Amount,
                    frequency = a.Frequency.ToString(),
                    isTaxable = a.IsTaxable,
                    isMandatory = a.IsMandatory,
                    effectiveDate = a.EffectiveDate,
                    endDate = a.EndDate,
                    isActive = a.IsActive,
                    employeeCount = a.EmployeeAssignments.Count(ea => !ea.IsDeleted && ea.IsActive)
                })
                .ToListAsync();

            _ = int.TryParse(draw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var drawInt);

            return Json(new
            {
                draw = drawInt,
                recordsTotal,
                recordsFiltered,
                data = page
            });
        }

        [HttpGet]
        [Permission("EMPLOYEEALLOWANCE_VIEW")]
        public async Task<IActionResult> Manage(int? id = null)
        {
            await PrepareManageViewBagAsync();

            if (!id.HasValue)
                return PartialView("Partials/_ManageEmployeeAllowance", new AllowanceEditViewModel());

            var allowance = await _db.Allowances
                .Include(a => a.AllowanceType)
                .Include(a => a.EmployeeAssignments.Where(ea => !ea.IsDeleted && ea.IsActive))
                .FirstOrDefaultAsync(a => a.Id == id.Value && !a.IsDeleted);

            if (allowance == null)
                return NotFound();

            var vm = new AllowanceEditViewModel
            {
                Id = allowance.Id,
                AllowanceName = allowance.AllowanceName,
                Code = allowance.Code,
                AllowanceTypeId = allowance.AllowanceTypeId,
                Amount = allowance.Amount,
                Frequency = allowance.Frequency,
                IsTaxable = allowance.IsTaxable,
                IsMandatory = allowance.IsMandatory,
                EffectiveDate = allowance.EffectiveDate,
                EndDate = allowance.EndDate,
                Description = allowance.Description,
                IsActive = allowance.IsActive,
                EmployeeIds = allowance.EmployeeAssignments.Select(ea => ea.EmployeeId).ToList()
            };

            return PartialView("Partials/_ManageEmployeeAllowance", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("EMPLOYEEALLOWANCE_VIEW")]
        public async Task<IActionResult> Manage(AllowanceEditViewModel viewModel)
        {
            if (viewModel.ApplyAllActiveEmployees)
                ModelState.Remove(nameof(AllowanceEditViewModel.EmployeeIds));

            if (!TryValidateModel(viewModel))
                return ValidationErrorResponse();

            viewModel.EmployeeIds = ParseEmployeeIdsFromRequest(viewModel.EmployeeIds);

            var currentUser = await _userManager.GetUserAsync(User);
            var request = ToSaveRequest(viewModel, viewModel.EmployeeIds);
            var isUpdate = viewModel.Id > 0;

            var result = isUpdate
                ? await _allowanceService.UpdateWithAssignmentsAsync(request, currentUser?.Id)
                : await _allowanceService.CreateWithAssignmentsAsync(request, currentUser?.Id);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            await LogAuditTrailAsync(
                isUpdate ? "UPDATE" : "CREATE",
                isUpdate ? new { viewModel.Id } : null,
                new { result.AllowanceId, result.AssignedEmployeeCount });

            return Json(new { success = true, message = result.Message });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var allowance = await _db.Allowances
                .Include(a => a.AllowanceType)
                .Include(a => a.EmployeeAssignments.Where(ea => !ea.IsDeleted))
                    .ThenInclude(ea => ea.Employee)
                        .ThenInclude(e => e!.Designations.Where(d => d.EffectiveTo == null))
                        .ThenInclude(d => d.Designation)
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

            if (allowance == null)
                return NotFound();

            var empIds = allowance.EmployeeAssignments.Select(ea => ea.EmployeeId).ToList();
            var unitNames = await EmployeeOrganizationHelper.GetActiveUnitDisplayNamesAsync(_db, empIds);

            var vm = new AllowanceDetailsViewModel
            {
                Allowance = new AllowanceListItemViewModel
                {
                    Id = allowance.Id,
                    AllowanceName = allowance.AllowanceName,
                    AllowanceTypeName = allowance.AllowanceType?.Name ?? "",
                    Code = allowance.Code,
                    Amount = allowance.Amount,
                    Frequency = allowance.Frequency,
                    IsTaxable = allowance.IsTaxable,
                    IsMandatory = allowance.IsMandatory,
                    EffectiveDate = allowance.EffectiveDate,
                    EndDate = allowance.EndDate,
                    IsActive = allowance.IsActive,
                    EmployeeCount = allowance.EmployeeAssignments.Count(ea => ea.IsActive)
                },
                Employees = allowance.EmployeeAssignments
                    .OrderBy(ea => ea.Employee.Surname)
                    .ThenBy(ea => ea.Employee.FirstName)
                    .Select(ea => new AllowanceEmployeeListItemViewModel
                    {
                        AssignmentId = ea.Id,
                        EmployeeIdNumber = ea.Employee.EmployeeIdNumber,
                        EmployeeName = ea.Employee.FullName,
                        OrganizationUnitName = unitNames.GetValueOrDefault(ea.EmployeeId),
                        DesignationName = ea.Employee.Designations
                            .Where(d => d.EffectiveTo == null)
                            .Select(d => d.Designation!.DesignationName)
                            .FirstOrDefault(),
                        IsActive = ea.IsActive
                    })
                    .ToList()
            };

            return PartialView("Partials/_ViewEmployeeAllowance", vm);
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var allowance = await _db.Allowances
                .Include(a => a.EmployeeAssignments.Where(ea => !ea.IsDeleted))
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

            if (allowance == null)
                return NotFound();

            var vm = new AllowanceListItemViewModel
            {
                Id = allowance.Id,
                AllowanceName = allowance.AllowanceName,
                Amount = allowance.Amount,
                Frequency = allowance.Frequency,
                IsActive = allowance.IsActive,
                EmployeeCount = allowance.EmployeeAssignments.Count
            };

            return PartialView("Partials/_DeleteEmployeeAllowance", vm);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var allowance = await _db.Allowances
                    .AsTracking()
                    .Include(a => a.EmployeeAssignments)
                    .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

                if (allowance == null)
                    return Json(new { success = false, message = "Allowance not found." });

                var existing = JsonSerializer.Serialize(allowance);
                var currentUser = await _userManager.GetUserAsync(User);
                var now = DateTime.Now;

                allowance.UpdatedById = currentUser?.Id;
                allowance.UpdatedOn = now;
                allowance.IsDeleted = true;

                foreach (var assignment in allowance.EmployeeAssignments.Where(ea => !ea.IsDeleted))
                {
                    assignment.UpdatedById = currentUser?.Id;
                    assignment.UpdatedOn = now;
                    assignment.IsDeleted = true;
                }

                await _db.SaveChangesAsync();

                await LogAuditTrailAsync("DELETE", existing, null);

                return Json(new { success = true, message = "Allowance and all employee assignments deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting allowance");
                return Json(new { success = false, message = "An error occurred while deleting the allowance." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeAllowances()
        {
            try
            {
                var data = await _db.EmployeeAllowances
                    .Include(a => a.Employee)
                    .Include(a => a.Allowance)
                        .ThenInclude(al => al.AllowanceType)
                    .Where(a => !a.IsDeleted && a.IsActive && a.Allowance != null && !a.Allowance.IsDeleted)
                    .Select(a => new EmployeeAllowanceListItemViewModel
                    {
                        Id = a.Id,
                        AllowanceId = a.AllowanceId,
                        EmployeeIdNumber = a.Employee.EmployeeIdNumber,
                        EmployeeName = a.Employee.FullName,
                        AllowanceName = a.Allowance.AllowanceName,
                        AllowanceTypeName = a.Allowance.AllowanceType.Name,
                        Code = a.Allowance.Code,
                        Amount = a.Allowance.Amount,
                        Frequency = a.Allowance.Frequency,
                        IsTaxable = a.Allowance.IsTaxable,
                        IsMandatory = a.Allowance.IsMandatory,
                        EffectiveDate = a.Allowance.EffectiveDate,
                        EndDate = a.Allowance.EndDate,
                        IsActive = a.IsActive && a.Allowance.IsActive
                    })
                    .ToListAsync();

                data = data.OrderBy(a => a.EmployeeIdNumber)
                    .ThenBy(a => a.AllowanceName)
                    .ToList();

                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employee allowances");
                return Json(new { success = false, message = "Error retrieving employee allowances" });
            }
        }
    }
}
