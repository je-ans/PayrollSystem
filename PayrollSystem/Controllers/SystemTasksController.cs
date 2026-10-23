using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.Controllers
{
    public class SystemTasksController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<SystemTasksController> _logger;
        private readonly AuditHelper _auditHelper;

        public SystemTasksController(ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<SystemTasksController> logger)
        {
            _db = db;
            _userManager = userManager;
            _auditHelper = auditHelper;
            _logger = logger;
        }

        //------------------------------------------------------- Private Methods ---------------------------------------------------------------

        private async Task LogAuditTrailAsync(string action, object oldData, object newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "System Tasks", "SystemTasks", oldData, newData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit trail for {Action}. OldData: {OldData}, NewData: {NewData}", action, oldData, newData);
            }
        }

        //------------------------------------------------------- System Tasks View ---------------------------------------------------------------

        [Permission("SYSTEMTASKS_VIEW")]
        public async Task<IActionResult> Index()
        {
            var tasks = await _db.SystemTasks
                .Include(s => s.Parent)
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .ToListAsync();

            return View(tasks);
        }

        [Permission("SYSTEMTASKS_VIEW")]
        public async Task<IActionResult> View(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var systemTask = await _db.SystemTasks
                .AsNoTracking()
                .Include(s => s.CreatedBy)
                .Include(s => s.Parent)
                .Include(s => s.UpdatedBy)
                .Where(s => !s.IsDeleted)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (systemTask == null)
            {
                return NotFound();
            }

            return PartialView("View", systemTask);
        }

        //------------------------------------------------------- Create System Tasks ---------------------------------------------------------------

        [Permission("SYSTEMTASKS_VIEW")]
        public IActionResult Create()
        {
            ViewData["ParentId"] = new SelectList(_db.SystemTasks, "Id", "TaskName");
            return PartialView("Create");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("SYSTEMTASKS_VIEW")]
        public async Task<IActionResult> Create(SystemTask systemTask)
        {
            if (!ModelState.IsValid)
            {
                var errorMessages = ModelStateHelper.GetModelErrors(ModelState);
                _logger.LogWarning("Validation failed: {Errors}", string.Join(", ", errorMessages));
                return Json(new { success = false, message = errorMessages });
            }

            try
            {
                systemTask.CreatedOn = DateTime.Now;
                systemTask.CreatedById = _userManager.GetUserId(User);

                _db.SystemTasks.Add(systemTask);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("Failed to save system task: {TaskName}", systemTask.TaskName);
                    return Json(new { success = false, message = "Failed to create system task." });
                }

                await LogAuditTrailAsync("Create System Task", null, systemTask);
                return Json(new { success = true, message = "System task created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating system task: {TaskName}", systemTask?.TaskName);
                return Json(new { success = false, message = $"An error occurred: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }

        //------------------------------------------------------- Edit System Tasks ---------------------------------------------------------------

        [Permission("SYSTEMTASKS_VIEW")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var systemTask = await _db.SystemTasks.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
            if (systemTask == null)
            {
                return NotFound();
            }

            ViewData["ParentId"] = new SelectList(_db.SystemTasks, "Id", "TaskName", systemTask.ParentId);
            return PartialView("Edit", systemTask);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("SYSTEMTASKS_VIEW")]
        public async Task<IActionResult> Edit(int id, SystemTask systemTask)
        {
            if (!ModelState.IsValid)
            {
                var errorMessages = ModelStateHelper.GetModelErrors(ModelState);
                _logger.LogWarning("Validation failed: {Errors}", string.Join(", ", errorMessages));
                return Json(new { success = false, message = errorMessages });
            }

            try
            {
                var existingTask = await _db.SystemTasks.FindAsync(id);
                if (existingTask == null)
                {
                    return Json(new { success = false, message = "System task not found." });
                }

                // Clone old data before modification
                var oldData = new SystemTask
                {
                    Id = existingTask.Id,
                    Code = existingTask.Code,
                    TaskName = existingTask.TaskName,
                    ParentId = existingTask.ParentId,
                    OrderNumber = existingTask.OrderNumber,
                    CreatedOn = existingTask.CreatedOn,
                    CreatedById = existingTask.CreatedById,
                    UpdatedOn = existingTask.UpdatedOn,
                    UpdatedById = existingTask.UpdatedById
                };

                existingTask.Code = systemTask.Code;
                existingTask.TaskName = systemTask.TaskName;
                existingTask.ParentId = systemTask.ParentId;
                existingTask.OrderNumber = systemTask.OrderNumber;
                existingTask.UpdatedOn = DateTime.Now;
                existingTask.UpdatedById = _userManager.GetUserId(User);

                await _db.SaveChangesAsync();

                // Pass correct oldData and newData
                await LogAuditTrailAsync("Update System Task", oldData, existingTask);
                return Json(new { success = true, message = "System task updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating system task: {TaskName}", systemTask?.TaskName);
                return Json(new { success = false, message = $"An error occurred: {ex.InnerException?.Message ?? ex.Message}" });
            }
        }


        //------------------------------------------------------- Delete System Tasks ---------------------------------------------------------------

        [Permission("SYSTEMTASKS_VIEW")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var systemTask = await _db.SystemTasks
                .AsNoTracking()
                .Include(s => s.CreatedBy)
                .Include(s => s.Parent)
                .Include(s => s.UpdatedBy)
                .Where(s => !s.IsDeleted)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (systemTask == null)
            {
                return NotFound();
            }

            return PartialView("Delete", systemTask);
        }

        [HttpPost]
        [Permission("SYSTEMTASKS_VIEW")]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            var sysTask = await _db.SystemTasks.FindAsync(id);
            if (sysTask == null || sysTask.IsDeleted)
            {
                _logger.LogWarning("Attempted to delete non-existent or already deleted system task with ID {Id}.", id);
                return Json(new { success = false, message = "System Task not found or already deleted." });
            }

            var oldData = new
            {
                sysTask.Id,
                sysTask.Code,
                sysTask.TaskName,
                sysTask.ParentId,
                sysTask.OrderNumber,
                sysTask.CreatedOn,
                sysTask.CreatedById,
                sysTask.UpdatedOn,
                sysTask.UpdatedById
            };

            sysTask.IsDeleted = true;
            sysTask.UpdatedById = _userManager.GetUserId(User);
            sysTask.UpdatedOn = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
                await LogAuditTrailAsync("Delete System Task", oldData, null);
                return Json(new { success = true, message = "System task deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting system task ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while deleting the system task." });
            }
        }
    }
}
