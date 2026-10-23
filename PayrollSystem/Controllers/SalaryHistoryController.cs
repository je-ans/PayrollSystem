using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.Controllers
{
    [Authorize]
    public class SalaryHistoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<SalaryHistoryController> _logger;

        public SalaryHistoryController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<SalaryHistoryController> logger)
        {
            _context = context;
            _userManager = userManager;
            _auditHelper = auditHelper;
            _logger = logger;
        }

        //=======================================================Private Method=================================================================

        private async Task LogAuditTrailAsync(string action, object oldData, object newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "SalaryHistory", "SalaryHistories", oldData, newData);
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

            return Json(new { success = false, message = "Validation failed: " + string.Join(", ", errors) });
        }

        //=======================================================Public Method=================================================================

        [Permission("SALARY_HISTORY_VIEW")]
        public async Task<IActionResult> Index()
        {
            var salaryHistories = await _context.SalaryHistories
                .Include(sh => sh.Employee)
                .Include(sh => sh.ApprovedBy)
                .OrderByDescending(sh => sh.CreatedOn)
                .ToListAsync();
            return View(salaryHistories);
        }

        [Permission("SALARY_HISTORY_VIEW")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var salaryHistory = await _context.SalaryHistories
                .Include(sh => sh.Employee)
                .Include(sh => sh.ApprovedBy)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (salaryHistory == null)
            {
                return NotFound();
            }

            return PartialView("Partials/_ViewSalaryHistory", salaryHistory);
        }

        [Permission("SALARY_HISTORY_VIEW")]
        public async Task<IActionResult> EmployeeHistory(Guid employeeId)
        {
            var salaryHistories = await _context.SalaryHistories
                .Include(sh => sh.Employee)
                .Include(sh => sh.ApprovedBy)
                .Where(sh => sh.EmployeeId == employeeId)
                .OrderByDescending(sh => sh.CreatedOn)
                .ToListAsync();

            ViewBag.EmployeeId = employeeId;
            ViewBag.EmployeeName = salaryHistories.FirstOrDefault()?.Employee?.FullName ?? "Unknown Employee";

            return View("EmployeeHistory", salaryHistories);
        }

        [Permission("SALARY_HISTORY_VIEW")]
        public IActionResult Create()
        {
            ViewData["EmployeeId"] = new SelectList(_context.Employees.Where(e => e.IsActive), "Id", "FullName");
            return PartialView("Partials/_CreateSalaryHistory");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("SALARY_HISTORY_VIEW")]
        public async Task<IActionResult> Create([Bind("EmployeeId,PreviousBasicSalary,NewBasicSalary,ChangeType,Reason,EffectiveDate")] SalaryHistory salaryHistory)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            try
            {
                salaryHistory.ApprovedById = _userManager.GetUserId(User);
                salaryHistory.ApprovalDate = DateTime.UtcNow;
                salaryHistory.CreatedById = _userManager.GetUserId(User);
                salaryHistory.CreatedOn = DateTime.UtcNow;

                _context.Add(salaryHistory);
                await _context.SaveChangesAsync();

                await LogAuditTrailAsync("Create Salary History", null, salaryHistory);
                return Json(new { success = true, message = "Salary history record created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Salary History for Employee ID {EmployeeId}", salaryHistory.EmployeeId);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }

        [Permission("SALARY_HISTORY_VIEW")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var salaryHistory = await _context.SalaryHistories.FindAsync(id);
            if (salaryHistory == null)
            {
                return NotFound();
            }
            ViewData["EmployeeId"] = new SelectList(_context.Employees.Where(e => e.IsActive), "Id", "FullName", salaryHistory.EmployeeId);
            return PartialView("Partials/_EditSalaryHistory", salaryHistory);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("SALARY_HISTORY_VIEW")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,EmployeeId,PreviousBasicSalary,NewBasicSalary,ChangeType,Reason,EffectiveDate,ApprovedById,ApprovalDate")] SalaryHistory salaryHistory)
        {
            if (id != salaryHistory.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            var existingHistory = await _context.SalaryHistories.FindAsync(id);
            if (existingHistory == null)
            {
                _logger.LogError("Salary History with ID {Id} not found.", id);
                return Json(new { success = false, message = "Salary history not found." });
            }

            var oldData = new
            {
                existingHistory.EmployeeId,
                existingHistory.PreviousBasicSalary,
                existingHistory.NewBasicSalary,
                existingHistory.ChangeType,
                existingHistory.Reason,
                existingHistory.EffectiveDate
            };

            try
            {
                existingHistory.EmployeeId = salaryHistory.EmployeeId;
                existingHistory.PreviousBasicSalary = salaryHistory.PreviousBasicSalary;
                existingHistory.NewBasicSalary = salaryHistory.NewBasicSalary;
                existingHistory.ChangeType = salaryHistory.ChangeType;
                existingHistory.Reason = salaryHistory.Reason;
                existingHistory.EffectiveDate = salaryHistory.EffectiveDate;
                existingHistory.ApprovedById = salaryHistory.ApprovedById;
                existingHistory.ApprovalDate = salaryHistory.ApprovalDate;
                existingHistory.UpdatedById = _userManager.GetUserId(User);
                existingHistory.UpdatedOn = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var newData = new
                {
                    existingHistory.EmployeeId,
                    existingHistory.PreviousBasicSalary,
                    existingHistory.NewBasicSalary,
                    existingHistory.ChangeType,
                    existingHistory.Reason,
                    existingHistory.EffectiveDate
                };

                await LogAuditTrailAsync("Update Salary History", oldData, newData);
                return Json(new { success = true, message = "Salary history updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Salary History with ID {Id}", id);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }

        [Permission("SALARY_HISTORY_VIEW")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var salaryHistory = await _context.SalaryHistories
                .Include(sh => sh.Employee)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (salaryHistory == null)
            {
                return NotFound();
            }

            return PartialView("Partials/_DeleteSalaryHistory", salaryHistory);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Permission("SALARY_HISTORY_VIEW")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var salaryHistory = await _context.SalaryHistories.FindAsync(id);
            if (salaryHistory == null)
            {
                return Json(new { success = false, message = "Salary history not found." });
            }

            try
            {
                await LogAuditTrailAsync("Delete Salary History", salaryHistory, null);

                _context.SalaryHistories.Remove(salaryHistory);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Salary history deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Salary History with ID {Id}", id);
                return Json(new { success = false, message = "An error occurred while deleting." });
            }
        }
    }
}