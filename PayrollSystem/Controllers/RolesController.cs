using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    public class RolesController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<RolesController> _logger;
        private readonly IMemoryCache _cache;

        public RolesController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            SignInManager<ApplicationUser> signInManager,
            AuditHelper auditHelper,
            ILogger<RolesController> logger,
            IMemoryCache cache)
        {
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _auditHelper = auditHelper;
            _logger = logger;
            _cache = cache;
        }


        //===========================================================================Private Methods==================================================================

        private async Task LogAuditTrailAsync(string action, object oldData, object newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "Role Management", "Role", oldData, newData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit trail for {Action}. OldData: {OldData}, NewData: {NewData}",
                    action, oldData, newData);
            }
        }

        private RolesViewModel MapToViewModel(ApplicationRole role)
        {
            return new RolesViewModel
            {
                Id = role.Id,
                RoleName = role.Name,
                Description = role.Description,
                Remarks = role.Remarks,
                IsActive = role.IsActive
            };
        }

        private async Task<bool> IsRoleNameAvailable(string roleName, string excludeRoleId = null)
        {
            var normalizedName = _roleManager.NormalizeKey(roleName);
            var query = _db.Roles.Where(r => r.NormalizedName == normalizedName && !r.IsDeleted);

            if (!string.IsNullOrEmpty(excludeRoleId))
            {
                query = query.Where(r => r.Id != excludeRoleId);
            }

            return !await query.AnyAsync();
        }

        private string BuildUpdatedRemarks(string existingRemarks, string additionalNote, string action)
        {
            var currentUser = _userManager.GetUserAsync(User).Result;
            var userName = currentUser?.UserName ?? "System";
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            var newRemarks = $"{existingRemarks} {action} by {userName} on {timestamp}.";

            if (!string.IsNullOrEmpty(additionalNote))
            {
                newRemarks += $"\nNote: {additionalNote}";
            }

            return newRemarks;
        }

        private JsonResult ValidationErrorResponse()
        {
            var errorMessages = ModelStateHelper.GetModelErrors(ModelState);
            _logger.LogWarning("Validation failed: {Errors}", string.Join("; ", errorMessages));
            return Json(new { success = false, message = string.Join("; ", errorMessages) });
        }


        //==============================================================Display Role List==================================================================

        [Permission("ROLE_VIEW")]
        public async Task<ActionResult> Index()
        {
            try
            {
                var roles = await _db.Roles
                    .AsNoTracking()
                    .Where(r => !r.IsDeleted)
                    .ToListAsync();

                var roleIdsWithUsers = await _db.UserRoles
                    .AsNoTracking()
                    .Join(_db.Roles.Where(r => !r.IsDeleted),
                          userRole => userRole.RoleId,
                          role => role.Id,
                          (userRole, role) => userRole.RoleId)
                    .Distinct()
                    .ToListAsync();

                ViewBag.RolesWithUsers = roleIdsWithUsers;
                return View(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving roles list");
                return View(new List<ApplicationRole>());
            }
        }


        //==============================================================Create Role==================================================================

        [HttpGet]
        [Permission("ROLE_VIEW")]
        public ActionResult CreateRole()
        {
            var model = new RolesViewModel();
            return PartialView("Partials/CreateRole", model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("ROLE_VIEW")]
        public async Task<ActionResult> CreateRole(RolesViewModel model)
        {
            try
            {
                _logger.LogInformation("Attempting to create role: {RoleName}", model.RoleName);

                if (!ModelState.IsValid) return ValidationErrorResponse();

                if (!await IsRoleNameAvailable(model.RoleName))
                {
                    _logger.LogWarning("Role already exists: {RoleName}", model.RoleName);
                    return Json(new { success = false, message = "Role already exists." });
                }

                var role = new ApplicationRole
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = model.RoleName,
                    NormalizedName = _roleManager.NormalizeKey(model.RoleName),
                    Description = model.Description,
                    Remarks = BuildUpdatedRemarks("", "", "Created"),
                    IsActive = model.IsActive
                };

                var result = await _roleManager.CreateAsync(role);

                var newData = new
                {
                    role.Id,
                    role.Name,
                    role.Description,
                    role.IsActive
                };

                if (result.Succeeded)
                {
                    _logger.LogInformation("Role created successfully: {RoleName}", model.RoleName);
                    _cache.Remove($"Role__{model.RoleName.ToUpperInvariant()}");
                    await LogAuditTrailAsync("Create Role", null, newData);
                    return Json(new { success = true, message = "Role created successfully." });
                }

                var errorMessages = result.Errors.Select(e => e.Description);
                _logger.LogError("Failed to create role: {Errors}", string.Join(", ", errorMessages));
                return Json(new { success = false, message = string.Join(", ", errorMessages) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role");
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }


        //==============================================================Edit Role==================================================================

        [HttpGet]
        [Permission("ROLE_VIEW")]
        public async Task<ActionResult> EditRole(string id)
        {
            try
            {
                var role = await _roleManager.FindByIdAsync(id);
                if (role == null || role.IsDeleted)
                {
                    _logger.LogWarning("Role not found or deleted: {RoleId}", id);
                    return NotFound();
                }

                var roleVm = MapToViewModel(role);

                return PartialView("Partials/EditRole", roleVm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving role for editing: {RoleId}", id);
                return NotFound();
            }
        }


        [HttpPost]
        [Permission("ROLE_VIEW")]
        public async Task<ActionResult> EditRole(string id, RolesViewModel model)
        {
            try
            {
                if (!ModelState.IsValid) return ValidationErrorResponse();

                var role = await _roleManager.FindByIdAsync(id) as ApplicationRole;
                if (role == null || role.IsDeleted)
                {
                    _logger.LogWarning("Role not found or deleted: {RoleId}", id);
                    return Json(new { success = false, message = "Role not found." });
                }

                var oldData = new
                {
                    role.Name,
                    role.Description,
                    role.Remarks,
                    role.IsActive
                };

                if (role.Name != model.RoleName)
                {
                    if (!await IsRoleNameAvailable(model.RoleName, id))
                    {
                        _logger.LogWarning("Role name already taken: {RoleName}", model.RoleName);
                        return Json(new { success = false, message = "Role name already taken." });
                    }

                    role.Name = model.RoleName;
                    role.NormalizedName = _roleManager.NormalizeKey(model.RoleName);
                }

                role.Description = model.Description;
                role.Remarks = BuildUpdatedRemarks(role.Remarks, model.Remarks, "Updated");
                role.IsActive = model.IsActive;

                var result = await _roleManager.UpdateAsync(role);

                var newData = new
                {
                    role.Name,
                    role.Description,
                    role.Remarks,
                    role.IsActive
                };

                if (result.Succeeded)
                {
                    _logger.LogInformation("Role updated successfully: {RoleId}", id);
                    _cache.Remove($"Role__{oldData.Name.ToUpperInvariant()}");
                    _cache.Remove($"Role__{role.Name.ToUpperInvariant()}");
                    await LogAuditTrailAsync("Update Role", oldData, newData);
                    return Json(new { success = true, message = "Role updated successfully." });
                }

                var errorMessages = result.Errors.Select(e => e.Description);
                _logger.LogError("Failed to update role: {Errors}", string.Join(", ", errorMessages));
                return Json(new { success = false, message = string.Join(", ", errorMessages) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role: {RoleId}", id);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }


        //==============================================================View Role==================================================================

        [HttpGet]
        [Permission("ROLE_VIEW")]
        public async Task<ActionResult> ViewRole(string id)
        {
            try
            {
                var role = await _roleManager.FindByIdAsync(id);
                if (role == null || role.IsDeleted)
                {
                    _logger.LogWarning("Role not found or deleted: {RoleId}", id);
                    return NotFound();
                }

                var roleVm = MapToViewModel(role);

                return PartialView("Partials/ViewRole", roleVm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing role: {RoleId}", id);
                return NotFound();
            }
        }


        //==============================================================Delete Role==================================================================

        [HttpGet]
        [Permission("ROLE_VIEW")]
        public async Task<ActionResult> DeleteRole(string id)
        {
            try
            {
                var role = await _roleManager.FindByIdAsync(id);
                if (role == null || role.IsDeleted)
                {
                    _logger.LogWarning("Role not found or already deleted: {RoleId}", id);
                    return NotFound();
                }

                // Check for users assigned to this role
                var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
                var hasUsers = usersInRole.Any();

                // If any restrictions exist, show warning
                if (hasUsers)
                {
                    var roleVm = MapToViewModel(role);
                    ViewBag.HasUsers = hasUsers;
                    return PartialView("Partials/DeleteWarningRole", roleVm);
                }

                // If no restrictions, show normal delete confirmation
                var viewModel = MapToViewModel(role);
                return PartialView("Partials/DeleteRole", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving role for deletion: {RoleId}", id);
                return NotFound();
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("ROLE_VIEW")]
        public async Task<ActionResult> DeleteConfirmed(string id)
        {
            try
            {
                var role = await _roleManager.FindByIdAsync(id) as ApplicationRole;
                if (role == null || role.IsDeleted)
                {
                    _logger.LogWarning("Role not found or already deleted: {RoleId}", id);
                    return Json(new { success = false, message = "Role not found or already deleted." });
                }

                // Check for users assigned to this role
                var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
                if (usersInRole.Any())
                {
                    _logger.LogWarning("Cannot delete role with assigned users: {RoleName}", role.Name);
                    return Json(new
                    {
                        success = false,
                        message = "Cannot delete role because it has assigned users. Please reassign users first."
                    });
                }

                var oldData = new
                {
                    role.Name,
                    role.Description,
                    role.Remarks,
                    role.IsActive
                };

                role.Remarks = BuildUpdatedRemarks(role.Remarks, "", "Deleted");
                role.IsDeleted = true;

                var result = await _roleManager.UpdateAsync(role);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Role deleted successfully: {RoleId}", id);
                    _cache.Remove($"Role__{role.Name.ToUpperInvariant()}");
                    await LogAuditTrailAsync("Delete Role", oldData, null);
                    return Json(new { success = true, message = "Role deleted successfully." });
                }

                var errorMessages = result.Errors.Select(e => e.Description);
                _logger.LogError("Failed to delete role: {Errors}", string.Join(", ", errorMessages));
                return Json(new { success = false, message = string.Join(", ", errorMessages) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role: {RoleId}", id);
                return Json(new { success = false, message = "An unexpected error occurred while deleting the role." });
            }
        }

        //==============================================================Check Role Name Availability==================================================================

        [HttpGet]
        public async Task<JsonResult> CheckRoleNameAvailability(string roleName, string? initialRoleName = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(roleName))
                {
                    return Json(new { available = false, message = "Role name cannot be empty." });
                }

                if (!string.IsNullOrEmpty(initialRoleName) &&
                    string.Equals(roleName, initialRoleName, StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { available = true });
                }

                var available = await IsRoleNameAvailable(roleName);
                return Json(new
                {
                    available,
                    message = available ? "Role name is available." : "Role name already exists."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking role name availability: {RoleName}", roleName);
                return Json(new { available = false, message = "Error checking role name availability." });
            }
        }
    }
}