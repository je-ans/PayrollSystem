using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    [Permission("PERMISSION_ASSIGN")]
    public class AssignRolePermissionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AssignRolePermissionController> _logger;
        private readonly AuditHelper _auditHelper;
        private readonly RoleManager<ApplicationRole> _roleManager;

        public AssignRolePermissionController(ApplicationDbContext context,
             UserManager<ApplicationUser> userManager,
             AuditHelper auditHelper,
             ILogger<AssignRolePermissionController> logger,
              RoleManager<ApplicationRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _auditHelper = auditHelper;
            _logger = logger;
            _roleManager = roleManager;
        }

        private async Task LogAuditTrailAsync(string action, object oldData, object newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "User Role Profiles", "AssignRolePermission", oldData, newData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit trail for {Action}. OldData: {OldData}, NewData: {NewData}", action, oldData, newData);

            }
        }

        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.AssignRolePermissions
                .AsNoTracking()
                .Include(u => u.CreatedBy)
                .Include(u => u.Role)
                .Include(u => u.Task)
                .Include(u => u.UpdatedBy);
            return View(await applicationDbContext.ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var assignRolePermission = await _context.AssignRolePermissions
                .AsNoTracking()
                .Include(u => u.CreatedBy)
                .Include(u => u.Role)
                .Include(u => u.Task)
                .Include(u => u.UpdatedBy)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (assignRolePermission == null)
            {
                return NotFound();
            }

            return View(assignRolePermission);
        }

        [HttpGet]
        public async Task<IActionResult> UserRights(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("Invalid role ID.");
            }

            AssignRolePermissionsViewModel vm = new();
            vm.RoleId = id;
            var allRoles = await _context.Roles.AsNoTracking().OrderBy(u => u.Name).ToListAsync();

            // Set selected role in dropdown
            ViewBag.RoleId = new SelectList(allRoles, "Id", "Name", id);

            // Fetch system tasks with proper Includes, excluding deleted ones
            vm.SystemTasks = await _context.SystemTasks
                .AsNoTracking()
                .Where(u => u.Parent == null && !u.IsDeleted) // Exclude deleted tasks
                .Include(t => t.ChildTasks.Where(ct => !ct.IsDeleted)) // Exclude deleted child tasks
                    .ThenInclude(t => t.ChildTasks.Where(ct => !ct.IsDeleted))
                    .ThenInclude(t => t.ChildTasks.Where(ct => !ct.IsDeleted))
                .OrderBy(u => u.OrderNumber)
                .ToListAsync();

            // Get assigned rights for the selected role, excluding deleted ones
            vm.RightsIdsAssigned = await _context.AssignRolePermissions
                .AsNoTracking()
                .Where(u => u.RoleId == id && u.TaskId.HasValue && !u.IsDeleted)
                .Select(u => u.TaskId.Value)
                .ToListAsync();

            return PartialView("UserRights", vm);
        }

        [HttpPost]
        public async Task<IActionResult> UserRights(AssignRolePermissionsViewModel userRights)
        {
            try
            {
                if (userRights.Ids == null || !userRights.Ids.Any())
                {
                    return Json(new { success = false, message = "No tasks provided." });
                }

                // Verify the role exists in the correct table
                var roleExists = await _roleManager.Roles.AnyAsync(r => r.Id == userRights.RoleId);
                if (!roleExists)
                {
                    return Json(new { success = false, message = "Role not found." });
                }

                // Remove existing rights
                var existingPermissions = await _context.AssignRolePermissions
                    .Where(arp => arp.RoleId == userRights.RoleId)
                    .ToListAsync();

                _context.AssignRolePermissions.RemoveRange(existingPermissions);

                // Add new permissions
                var userId = _userManager.GetUserId(User);
                var now = DateTime.UtcNow;

                var newPermissions = userRights.Ids.Select(taskId => new AssignRolePermission
                {
                    TaskId = taskId,
                    RoleId = userRights.RoleId,
                    CreatedById = userId,
                    CreatedOn = now,
                    UpdatedById = userId,
                    UpdatedOn = now,
                    IsDeleted = false
                });

                await _context.AssignRolePermissions.AddRangeAsync(newPermissions);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Permissions updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating permissions for role: {RoleId}", userRights.RoleId);
                return Json(new { success = false, message = "An error occurred while updating permissions." });
            }
        }

        public IActionResult Create()
        {
            ViewData["RoleId"] = new SelectList(_context.Roles.AsNoTracking(), "Id", "Name");
            ViewData["TaskId"] = new SelectList(_context.SystemTasks.AsNoTracking(), "Id", "TaskName");
            return PartialView("Create");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AssignRolePermission assignRolePermission)
        {
            if (!ModelState.IsValid)
            {
                var errorMessages = ModelStateHelper.GetModelErrors(ModelState);
                _logger.LogWarning("Validation failed: {Errors}", string.Join(", ", errorMessages));
                return Json(new { success = false, message = errorMessages });
            }

            try
            {
                assignRolePermission.CreatedOn = DateTime.Now;
                assignRolePermission.CreatedById = _userManager.GetUserId(User); // Ensure _userManager is injected

                _context.Add(assignRolePermission);
                var result = await _context.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("Failed to save system task: {TaskName}", assignRolePermission.Task);
                    return Json(new { success = false, message = "Failed to create system task. No changes were made." });
                }

                await LogAuditTrailAsync("Create System task", null, assignRolePermission);
                return Json(new { success = true, message = "System task created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating system task: {TaskName}", assignRolePermission?.Task);

                return Json(new
                {
                    success = false,
                    message = $"An error occurred: {ex.InnerException?.Message ?? ex.Message}"
                });
            }
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var assignRolePermission = await _context.AssignRolePermissions
                .AsNoTracking()
                .Include(u => u.CreatedBy)
                .Include(u => u.Role)
                .Include(u => u.Task)
                .Include(u => u.UpdatedBy)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (assignRolePermission == null)
            {
                return NotFound();
            }

            ViewData["RoleId"] = new SelectList(_context.Roles.AsNoTracking(), "Id", "Name", assignRolePermission.RoleId);
            ViewData["TaskId"] = new SelectList(_context.SystemTasks.AsNoTracking(), "Id", "TaskName", assignRolePermission.TaskId);
            return PartialView("Edit", assignRolePermission);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,TaskId,RoleId,CreatedById,CreatedOn,UpdatedById,UpdatedOn,IsDeleted")] AssignRolePermission assignRolePermission)
        {
            if (id != assignRolePermission.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(assignRolePermission);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AssignRolePermissionExists(assignRolePermission.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewData["RoleId"] = new SelectList(_context.Roles, "Id", "Name", assignRolePermission.RoleId);
            ViewData["TaskId"] = new SelectList(_context.SystemTasks, "Id", "TaskName", assignRolePermission.TaskId);
            return View(assignRolePermission);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var assignRolePermission = await _context.AssignRolePermissions
                .AsNoTracking()
                .Include(u => u.CreatedBy)
                .Include(u => u.Role)
                .Include(u => u.Task)
                .Include(u => u.UpdatedBy)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (assignRolePermission == null)
            {
                return NotFound();
            }

            return View(assignRolePermission);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var assignRolePermission = await _context.AssignRolePermissions.FindAsync(id);
            if (assignRolePermission != null)
            {
                _context.AssignRolePermissions.Remove(assignRolePermission);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AssignRolePermissionExists(int id)
        {
            return _context.AssignRolePermissions.Any(e => e.Id == id);
        }
    }
}
