using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    public class DepartmentController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<DepartmentController> _logger;

        public DepartmentController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<DepartmentController> logger)
        {
            _userManager = userManager;
            _db = db;
            _auditHelper = auditHelper;
            _logger = logger;
        }


        //=======================================================Private Method=================================================================

        private async Task LogAuditTrailAsync(string action, object oldData, object newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "Department", "Departments", oldData, newData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit trail for {Action}. OldData: {OldData}, NewData: {NewData}",
                    action, oldData, newData);
            }
        }

        private JsonResult ValidationErrorResponse()
        {
            var errorMessages = ModelStateHelper.GetModelErrors(ModelState);
            _logger.LogWarning("Validation failed: {Errors}", string.Join("; ", errorMessages));
            return Json(new { success = false, message = string.Join("; ", errorMessages) });

        }

        //=======================================================Get Department List=============================================================

        [Permission("DEPARTMENT_VIEW")]
        public async Task<ActionResult> Index()
        {
            var departments = await _db.Departments
                .AsNoTracking()
                .Where(d => !d.IsDeleted)
                .ToListAsync();

            // Get all departments that have users assigned
            var departmentsWithUsers = new List<int>();
            foreach (var department in departments)
            {
                var hasUsers = await _db.Users.AnyAsync(u => u.DepartmentId == department.Id && !u.IsDeleted);
                if (hasUsers)
                {
                    departmentsWithUsers.Add(department.Id);
                }
            }

            ViewBag.DepartmentsWithUsers = departmentsWithUsers;
            return View(departments);
        }

        //=======================================================Create Department==============================================================

        [Permission("DEPARTMENT_VIEW")]
        public ActionResult CreateDepartment()
        {
            return PartialView("Partials/CreateDepartment");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("DEPARTMENT_VIEW")]
        public async Task<ActionResult> CreateDepartment(DepartmentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            try
            {
                if (await _db.Departments.AnyAsync(d =>
                    d.DepartmentName == model.DepartmentName && !d.IsDeleted))
                {
                    return Json(new { success = false, message = "Department name already exists." });
                }

                var department = new Department
                {
                    DepartmentName = model.DepartmentName,
                    DepartmentDescription = model.DepartmentDescription,
                    IsActive = model.IsActive,
                    CreatedById = _userManager.GetUserId(User),
                    CreatedOn = DateTime.UtcNow
                };

                _db.Departments.Add(department);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("Failed to save department: {DepartmentName}", model.DepartmentName);
                    return Json(new { success = false, message = "Failed to create department." });
                }

                await LogAuditTrailAsync("Create Department", null, department);
                return Json(new { success = true, message = "Department created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating department: {DepartmentName}", model.DepartmentName);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }


        //=======================================================Edit Department===================================================================


        [HttpGet]
        [Permission("DEPARTMENT_VIEW")]
        public async Task<ActionResult> EditDepartment(int id)
        {
            var department = await _db.Departments.FindAsync(id);
            if (department == null) return NotFound();

            var viewModel = new DepartmentViewModel
            {
                Id = department.Id,
                DepartmentName = department.DepartmentName,
                DepartmentDescription = department.DepartmentDescription,
                IsActive = department.IsActive
            };

            return PartialView("Partials/EditDepartment", viewModel);
        }


        [HttpPost]
        [Permission("DEPARTMENT_VIEW")]
        public async Task<ActionResult> EditDepartment(int id, DepartmentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            var department = await _db.Departments.FindAsync(id);
            if (department == null)
            {
                _logger.LogError("Department with ID {Id} not found.", id);
                return Json(new { success = false, message = "Department not found." });
            }

            if (department.DepartmentName != model.DepartmentName &&
                await _db.Departments.AnyAsync(d =>
                    d.DepartmentName == model.DepartmentName && !d.IsDeleted && d.Id != id))
            {
                return Json(new { success = false, message = "Another department with the same name already exists." });
            }

            var oldDepartmentData = JsonSerializer.Deserialize<Department>(
                JsonSerializer.Serialize(department));

            // Manual mapping
            department.DepartmentName = model.DepartmentName;
            department.DepartmentDescription = model.DepartmentDescription;
            department.IsActive = model.IsActive;
            department.UpdatedById = _userManager.GetUserId(User);
            department.UpdatedOn = DateTime.UtcNow;

            try
            {
                _db.Departments.Update(department);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("No records updated for department ID {Id}", id);
                    return Json(new { success = false, message = "No changes were made." });
                }

                await LogAuditTrailAsync("Update Department", oldDepartmentData, department);
                return Json(new { success = true, message = "Department updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Department ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while updating." });
            }
        }


        //=======================================================View Department===================================================================


        [HttpGet]
        [Permission("DEPARTMENT_VIEW")]
        public async Task<ActionResult> ViewDepartment(int id)
        {
            var department = await _db.Departments
                .AsNoTracking()
                .Where(d => d.Id == id && !d.IsDeleted)
                .FirstOrDefaultAsync();

            if (department == null)
            {
                _logger.LogWarning("Department with ID {Id} not found.", id);
                return NotFound();
            }

            var viewModel = new DepartmentViewModel
            {
                Id = department.Id,
                DepartmentName = department.DepartmentName,
                DepartmentDescription = department.DepartmentDescription,
                IsActive = department.IsActive,
            };

            return PartialView("Partials/ViewDepartment", viewModel);
        }


        //=======================================================Delete Department===================================================================

        [HttpGet]
        [Permission("DEPARTMENT_VIEW")]
        public async Task<ActionResult> DeleteDepartment(int id)
        {
            var department = await _db.Departments
                .AsNoTracking()
                .Where(d => d.Id == id && !d.IsDeleted)
                .FirstOrDefaultAsync();

            if (department == null)
            {
                _logger.LogWarning("Department with ID {Id} not found.", id);
                return NotFound();
            }

            // Check for users assigned to this department
            var hasUsers = await _db.Users.AnyAsync(u => u.DepartmentId == department.Id && !u.IsDeleted);

            // If any restrictions exist, show warning
            if (hasUsers)
            {
                var viewModel = new DepartmentViewModel
                {
                    Id = department.Id,
                    DepartmentName = department.DepartmentName,
                    DepartmentDescription = department.DepartmentDescription,
                    IsActive = department.IsActive,
                };
                ViewBag.HasUsers = hasUsers;
                return PartialView("Partials/DeleteWarningDepartment", viewModel);
            }

            // If no restrictions, show normal delete confirmation
            var deleteViewModel = new DepartmentViewModel
            {
                Id = department.Id,
                DepartmentName = department.DepartmentName,
                DepartmentDescription = department.DepartmentDescription,
                IsActive = department.IsActive,
            };

            return PartialView("Partials/DeleteDepartment", deleteViewModel);
        }

        [HttpPost]
        [Permission("DEPARTMENT_VIEW")]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            var department = await _db.Departments.FindAsync(id);
            if (department == null || department.IsDeleted)
            {
                _logger.LogWarning("Department with ID {Id} not found or already deleted.", id);
                return Json(new { success = false, message = "Department not found or already deleted." });
            }

            // Check for users assigned to this department
            var hasUsers = await _db.Users.AnyAsync(u => u.DepartmentId == department.Id && !u.IsDeleted);
            if (hasUsers)
            {
                _logger.LogWarning("Cannot delete department with assigned users: {DepartmentName}", department.DepartmentName);
                return Json(new
                {
                    success = false,
                    message = "Cannot delete department because it has assigned users. Please reassign users first."
                });
            }

            var oldData = new DepartmentViewModel
            {
                Id = department.Id,
                DepartmentName = department.DepartmentName,
                DepartmentDescription = department.DepartmentDescription,
                IsActive = department.IsActive,
            };

            department.IsDeleted = true;
            department.UpdatedById = _userManager.GetUserId(User);
            department.UpdatedOn = DateTime.UtcNow;

            try
            {
                _db.Departments.Update(department);
                var result = await _db.SaveChangesAsync();

                if (result > 0)
                {
                    await LogAuditTrailAsync("Delete Department", oldData, null);
                    return Json(new { success = true, message = "Department deleted successfully." });
                }

                _logger.LogError("Failed to delete department ID {Id}.", id);
                return Json(new { success = false, message = "Failed to delete department." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting department ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while deleting." });
            }
        }

        //==========================================================================================================================
    }
}
