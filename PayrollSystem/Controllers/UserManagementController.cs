using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    public class UserManagementController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<UserManagementController> _logger;
        private readonly IPasswordValidator<ApplicationUser> _passwordValidator;
        private readonly IConfiguration _configuration;

        public UserManagementController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<ApplicationRole> roleManager,
            AuditHelper auditHelper,
            ILogger<UserManagementController> logger,
            IPasswordValidator<ApplicationUser> passwordValidator,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _db = db;
            _auditHelper = auditHelper;
            _logger = logger;
            _passwordValidator = passwordValidator;
            _configuration = configuration;
        }

        //--------------------------------------Private Methods----------------------------------------------

        private async Task<ApplicationUser> GetUserByIdAsync(string id, bool asNoTracking = false)
        {
            try
            {
                var query = _userManager.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.Designation)
                .Where(u => u.Id == id && !u.IsDeleted);

                if (asNoTracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user with ID {UserId}", id);
                return null;
            }
        }

        private async Task<SelectList> GetRoleSelectListAsync(string? selectedRoleId = null)
        {
            var roles = await _roleManager.Roles
                .AsNoTracking()
                .Where(r => r.IsActive && !r.IsDeleted)
                .ToListAsync();

            return new SelectList(roles, "Id", "Name", selectedRoleId);
        }

        private async Task<SelectList> GetActiveDepartmentsAsync()
        {
            var departments = await _db.Departments
                .AsNoTracking()
                .Where(d => d.IsActive && !d.IsDeleted)
                .ToListAsync();

            return new SelectList(departments, "Id", "DepartmentName");
        }

        private async Task<SelectList> GetActiveDesignationsAsync()
        {
            var designations = await _db.Designations
                .AsNoTracking()
                .Where(d => d.IsActive && !d.IsDeleted)
                .ToListAsync();

            return new SelectList(designations, "Id", "DesignationName");
        }

        private async Task LogAuditTrailAsync(string action, object oldData, object newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "User Management", "User", oldData, newData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit trail for {Action}. OldData: {OldData}, NewData: {NewData}", action, oldData, newData);
            }
        }

        private async Task<string> SaveProfilePictureAsync(IFormFile profilePicture, string lastName, string firstName)
        {
            var fileExtension = Path.GetExtension(profilePicture.FileName);
            var fileName = $"{lastName}_{firstName}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";

            var path = _configuration["FileSettings:UploadFolder"];
            if (string.IsNullOrEmpty(path))
            {
                throw new Exception("Upload folder not configured.");
            }

            var filePath = Path.Combine(path, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await profilePicture.CopyToAsync(stream);
            }

            return fileName;
        }

        private JsonResult ValidationErrorResponse()
        {
            var errorMessages = ModelStateHelper.GetModelErrors(ModelState);
            _logger.LogWarning("Validation failed: {Errors}", string.Join("; ", errorMessages));
            return Json(new { success = false, message = string.Join("; ", errorMessages) });
        }

        //--------------------------------------List of Users Account----------------------------------------------

        [Permission("USER_VIEW")]
        public async Task<ActionResult> UserAccount()
        {
            var users = await _db.Users
                .AsNoTracking()
                .Include(x => x.Role)
                .Include(x => x.Department)
                .Include(x => x.Designation)
                .Where(x => !x.IsDeleted)
                .OrderByDescending(u => u.CreatedOn)
                .ToListAsync();

            var userIds = users.Select(u => u.Id).ToList();

            var usersWithReferences = await GetUsersWithReferencesAsync(userIds);

            ViewBag.UsersWithRestrictions = usersWithReferences.Keys.ToList();
            return View(users);
        }

        //--------------------------------------Creating User Account----------------------------------------------

        [HttpGet]
        [Permission("USER_VIEW")]
        public async Task<ActionResult> CreateUser()
        {
            ViewData["RoleId"] = await GetRoleSelectListAsync();
            ViewData["DepartmentId"] = await GetActiveDepartmentsAsync();
            ViewData["DesignationId"] = await GetActiveDesignationsAsync();

            return PartialView("Partials/CreateUser");
        }

        [HttpPost]
        [Permission("USER_VIEW")]
        public async Task<ActionResult> CreateUser(UserViewModel model, IFormFile? profilePicture)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            try
            {
                var user = new ApplicationUser
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    MiddleName = model.MiddleName,
                    LastName = model.LastName,
                    FullName = $"{model.FirstName} {model.MiddleName} {model.LastName}".Replace("  ", " ").Trim(),
                    CustomizeName = model.CustomizeName,
                    PhoneNumber = model.PhoneNumber,
                    RoleId = model.RoleId,
                    DepartmentId = model.DepartmentId,
                    DesignationId = model.DesignationId,
                    IsActive = model.IsActive,
                    CustomizeDesignation = model.CustomizeDesignation,
                    CreatedById = _userManager.GetUserId(User),
                    CreatedOn = DateTime.UtcNow
                };

                if (profilePicture != null && profilePicture.Length > 0)
                {
                    user.ProfilePicture = await SaveProfilePictureAsync(profilePicture, user.LastName, user.FirstName);
                }

                var result = await _userManager.CreateAsync(user, model.Password);
                if (!result.Succeeded)
                {
                    return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
                }

                var role = await _roleManager.FindByIdAsync(model.RoleId);
                if (role != null)
                {
                    var roleResult = await _userManager.AddToRoleAsync(user, role.Name);
                    if (!roleResult.Succeeded)
                    {
                        return Json(new { success = false, message = "User created but failed to assign role." });
                    }
                }
                else
                {
                    return Json(new { success = false, message = "Invalid RoleId. Role not found." });
                }

                if (role != null)
                {
                    model.RoleName = role.Name;
                }

                if (user.DepartmentId.HasValue)
                {
                    var department = await _db.Departments.FindAsync(user.DepartmentId);
                    model.DepartmentName = department?.DepartmentName;
                }

                if (user.DesignationId.HasValue)
                {
                    var designation = await _db.Designations.FindAsync(user.DesignationId);
                    model.DesignationName = designation?.DesignationName;
                }

                await LogAuditTrailAsync("Create User", null, model);

                return Json(new { success = true, message = "User created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating user.");
                return Json(new { success = false, message = "An unexpected error occurred. Please try again later." });
            }
        }

        //--------------------------------------Updating/Editing User Account----------------------------------------------

        [HttpGet]
        [Permission("USER_VIEW")]
        public async Task<ActionResult> EditUser(string id)
        {
            var user = await GetUserByIdAsync(id);
            if (user == null) return NotFound();

            var userViewModel = new UserViewModel
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName,
                FullName = $"{user.FirstName} {user.MiddleName} {user.LastName}".Replace("  ", " ").Trim(),
                CustomizeName = user.CustomizeName,
                PhoneNumber = user.PhoneNumber,
                RoleId = user.RoleId,
                DesignationId = user.DesignationId,
                CustomizeDesignation = user.CustomizeDesignation,
                DepartmentId = user.DepartmentId,
                ProfilePicture = user.ProfilePicture,
                IsActive = user.IsActive,
            };

            ViewData["RoleId"] = await GetRoleSelectListAsync(user.RoleId);
            ViewData["DepartmentId"] = await GetActiveDepartmentsAsync();
            ViewData["DesignationId"] = await GetActiveDesignationsAsync();

            return PartialView("Partials/EditUser", userViewModel);
        }

        [HttpPost]
        [Permission("USER_VIEW")]
        public async Task<ActionResult> EditUser(string id, UserViewModel model, IFormFile? profilePicture)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var user = await GetUserByIdAsync(id);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            // Store the original user data for audit trail
            var oldUserData = new ApplicationUser
            {
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Email = user.Email,
                UserName = user.UserName,
                CustomizeName = user.CustomizeName,
                RoleId = user.RoleId,
                DepartmentId = user.DepartmentId,
                DesignationId = user.DesignationId,
                CustomizeDesignation = user.CustomizeDesignation,
                ProfilePicture = user.ProfilePicture,
                IsActive = user.IsActive,
            };

            // Store the original profile picture path
            var originalProfilePicture = user.ProfilePicture;

            // Update user properties
            user.FirstName = model.FirstName;
            user.MiddleName = model.MiddleName;
            user.LastName = model.LastName;
            user.FullName = $"{model.FirstName} {model.MiddleName} {model.LastName}".Replace("  ", " ").Trim();
            user.PhoneNumber = model.PhoneNumber;
            user.Email = model.Email;
            user.UserName = model.UserName;
            user.CustomizeName = model.CustomizeName;
            user.RoleId = model.RoleId;
            user.DepartmentId = model.DepartmentId;
            user.DesignationId = model.DesignationId;
            user.CustomizeDesignation = model.CustomizeDesignation;
            user.IsActive = model.IsActive;
            user.UpdatedById = _userManager.GetUserId(User);
            user.UpdatedOn = DateTime.UtcNow;

            // Handle profile picture update
            if (profilePicture != null && profilePicture.Length > 0)
            {
                user.ProfilePicture = await SaveProfilePictureAsync(profilePicture, user.LastName, user.FirstName);
            }
            // If no new profile picture, the original is retained

            // Handle password update
            if (!string.IsNullOrEmpty(model.Password))
            {
                var validationResult = await _passwordValidator.ValidateAsync(_userManager, user, model.Password);
                if (!validationResult.Succeeded)
                {
                    return Json(new { success = false, message = string.Join(", ", validationResult.Errors.Select(e => e.Description)) });
                }

                var newPasswordHash = _userManager.PasswordHasher.HashPassword(user, model.Password);

                // Update only if the new hash is different
                if (newPasswordHash != user.PasswordHash)
                {
                    user.PasswordHash = newPasswordHash;
                    user.PasswordChangedOn = DateTime.UtcNow;
                }
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }

            // Handle role updates
            var existingRoles = await _userManager.GetRolesAsync(user);

            // Remove user from old roles (if any)
            if (existingRoles.Any())
            {
                var removeRoleResult = await _userManager.RemoveFromRolesAsync(user, existingRoles);
                if (!removeRoleResult.Succeeded)
                {
                    return Json(new { success = false, message = "Failed to remove old roles." });
                }
            }

            // Assign new role if RoleId is provided
            var role = await _roleManager.FindByIdAsync(model.RoleId);
            if (role == null)
            {
                return Json(new { success = false, message = "Invalid RoleId. Role not found." });
            }

            var addRoleResult = await _userManager.AddToRoleAsync(user, role.Name);
            if (!addRoleResult.Succeeded)
            {
                return Json(new { success = false, message = "User updated but failed to assign new role." });
            }

            await LogAuditTrailAsync("Update User", oldUserData, model);
            return Json(new { success = true, message = "User updated successfully." });
        }

        //--------------------------------------View User Account Details----------------------------------------------

        [HttpGet]
        [Permission("USER_VIEW")]
        public async Task<ActionResult> ViewUser(string id)
        {
            var user = await GetUserByIdAsync(id, asNoTracking: true);
            if (user == null) return NotFound();

            var userViewModel = new UserViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName,
                FullName = user.FullName,
                Email = user.Email,
                UserName = user.UserName,
                PhoneNumber = user.PhoneNumber,
                RoleId = user.RoleId,
                DepartmentId = user.DepartmentId,
                DesignationId = user.DesignationId,
                CustomizeName = user.CustomizeName,
                CustomizeDesignation = user.CustomizeDesignation,
                ProfilePicture = user.ProfilePicture,
                IsActive = user.IsActive,
            };

            // Use already loaded navigation properties
            userViewModel.RoleName = user.Role?.Name;
            userViewModel.DepartmentName = user.Department?.DepartmentName;
            userViewModel.DesignationName = user.Designation?.DesignationName;

            return PartialView("Partials/ViewUser", userViewModel);
        }

        //--------------------------------------Deleting User Account----------------------------------------------

        private async Task<(bool hasReferences, Dictionary<string, bool> references)> CheckUserReferencesAsync(string userId)
        {
            var references = new Dictionary<string, bool>
            {
                { "Departments", await _db.Departments.AnyAsync(d => d.CreatedById == userId || d.UpdatedById == userId) },
                { "Designations", await _db.Designations.AnyAsync(d => d.CreatedById == userId || d.UpdatedById == userId) },
                // Add other potential references here if needed for single-user deletion checks
            };

            return (references.Values.Any(x => x), references);
        }

        private async Task<Dictionary<string, Dictionary<string, bool>>> GetUsersWithReferencesAsync(List<string> userIds)
        {
            var usersWithReferences = new Dictionary<string, Dictionary<string, bool>>();

            if (!userIds.Any()) return usersWithReferences;

            // Check for references in Departments
            var departmentReferences = await _db.Departments
                .AsNoTracking()
                .Where(d => userIds.Contains(d.CreatedById!) || userIds.Contains(d.UpdatedById!))
                .Select(d => new { UserId = d.CreatedById, d.UpdatedById })
                .ToListAsync();

            // Check for references in Designations
            var designationReferences = await _db.Designations
                .AsNoTracking()
                .Where(d => userIds.Contains(d.CreatedById!) || userIds.Contains(d.UpdatedById!))
                .Select(d => new { UserId = d.CreatedById, d.UpdatedById })
                .ToListAsync();

            foreach (var userId in userIds)
            {
                var references = new Dictionary<string, bool>
                {
                    { "Departments", departmentReferences.Any(r => r.UserId == userId || r.UpdatedById == userId) },
                    { "Designations", designationReferences.Any(r => r.UserId == userId || r.UpdatedById == userId) }
                };
                usersWithReferences[userId] = references;
            }

            return usersWithReferences;
        }

        [HttpGet]
        [Permission("USER_VIEW")]
        public async Task<ActionResult> DeleteUser(string id)
        {
            var user = await GetUserByIdAsync(id, asNoTracking: true);
            if (user == null) return NotFound();

            // Check if user is an admin
            var isAdmin = user.Role?.Name == "Admin";
            var adminCount = await _userManager.Users.AsNoTracking().CountAsync(u => u.Role.Name == "Admin" && !u.IsDeleted);

            // Check if user is referenced in any other tables (using the single-user method for this specific action)
            var (hasReferences, references) = await CheckUserReferencesAsync(user.Id);

            // If user is admin and is the last admin, or has references, show warning
            if ((isAdmin && adminCount <= 1) || hasReferences)
            {
                var viewModel = new UserViewModel
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    MiddleName = user.MiddleName,
                    LastName = user.LastName,
                    FullName = user.FullName,
                    CustomizeName = user.CustomizeName,
                    Email = user.Email,
                    UserName = user.UserName,
                    PhoneNumber = user.PhoneNumber,
                    RoleId = user.RoleId,
                    DepartmentId = user.DepartmentId,
                    DesignationId = user.DesignationId,
                    CustomizeDesignation = user.CustomizeDesignation,
                    ProfilePicture = user.ProfilePicture,
                    IsActive = user.IsActive,
                    RoleName = user.Role?.Name,
                    DepartmentName = user.Department?.DepartmentName,
                    DesignationName = user.Designation?.DesignationName,
                    CanDelete = false
                };

                // Add specific restriction information
                ViewBag.IsLastAdmin = isAdmin && adminCount <= 1;
                ViewBag.HasReferences = hasReferences;
                ViewBag.References = references;

                return PartialView("Partials/DeleteWarningUser", viewModel);
            }

            // If user can be deleted, show delete confirmation
            return PartialView("Partials/DeleteUser", new UserViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName,
                FullName = user.FullName,
                CustomizeName = user.CustomizeName,
                Email = user.Email,
                UserName = user.UserName,
                PhoneNumber = user.PhoneNumber,
                RoleId = user.RoleId,
                DepartmentId = user.DepartmentId,
                DesignationId = user.DesignationId,
                CustomizeDesignation = user.CustomizeDesignation,
                ProfilePicture = user.ProfilePicture,
                IsActive = user.IsActive,
                RoleName = user.Role?.Name,
                DepartmentName = user.Department?.DepartmentName,
                DesignationName = user.Designation?.DesignationName,
                CanDelete = true
            });
        }

        [HttpPost]
        [Permission("USER_VIEW")]
        public async Task<ActionResult> DeleteConfirmed(string id)
        {
            var user = await GetUserByIdAsync(id);
            if (user == null || user.IsDeleted)
            {
                return Json(new { success = false, message = "User not found or already deleted." });
            }

            // Check if user is an admin
            var isAdmin = user.Role?.Name == "Admin";
            var adminCount = await _userManager.Users.AsNoTracking().CountAsync(u => u.Role.Name == "Admin" && !u.IsDeleted);

            // If user is admin and is the last admin, prevent deletion
            if (isAdmin && adminCount <= 1)
            {
                return Json(new { success = false, message = "Cannot delete the last admin user. At least one admin must remain in the system." });
            }

            // Check if user is referenced in any other tables
            var (hasReferences, references) = await CheckUserReferencesAsync(user.Id);
            if (hasReferences)
            {
                return Json(new { success = false, message = "Cannot delete user because it is referenced in other records." });
            }

            // Create a view model for audit logging
            var oldUserData = new UserViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName,
                FullName = user.FullName,
                CustomizeName = user.CustomizeName,
                Email = user.Email,
                UserName = user.UserName,
                PhoneNumber = user.PhoneNumber,
                RoleId = user.RoleId,
                DepartmentId = user.DepartmentId,
                DesignationId = user.DesignationId,
                CustomizeDesignation = user.CustomizeDesignation,
                ProfilePicture = user.ProfilePicture,
                IsActive = user.IsActive,
                RoleName = user.Role?.Name,
                DepartmentName = user.Department?.DepartmentName,
                DesignationName = user.Designation?.DesignationName
            };

            _logger.LogInformation("Deleting user: {@OldUserData}", oldUserData);

            // Mark user as deleted
            user.IsDeleted = true;
            user.UpdatedById = _userManager.GetUserId(User);
            user.UpdatedOn = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await LogAuditTrailAsync("Delete User", oldUserData, null);
                return Json(new { success = true, message = "User deleted successfully." });
            }

            return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }
    }
}