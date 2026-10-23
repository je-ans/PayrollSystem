using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    [Authorize]
    public class EmployeeLoanController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<EmployeeLoanController> _logger;

        public EmployeeLoanController(
            ApplicationDbContext db,
            AuditHelper auditHelper,
            ILogger<EmployeeLoanController> logger)
        {
            _db = db;
            _auditHelper = auditHelper;
            _logger = logger;
        }

        private JsonResult ValidationErrorResponse()
        {
            var errors = ModelStateHelper.GetModelErrors(ModelState);
            return Json(new { success = false, message = string.Join("; ", errors) });
        }

        [Permission("EMPLOYEE_VIEW")]
        [Permission("EMPLOYEELOAN_VIEW")]
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

            return View();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Permission("EMPLOYEE_VIEW")]
        [Permission("EMPLOYEELOAN_VIEW")]
        public async Task<IActionResult> GetEmployeeLoansJson()
        {
            var draw = HttpContext.Request.Form["draw"].FirstOrDefault() ?? "1";
            var start = int.TryParse(HttpContext.Request.Form["start"].FirstOrDefault(), out var st) ? st : 0;
            var length = int.TryParse(HttpContext.Request.Form["length"].FirstOrDefault(), out var ln) ? ln : 25;
            var sortColIdx = HttpContext.Request.Form["order[0][column]"].FirstOrDefault() ?? "2";
            var sortColumn = HttpContext.Request.Form[$"columns[{sortColIdx}][data]"].FirstOrDefault();
            var sortDir = HttpContext.Request.Form["order[0][dir]"].FirstOrDefault() ?? "asc";
            var searchValue = HttpContext.Request.Form["search[value]"].FirstOrDefault();

            var filterEmployeeId = HttpContext.Request.Form["employeeId"].FirstOrDefault();
            var filterFullName = HttpContext.Request.Form["fullName"].FirstOrDefault();
            var filterDesignationId = HttpContext.Request.Form["designationId"].FirstOrDefault();
            var filterOrganizationStructureId = HttpContext.Request.Form["organizationStructureId"].FirstOrDefault();
            var filterLoanStatus = HttpContext.Request.Form["loanStatus"].FirstOrDefault();

            var baseQuery = _db.EmployeeLoans
                .Where(l => !l.IsDeleted);

            var recordsTotal = await baseQuery.CountAsync();

            var query = _db.EmployeeLoans
                .Where(l => !l.IsDeleted)
                .Include(l => l.Employee)
                    .ThenInclude(e => e!.Designations.Where(d => d.EffectiveTo == null))
                    .ThenInclude(d => d.Designation)
                .Include(l => l.DeductionType)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filterEmployeeId))
                query = query.Where(l => l.Employee.EmployeeIdNumber.ToLower().Contains(filterEmployeeId.ToLower()));

            if (!string.IsNullOrWhiteSpace(filterFullName))
            {
                var name = filterFullName.ToLower();
                query = query.Where(l =>
                    (l.Employee.FirstName + " " + l.Employee.MiddleName + " " + l.Employee.Surname).ToLower().Contains(name) ||
                    (l.Employee.FirstName + " " + l.Employee.Surname).ToLower().Contains(name));
            }

            if (!string.IsNullOrWhiteSpace(filterDesignationId) && int.TryParse(filterDesignationId, out var desId))
                query = query.Where(l => l.Employee.Designations.Any(d => d.DesignationId == desId && d.EffectiveTo == null));

            if (!string.IsNullOrWhiteSpace(filterOrganizationStructureId) && int.TryParse(filterOrganizationStructureId, out var orgUnitId))
                query = query.Where(l => _db.EmployeeOrganizationAssignments.Any(a =>
                    a.EmployeeId == l.EmployeeId &&
                    a.OrganizationStructureId == orgUnitId &&
                    (a.EffectiveTo == null || a.EffectiveTo >= DateTime.Today)));

            if (!string.IsNullOrWhiteSpace(filterLoanStatus) &&
                Enum.TryParse<EmployeeLoanStatus>(filterLoanStatus, true, out var loanStatusEnum))
                query = query.Where(l => l.Status == loanStatusEnum);

            if (!string.IsNullOrWhiteSpace(searchValue))
            {
                var s = searchValue.ToLower();
                query = query.Where(l =>
                    l.Employee.EmployeeIdNumber.ToLower().Contains(s) ||
                    l.Employee.FirstName.ToLower().Contains(s) ||
                    l.Employee.Surname.ToLower().Contains(s) ||
                    (l.Employee.MiddleName != null && l.Employee.MiddleName.ToLower().Contains(s)) ||
                    (l.LoanName != null && l.LoanName.ToLower().Contains(s)) ||
                    (l.DeductionType != null && l.DeductionType.Name.ToLower().Contains(s)) ||
                    (l.ReferenceNumber != null && l.ReferenceNumber.ToLower().Contains(s)) ||
                    (l.Employee.FirstName + " " + l.Employee.Surname).ToLower().Contains(s) ||
                    l.Status.ToString().ToLower().Contains(s));
            }

            var recordsFiltered = await query.CountAsync();

            query = sortColumn switch
            {
                "employeeIdNumber" => sortDir == "asc"
                    ? query.OrderBy(l => l.Employee.EmployeeIdNumber)
                    : query.OrderByDescending(l => l.Employee.EmployeeIdNumber),
                "employeeName" => sortDir == "asc"
                    ? query.OrderBy(l => l.Employee.Surname).ThenBy(l => l.Employee.FirstName)
                    : query.OrderByDescending(l => l.Employee.Surname).ThenByDescending(l => l.Employee.FirstName),
                "loanName" => sortDir == "asc"
                    ? query.OrderBy(l => l.LoanName ?? "").ThenBy(l => l.DeductionType!.Name)
                    : query.OrderByDescending(l => l.LoanName ?? "").ThenByDescending(l => l.DeductionType!.Name),
                "principalAmount" => sortDir == "asc"
                    ? query.OrderBy(l => l.PrincipalAmount)
                    : query.OrderByDescending(l => l.PrincipalAmount),
                "totalAmountToRepay" => sortDir == "asc"
                    ? query.OrderBy(l => l.TotalAmountToRepay)
                    : query.OrderByDescending(l => l.TotalAmountToRepay),
                "installmentAmount" => sortDir == "asc"
                    ? query.OrderBy(l => l.InstallmentAmount)
                    : query.OrderByDescending(l => l.InstallmentAmount),
                "installmentsPaid" => sortDir == "asc"
                    ? query.OrderBy(l => l.InstallmentsPaid)
                    : query.OrderByDescending(l => l.InstallmentsPaid),
                "remainingBalance" => sortDir == "asc"
                    ? query.OrderBy(l => l.RemainingBalance)
                    : query.OrderByDescending(l => l.RemainingBalance),
                "status" => sortDir == "asc"
                    ? query.OrderBy(l => l.Status)
                    : query.OrderByDescending(l => l.Status),
                _ => query.OrderByDescending(l => l.CreatedOn)
            };

            var page = await query
                .Skip(start)
                .Take(length)
                .Select(l => new
                {
                    id = l.Id,
                    employeeIdNumber = l.Employee.EmployeeIdNumber,
                    employeeName = (l.Employee.FirstName + " " +
                                    (l.Employee.MiddleName != null && l.Employee.MiddleName != ""
                                        ? l.Employee.MiddleName + " "
                                        : "") +
                                    l.Employee.Surname).Trim(),
                    loanName = !string.IsNullOrWhiteSpace(l.LoanName)
                        ? l.LoanName
                        : (l.DeductionType != null ? l.DeductionType.Name : "Loan"),
                    principalAmount = l.PrincipalAmount,
                    totalAmountToRepay = l.TotalAmountToRepay,
                    installmentAmount = l.InstallmentAmount,
                    totalInstallments = l.TotalInstallments,
                    installmentsPaid = l.InstallmentsPaid,
                    remainingBalance = l.RemainingBalance,
                    status = l.Status.ToString()
                })
                .ToListAsync();

            return Json(new
            {
                draw = int.Parse(draw, CultureInfo.InvariantCulture),
                recordsTotal,
                recordsFiltered,
                data = page
            });
        }

        [HttpGet]
        [Permission("EMPLOYEE_VIEW")]
        [Permission("EMPLOYEELOAN_VIEW")]
        public async Task<IActionResult> Create()
        {
            var employees = await _db.Employees
                .Where(e => !e.IsDeleted && e.IsActive)
                .OrderBy(e => e.Surname)
                .ThenBy(e => e.FirstName)
                .Select(e => new { e.Id, Name = e.FullName, e.EmployeeIdNumber })
                .ToListAsync();
            ViewBag.Employees = employees;

            return PartialView("Partials/_CreateEmployeeLoan");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("EMPLOYEE_VIEW")]
        [Permission("EMPLOYEELOAN_VIEW")]
        public async Task<IActionResult> Create(EmployeeLoanViewModel model)
        {
            try
            {
                var loanDeductionType = await _db.DeductionTypes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(dt => dt.Code == "LOAN");

                if (loanDeductionType == null)
                {
                    return Json(new { success = false, message = "Loan deduction type (Code: LOAN) is not configured." });
                }

                model.DeductionTypeId = loanDeductionType.Id;

                if (!ModelState.IsValid)
                {
                    return ValidationErrorResponse();
                }

                var interestRate = model.InterestRate ?? 0M;
                var totalToRepay = model.PrincipalAmount + (model.PrincipalAmount * (interestRate / 100M));
                var installmentAmount = model.InstallmentAmount > 0
                    ? model.InstallmentAmount
                    : Math.Round(totalToRepay / model.TotalInstallments, 2);

                var loan = new EmployeeLoan
                {
                    EmployeeId = model.EmployeeId,
                    DeductionTypeId = loanDeductionType.Id,
                    LoanName = model.LoanName,
                    PrincipalAmount = model.PrincipalAmount,
                    InterestRate = interestRate,
                    TotalInstallments = model.TotalInstallments,
                    InstallmentAmount = installmentAmount,
                    TotalAmountToRepay = totalToRepay,
                    RemainingBalance = totalToRepay,
                    InstallmentsPaid = 0,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    Status = EmployeeLoanStatus.Active,
                    ReferenceNumber = model.ReferenceNumber,
                    Remarks = model.Remarks
                };

                _db.EmployeeLoans.Add(loan);
                await _db.SaveChangesAsync();

                await _auditHelper.LogAuditTrail("CREATE", "EmployeeLoan", "EmployeeLoans", null, loan);

                return Json(new { success = true, message = "Employee loan created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating employee loan");
                return Json(new { success = false, message = "An error occurred while creating the employee loan." });
            }
        }

        [HttpGet]
        [Permission("EMPLOYEE_VIEW")]
        [Permission("EMPLOYEELOAN_VIEW")]
        public async Task<IActionResult> Edit(int id)
        {
            var loan = await _db.EmployeeLoans
                .Include(l => l.Employee)
                .Include(l => l.DeductionType)
                .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);

            if (loan == null)
            {
                return NotFound();
            }

            var vm = new EmployeeLoanViewModel
            {
                Id = loan.Id,
                EmployeeId = loan.EmployeeId,
                EmployeeName = loan.Employee.FullName,
                DeductionTypeId = loan.DeductionTypeId,
                DeductionTypeName = loan.DeductionType?.Name ?? "Loan",
                LoanName = loan.LoanName,
                PrincipalAmount = loan.PrincipalAmount,
                InterestRate = loan.InterestRate,
                TotalInstallments = loan.TotalInstallments,
                InstallmentAmount = loan.InstallmentAmount,
                TotalAmountToRepay = loan.TotalAmountToRepay,
                RemainingBalance = loan.RemainingBalance,
                InstallmentsPaid = loan.InstallmentsPaid,
                StartDate = loan.StartDate,
                EndDate = loan.EndDate,
                Status = loan.Status,
                ReferenceNumber = loan.ReferenceNumber,
                Remarks = loan.Remarks
            };

            return PartialView("Partials/_EditEmployeeLoan", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("EMPLOYEE_VIEW")]
        [Permission("EMPLOYEELOAN_VIEW")]
        public async Task<IActionResult> Edit(EmployeeLoanViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            try
            {
                var loanDeductionTypeId = await _db.DeductionTypes
                    .Where(dt => dt.Code == "LOAN")
                    .Select(dt => (int?)dt.Id)
                    .FirstOrDefaultAsync();

                if (!loanDeductionTypeId.HasValue)
                {
                    return Json(new { success = false, message = "Loan deduction type (Code: LOAN) is not configured." });
                }

                var loan = await _db.EmployeeLoans
                    .FirstOrDefaultAsync(l => l.Id == model.Id && !l.IsDeleted);

                if (loan == null)
                {
                    return Json(new { success = false, message = "Employee loan not found." });
                }

                var oldData = JsonSerializer.Serialize(loan);

                loan.DeductionTypeId = loanDeductionTypeId.Value;
                loan.LoanName = model.LoanName;
                loan.PrincipalAmount = model.PrincipalAmount;
                loan.InterestRate = model.InterestRate;
                loan.TotalInstallments = model.TotalInstallments;
                loan.InstallmentAmount = model.InstallmentAmount;
                loan.TotalAmountToRepay = model.TotalAmountToRepay;
                loan.RemainingBalance = model.RemainingBalance;
                loan.InstallmentsPaid = model.InstallmentsPaid;
                loan.StartDate = model.StartDate;
                loan.EndDate = model.EndDate;
                loan.Status = model.Status;
                loan.ReferenceNumber = model.ReferenceNumber;
                loan.Remarks = model.Remarks;

                _db.EmployeeLoans.Update(loan);
                await _db.SaveChangesAsync();

                await _auditHelper.LogAuditTrail("UPDATE", "EmployeeLoan", "EmployeeLoans", oldData, loan);

                return Json(new { success = true, message = "Employee loan updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee loan");
                return Json(new { success = false, message = "An error occurred while updating the employee loan." });
            }
        }

        [HttpGet]
        [Permission("EMPLOYEE_VIEW")]
        [Permission("EMPLOYEELOAN_VIEW")]
        public async Task<IActionResult> Details(int id)
        {
            var loan = await _db.EmployeeLoans
                .Include(l => l.Employee)
                .Include(l => l.DeductionType)
                .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);

            if (loan == null)
            {
                return NotFound();
            }

            var vm = new EmployeeLoanViewModel
            {
                Id = loan.Id,
                EmployeeId = loan.EmployeeId,
                EmployeeName = loan.Employee.FullName,
                DeductionTypeId = loan.DeductionTypeId,
                DeductionTypeName = loan.DeductionType?.Name ?? "Loan",
                LoanName = loan.LoanName,
                PrincipalAmount = loan.PrincipalAmount,
                InterestRate = loan.InterestRate,
                TotalInstallments = loan.TotalInstallments,
                InstallmentAmount = loan.InstallmentAmount,
                TotalAmountToRepay = loan.TotalAmountToRepay,
                RemainingBalance = loan.RemainingBalance,
                InstallmentsPaid = loan.InstallmentsPaid,
                StartDate = loan.StartDate,
                EndDate = loan.EndDate,
                Status = loan.Status,
                ReferenceNumber = loan.ReferenceNumber,
                Remarks = loan.Remarks
            };

            return PartialView("Partials/_ViewEmployeeLoan", vm);
        }

        [HttpGet]
        [Permission("EMPLOYEE_VIEW")]
        [Permission("EMPLOYEELOAN_VIEW")]
        public async Task<IActionResult> Delete(int id)
        {
            var loan = await _db.EmployeeLoans
                .Include(l => l.Employee)
                .Include(l => l.DeductionType)
                .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);

            if (loan == null)
            {
                return NotFound();
            }

            var loanDisplayName = !string.IsNullOrWhiteSpace(loan.LoanName)
                ? loan.LoanName
                : (loan.DeductionType != null ? loan.DeductionType.Name : "Loan");
            var vm = new EmployeeLoanViewModel
            {
                Id = loan.Id,
                EmployeeId = loan.EmployeeId,
                EmployeeName = loan.Employee.FullName,
                LoanName = loanDisplayName,
                PrincipalAmount = loan.PrincipalAmount,
                TotalAmountToRepay = loan.TotalAmountToRepay,
                RemainingBalance = loan.RemainingBalance,
                Status = loan.Status,
                ReferenceNumber = loan.ReferenceNumber
            };

            return PartialView("Partials/_DeleteEmployeeLoan", vm);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Permission("EMPLOYEE_VIEW")]
        [Permission("EMPLOYEELOAN_VIEW")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var loan = await _db.EmployeeLoans
                    .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);

                if (loan == null)
                {
                    return Json(new { success = false, message = "Employee loan not found." });
                }

                var oldData = JsonSerializer.Serialize(loan);
                loan.IsDeleted = true;
                _db.EmployeeLoans.Update(loan);
                await _db.SaveChangesAsync();

                await _auditHelper.LogAuditTrail("DELETE", "EmployeeLoan", "EmployeeLoans", oldData, null);

                return Json(new { success = true, message = "Employee loan deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting employee loan");
                return Json(new { success = false, message = "An error occurred while deleting the employee loan." });
            }
        }

        [HttpGet]
        [Permission("EMPLOYEE_VIEW")]
        [Permission("EMPLOYEELOAN_VIEW")]
        public async Task<IActionResult> GetEmployeeLoans(Guid? employeeId = null)
        {
            try
            {
                var query = _db.EmployeeLoans
                    .Include(l => l.Employee)
                    .Include(l => l.DeductionType)
                    .Where(l => !l.IsDeleted);

                if (employeeId.HasValue)
                {
                    query = query.Where(l => l.EmployeeId == employeeId.Value);
                }

                var loans = await query
                    .OrderByDescending(l => l.CreatedOn)
                    .ToListAsync();

                var data = loans.Select(l => new
                {
                    l.Id,
                    EmployeeName = l.Employee.FullName,
                    EmployeeIdNumber = l.Employee.EmployeeIdNumber,
                    LoanName = !string.IsNullOrWhiteSpace(l.LoanName) ? l.LoanName : (l.DeductionType != null ? l.DeductionType.Name : "Loan"),
                    l.PrincipalAmount,
                    l.TotalAmountToRepay,
                    l.InstallmentAmount,
                    l.TotalInstallments,
                    l.InstallmentsPaid,
                    l.RemainingBalance,
                    Status = l.Status.ToString(),
                    StartDate = l.StartDate.ToString("yyyy-MM-dd"),
                    EndDate = l.EndDate.HasValue ? l.EndDate.Value.ToString("yyyy-MM-dd") : null,
                    l.ReferenceNumber
                }).ToList();

                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employee loans");
                return Json(new { success = false, message = "Error retrieving employee loans." });
            }
        }
    }
}

