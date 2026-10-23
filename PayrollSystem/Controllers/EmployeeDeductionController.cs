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
    public class EmployeeDeductionController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IDeductionService _deductionService;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<EmployeeDeductionController> _logger;

        public EmployeeDeductionController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IDeductionService deductionService,
            AuditHelper auditHelper,
            ILogger<EmployeeDeductionController> logger)
        {
            _db = db;
            _userManager = userManager;
            _deductionService = deductionService;
            _auditHelper = auditHelper;
            _logger = logger;
        }

        private async Task LogAuditTrailAsync(string action, object? oldData, object? newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "Deduction", "Deductions", oldData, newData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit trail for {Action}", action);
            }
        }

        private JsonResult ValidationErrorResponse()
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return Json(new { success = false, message = "Validation failed", errors });
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

            var deductionTypes = await GetNonLoanDeductionTypesAsync();
            ViewBag.DeductionTypes = new SelectList(deductionTypes, "Id", "Name");
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

        private static DeductionSaveRequest ToSaveRequest(DeductionEditViewModel vm, List<Guid> employeeIds)
        {
            return new DeductionSaveRequest
            {
                Id = vm.Id,
                DeductionName = vm.DeductionName,
                Code = vm.Code,
                DeductionTypeId = vm.DeductionTypeId,
                Amount = vm.Amount,
                EffectiveStartDate = vm.EffectiveStartDate,
                EffectiveEndDate = vm.EffectiveEndDate,
                Remarks = vm.Remarks,
                IsActive = vm.IsActive,
                ApplyAllActiveEmployees = vm.ApplyAllActiveEmployees,
                EmployeeIds = employeeIds
            };
        }

        private async Task<List<object>> GetNonLoanDeductionTypesAsync()
        {
            var allTypes = await _db.DeductionTypes
                .AsNoTracking()
                .OrderBy(dt => dt.DisplayOrder)
                .ThenBy(dt => dt.Name)
                .Select(dt => new { dt.Id, dt.Name, dt.Code })
                .ToListAsync();

            return allTypes
                .Where(dt => !string.Equals(dt.Code, "LOAN", StringComparison.OrdinalIgnoreCase))
                .Select(dt => (object)new { dt.Id, dt.Name })
                .ToList();
        }

        [Permission("DEDUCTIONMASTER_VIEW")]
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

            ViewBag.DeductionTypes = await GetNonLoanDeductionTypesAsync();

            return View();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Permission("DEDUCTIONMASTER_VIEW")]
        public async Task<IActionResult> GetEmployeeDeductionsJson()
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
            var filterDeductionTypeId = HttpContext.Request.Form["deductionTypeId"].FirstOrDefault();
            var filterStatus = HttpContext.Request.Form["status"].FirstOrDefault();

            var recordsTotal = await _db.Deductions.CountAsync(dm => !dm.IsDeleted);

            var query = _db.Deductions
                .Where(dm => !dm.IsDeleted)
                .Include(dm => dm.DeductionType)
                .Include(dm => dm.EmployeeAssignments.Where(ed => !ed.IsDeleted))
                    .ThenInclude(ed => ed.Employee)
                        .ThenInclude(e => e!.Designations.Where(d => d.EffectiveTo == null))
                        .ThenInclude(d => d.Designation)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filterEmployeeId))
            {
                var empId = filterEmployeeId.ToLower();
                query = query.Where(dm => dm.EmployeeAssignments.Any(ed =>
                    !ed.IsDeleted && ed.Employee.EmployeeIdNumber.ToLower().Contains(empId)));
            }

            if (!string.IsNullOrWhiteSpace(filterFullName))
            {
                var name = filterFullName.ToLower();
                query = query.Where(dm => dm.EmployeeAssignments.Any(ed =>
                    !ed.IsDeleted &&
                    ((ed.Employee.FirstName + " " + ed.Employee.MiddleName + " " + ed.Employee.Surname).ToLower().Contains(name) ||
                     (ed.Employee.FirstName + " " + ed.Employee.Surname).ToLower().Contains(name))));
            }

            if (!string.IsNullOrWhiteSpace(filterDesignationId) && int.TryParse(filterDesignationId, out var desId))
                query = query.Where(dm => dm.EmployeeAssignments.Any(ed =>
                    !ed.IsDeleted && ed.Employee.Designations.Any(d => d.DesignationId == desId && d.EffectiveTo == null)));

            if (!string.IsNullOrWhiteSpace(filterOrganizationStructureId) && int.TryParse(filterOrganizationStructureId, out var orgUnitId))
                query = query.Where(dm => dm.EmployeeAssignments.Any(ed =>
                    !ed.IsDeleted && _db.EmployeeOrganizationAssignments.Any(a =>
                        a.EmployeeId == ed.EmployeeId &&
                        a.OrganizationStructureId == orgUnitId &&
                        (a.EffectiveTo == null || a.EffectiveTo >= DateTime.Today))));

            if (!string.IsNullOrWhiteSpace(filterDeductionTypeId) && int.TryParse(filterDeductionTypeId, out var dtId))
                query = query.Where(dm => dm.DeductionTypeId == dtId);

            if (!string.IsNullOrWhiteSpace(filterStatus) && bool.TryParse(filterStatus, out var isActive))
                query = query.Where(dm => dm.IsActive == isActive);

            if (!string.IsNullOrWhiteSpace(searchValue))
            {
                var s = searchValue.ToLower();
                query = query.Where(dm =>
                    (dm.DeductionName != null && dm.DeductionName.ToLower().Contains(s)) ||
                    (dm.DeductionType != null && dm.DeductionType.Name.ToLower().Contains(s)) ||
                    (dm.Code != null && dm.Code.ToLower().Contains(s)) ||
                    dm.EmployeeAssignments.Any(ed => !ed.IsDeleted && (
                        ed.Employee.EmployeeIdNumber.ToLower().Contains(s) ||
                        ed.Employee.FirstName.ToLower().Contains(s) ||
                        ed.Employee.Surname.ToLower().Contains(s) ||
                        (ed.Employee.MiddleName != null && ed.Employee.MiddleName.ToLower().Contains(s)))));
            }

            var recordsFiltered = await query.CountAsync();

            query = sortColumn switch
            {
                "deductionName" => sortDir == "asc"
                    ? query.OrderBy(dm => dm.DeductionName)
                    : query.OrderByDescending(dm => dm.DeductionName),
                "deductionTypeName" => sortDir == "asc"
                    ? query.OrderBy(dm => dm.DeductionType != null ? dm.DeductionType.Name : "")
                    : query.OrderByDescending(dm => dm.DeductionType != null ? dm.DeductionType.Name : ""),
                "code" => sortDir == "asc"
                    ? query.OrderBy(dm => dm.Code)
                    : query.OrderByDescending(dm => dm.Code),
                "amount" => sortDir == "asc"
                    ? query.OrderBy(dm => dm.Amount)
                    : query.OrderByDescending(dm => dm.Amount),
                "effectiveStartDate" => sortDir == "asc"
                    ? query.OrderBy(dm => dm.EffectiveStartDate)
                    : query.OrderByDescending(dm => dm.EffectiveStartDate),
                "effectiveEndDate" => sortDir == "asc"
                    ? query.OrderBy(dm => dm.EffectiveEndDate)
                    : query.OrderByDescending(dm => dm.EffectiveEndDate),
                "isActive" => sortDir == "asc"
                    ? query.OrderBy(dm => dm.IsActive)
                    : query.OrderByDescending(dm => dm.IsActive),
                "employeeCount" => sortDir == "asc"
                    ? query.OrderBy(dm => dm.EmployeeAssignments.Count(ed => !ed.IsDeleted && ed.IsActive))
                    : query.OrderByDescending(dm => dm.EmployeeAssignments.Count(ed => !ed.IsDeleted && ed.IsActive)),
                _ => query.OrderBy(dm => dm.DeductionName)
            };

            var page = await query
                .Skip(start)
                .Take(length)
                .Select(dm => new
                {
                    id = dm.Id,
                    deductionName = dm.DeductionName,
                    deductionTypeName = dm.DeductionType != null ? dm.DeductionType.Name : "",
                    code = dm.Code,
                    amount = dm.Amount,
                    effectiveStartDate = dm.EffectiveStartDate,
                    effectiveEndDate = dm.EffectiveEndDate,
                    isActive = dm.IsActive,
                    employeeCount = dm.EmployeeAssignments.Count(ed => !ed.IsDeleted && ed.IsActive)
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
        [Permission("DEDUCTIONMASTER_VIEW")]
        public async Task<IActionResult> Manage(int? id = null)
        {
            await PrepareManageViewBagAsync();

            if (!id.HasValue)
                return PartialView("Partials/_ManageEmployeeDeduction", new DeductionEditViewModel());

            var deduction = await _db.Deductions
                .Include(d => d.DeductionType)
                .Include(d => d.EmployeeAssignments.Where(ed => !ed.IsDeleted && ed.IsActive))
                .FirstOrDefaultAsync(d => d.Id == id.Value && !d.IsDeleted);

            if (deduction == null)
                return NotFound();

            var vm = new DeductionEditViewModel
            {
                Id = deduction.Id,
                DeductionTypeId = deduction.DeductionTypeId,
                DeductionName = deduction.DeductionName,
                Code = deduction.Code,
                Amount = deduction.Amount,
                EffectiveStartDate = deduction.EffectiveStartDate,
                EffectiveEndDate = deduction.EffectiveEndDate,
                Remarks = deduction.Remarks,
                IsActive = deduction.IsActive,
                EmployeeIds = deduction.EmployeeAssignments.Select(ed => ed.EmployeeId).ToList()
            };

            return PartialView("Partials/_ManageEmployeeDeduction", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("DEDUCTIONMASTER_VIEW")]
        public async Task<IActionResult> Manage(DeductionEditViewModel viewModel)
        {
            if (viewModel.ApplyAllActiveEmployees)
                ModelState.Remove(nameof(DeductionEditViewModel.EmployeeIds));

            if (!TryValidateModel(viewModel))
                return ValidationErrorResponse();

            viewModel.EmployeeIds = ParseEmployeeIdsFromRequest(viewModel.EmployeeIds);

            var currentUser = await _userManager.GetUserAsync(User);
            var request = ToSaveRequest(viewModel, viewModel.EmployeeIds);
            var isUpdate = viewModel.Id > 0;

            var result = isUpdate
                ? await _deductionService.UpdateWithAssignmentsAsync(request, currentUser?.Id)
                : await _deductionService.CreateWithAssignmentsAsync(request, currentUser?.Id);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            await LogAuditTrailAsync(
                isUpdate ? "UPDATE" : "CREATE",
                isUpdate ? new { viewModel.Id } : null,
                new { result.DeductionId, result.AssignedEmployeeCount });

            return Json(new { success = true, message = result.Message });
        }

        [HttpGet]
        [Permission("DEDUCTIONMASTER_VIEW")]
        public async Task<IActionResult> Details(int id)
        {
            var master = await _db.Deductions
                .Include(dm => dm.DeductionType)
                .Include(dm => dm.EmployeeAssignments.Where(ed => !ed.IsDeleted))
                    .ThenInclude(ed => ed.Employee)
                        .ThenInclude(e => e!.Designations.Where(d => d.EffectiveTo == null))
                        .ThenInclude(d => d.Designation)
                .FirstOrDefaultAsync(dm => dm.Id == id && !dm.IsDeleted);

            if (master == null)
                return NotFound();

            var empIds = master.EmployeeAssignments.Select(ed => ed.EmployeeId).ToList();
            var unitNames = await EmployeeOrganizationHelper.GetActiveUnitDisplayNamesAsync(_db, empIds);

            var vm = new DeductionDetailsViewModel
            {
                Deduction = new DeductionListItemViewModel
                {
                    Id = master.Id,
                    DeductionName = master.DeductionName,
                    DeductionTypeName = master.DeductionType?.Name ?? "",
                    Code = master.Code,
                    Amount = master.Amount,
                    EffectiveStartDate = master.EffectiveStartDate,
                    EffectiveEndDate = master.EffectiveEndDate,
                    IsActive = master.IsActive,
                    Remarks = master.Remarks,
                    EmployeeCount = master.EmployeeAssignments.Count(ed => ed.IsActive)
                },
                Employees = master.EmployeeAssignments
                    .OrderBy(ed => ed.Employee.Surname)
                    .ThenBy(ed => ed.Employee.FirstName)
                    .Select(ed => new DeductionEmployeeListItemViewModel
                    {
                        AssignmentId = ed.Id,
                        EmployeeIdNumber = ed.Employee.EmployeeIdNumber,
                        EmployeeName = ed.Employee.FullName,
                        OrganizationUnitName = unitNames.GetValueOrDefault(ed.EmployeeId),
                        DesignationName = ed.Employee.Designations
                            .Where(d => d.EffectiveTo == null)
                            .Select(d => d.Designation!.DesignationName)
                            .FirstOrDefault(),
                        IsActive = ed.IsActive
                    })
                    .ToList()
            };

            return PartialView("Partials/_ViewEmployeeDeduction", vm);
        }

        [HttpGet]
        [Permission("DEDUCTIONMASTER_VIEW")]
        public async Task<IActionResult> Delete(int id)
        {
            var master = await _db.Deductions
                .Include(dm => dm.EmployeeAssignments.Where(ed => !ed.IsDeleted))
                .FirstOrDefaultAsync(dm => dm.Id == id && !dm.IsDeleted);

            if (master == null)
                return NotFound();

            var vm = new DeductionListItemViewModel
            {
                Id = master.Id,
                DeductionName = master.DeductionName,
                Amount = master.Amount,
                IsActive = master.IsActive,
                EmployeeCount = master.EmployeeAssignments.Count
            };

            return PartialView("Partials/_DeleteEmployeeDeduction", vm);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Permission("DEDUCTIONMASTER_VIEW")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var master = await _db.Deductions
                    .AsTracking()
                    .Include(dm => dm.EmployeeAssignments)
                    .FirstOrDefaultAsync(dm => dm.Id == id && !dm.IsDeleted);

                if (master == null)
                    return Json(new { success = false, message = "Deduction not found." });

                var existing = JsonSerializer.Serialize(master);
                var currentUser = await _userManager.GetUserAsync(User);
                var now = DateTime.Now;

                master.UpdatedById = currentUser?.Id;
                master.UpdatedOn = now;
                master.IsDeleted = true;

                foreach (var assignment in master.EmployeeAssignments.Where(ed => !ed.IsDeleted))
                {
                    assignment.UpdatedById = currentUser?.Id;
                    assignment.UpdatedOn = now;
                    assignment.IsDeleted = true;
                }

                await _db.SaveChangesAsync();

                await LogAuditTrailAsync("DELETE", existing, null);

                return Json(new { success = true, message = "Deduction and all employee assignments deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting deduction");
                return Json(new { success = false, message = "An error occurred while deleting the deduction." });
            }
        }

        [HttpGet]
        [Permission("DEDUCTIONMASTER_VIEW")]
        public async Task<IActionResult> GetEmployeeDeductions()
        {
            try
            {
                var data = await _db.EmployeeDeductions
                    .Include(ed => ed.Employee)
                    .Include(ed => ed.Deduction)
                        .ThenInclude(dm => dm.DeductionType)
                    .Where(ed => !ed.IsDeleted && ed.IsActive && ed.Deduction != null && !ed.Deduction.IsDeleted)
                    .Select(ed => new EmployeeDeductionListItemViewModel
                    {
                        Id = ed.Id,
                        DeductionId = ed.DeductionId,
                        EmployeeIdNumber = ed.Employee.EmployeeIdNumber,
                        EmployeeName = ed.Employee.FullName,
                        DeductionName = ed.Deduction.DeductionName,
                        DeductionTypeName = ed.Deduction.DeductionType.Name,
                        Code = ed.Deduction.Code,
                        Amount = ed.Deduction.Amount,
                        EffectiveStartDate = ed.Deduction.EffectiveStartDate,
                        EffectiveEndDate = ed.Deduction.EffectiveEndDate,
                        IsActive = ed.IsActive && ed.Deduction.IsActive,
                        Remarks = ed.Deduction.Remarks
                    })
                    .ToListAsync();

                data = data.OrderBy(ed => ed.EmployeeIdNumber)
                    .ThenBy(ed => ed.DeductionName)
                    .ToList();

                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employee deductions");
                return Json(new { success = false, message = "Error retrieving employee deductions" });
            }
        }
    }
}
