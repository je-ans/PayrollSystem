using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Models.BuildUp;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<EmployeeController> _logger;

        public EmployeeController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            AuditHelper auditHelper,
            ILogger<EmployeeController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
            _auditHelper = auditHelper;
            _logger = logger;
        }

        //=======================================================Private Method=================================================================

        private async Task LogAuditTrailAsync(string action, object oldData, object newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "Employee", "Employees", oldData, newData);
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

        private static string GetEnumDisplayName(Enum value)
        {
            var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
            var display = member?.GetCustomAttribute<DisplayAttribute>();
            return display?.Name ?? value.ToString();
        }

        private async Task PopulateDropdownsAsync(EmployeeViewModel viewModel)
        {
            viewModel.DesignationOptions = await _db.Designations
                .Where(d => !d.IsDeleted && d.IsActive)
                .AsNoTracking()
                .ToListAsync();

            viewModel.OrganizationUnits = await _db.OrganizationStructures
                .AsNoTracking()
                .Where(o => o.IsActive)
                .OrderBy(o => o.Level)
                .ThenBy(o => o.Name)
                .Select(o => new ParentOptionViewModel
                {
                    Id = o.Id,
                    Level = o.Level,
                    DisplayText = $"{GetEnumDisplayName(o.Level)} — {o.Name}"
                })
                .ToListAsync();

            viewModel.RateTypes = await _db.RateTypes
                .Where(r => !r.IsDeleted && r.IsActive)
                .AsNoTracking()
                .ToListAsync();
        }

        private async Task LoadOrganizationPlacementsAsync(EmployeeViewModel viewModel, Guid employeeId)
        {
            var placements = new List<string>();

            var activeRoles = await _db.EmployeeRoleAssignments
                .AsNoTracking()
                .Where(a => a.EmployeeId == employeeId && a.EffectiveTo == null)
                .Include(a => a.OrganizationStructure)
                .OrderBy(a => a.OrganizationStructure!.Name)
                .ThenBy(a => a.LeadershipRole)
                .ThenBy(a => a.FunctionalRole)
                .ToListAsync();

            var roleUnitIds = new HashSet<int>();
            foreach (var role in activeRoles)
            {
                if (role.OrganizationStructure == null)
                    continue;
                roleUnitIds.Add(role.OrganizationStructureId);
                var roleLabel = role.LeadershipRole.HasValue
                    ? GetEnumDisplayName(role.LeadershipRole.Value)
                    : role.FunctionalRole.HasValue
                        ? GetEnumDisplayName(role.FunctionalRole.Value)
                        : "Role";
                if (role.LeadershipRole.HasValue && role.FunctionalRole.HasValue)
                    roleLabel = $"{GetEnumDisplayName(role.LeadershipRole.Value)} / {GetEnumDisplayName(role.FunctionalRole.Value)}";
                placements.Add(
                    $"{GetEnumDisplayName(role.OrganizationStructure.Level)} — {role.OrganizationStructure.Name}: {roleLabel}");
            }

            var staffOnly = await _db.EmployeeOrganizationAssignments
                .AsNoTracking()
                .Where(a => a.EmployeeId == employeeId &&
                    a.EffectiveTo == null &&
                    !roleUnitIds.Contains(a.OrganizationStructureId))
                .Include(a => a.OrganizationStructure)
                .OrderBy(a => a.OrganizationStructure!.Name)
                .ToListAsync();

            foreach (var assignment in staffOnly)
            {
                if (assignment.OrganizationStructure == null)
                    continue;
                placements.Add(
                    $"{GetEnumDisplayName(assignment.OrganizationStructure.Level)} — {assignment.OrganizationStructure.Name}{(assignment.IsPrimary ? " (Primary schedule unit)" : " (Secondary)")}");
            }

            viewModel.OrganizationPlacements = placements;
        }

        private async Task<List<int>> GetActiveOrganizationStructureIdsAsync(Guid employeeId)
        {
            return await _db.EmployeeOrganizationAssignments
                .AsNoTracking()
                .Where(a => a.EmployeeId == employeeId && a.EffectiveTo == null)
                .Select(a => a.OrganizationStructureId)
                .ToListAsync();
        }

        private async Task SyncEmployeeDesignationAsync(
            Guid employeeId,
            int designationId,
            DateOnly? dateHired)
        {
            var effectiveFrom = dateHired.HasValue
                ? dateHired.Value.ToDateTime(TimeOnly.MinValue)
                : DateTime.Today;

            var current = await _db.EmployeeDesignations
                .Where(d => d.EmployeeId == employeeId && d.EffectiveTo == null)
                .FirstOrDefaultAsync();

            if (current != null && current.DesignationId == designationId)
                return;

            if (current != null)
            {
                current.EffectiveTo = DateTime.Today;
                _db.EmployeeDesignations.Update(current);
            }

            _db.EmployeeDesignations.Add(new EmployeeDesignation
            {
                EmployeeId = employeeId,
                DesignationId = designationId,
                EffectiveFrom = effectiveFrom
            });

            await _db.SaveChangesAsync();
        }

        private async Task SyncEmployeeOrganizationAssignmentsAsync(
            Guid employeeId,
            List<int> organizationStructureIds,
            int? primaryOrganizationStructureId,
            DateOnly? dateHired)
        {
            var effectiveFrom = dateHired.HasValue
                ? dateHired.Value.ToDateTime(TimeOnly.MinValue)
                : DateTime.Today;

            var selectedIds = organizationStructureIds.Distinct().ToList();
            var primaryId = primaryOrganizationStructureId is > 0 &&
                            selectedIds.Contains(primaryOrganizationStructureId.Value)
                ? primaryOrganizationStructureId.Value
                : selectedIds.FirstOrDefault();

            var existing = await _db.EmployeeOrganizationAssignments
                .Where(a => a.EmployeeId == employeeId)
                .ToListAsync();

            foreach (var unitId in selectedIds)
            {
                var row = existing.FirstOrDefault(a => a.OrganizationStructureId == unitId);
                if (row != null)
                {
                    row.EffectiveFrom = effectiveFrom;
                    row.EffectiveTo = null;
                    row.IsPrimary = unitId == primaryId;
                    _db.EmployeeOrganizationAssignments.Update(row);
                }
                else
                {
                    _db.EmployeeOrganizationAssignments.Add(new EmployeeOrganizationAssignment
                    {
                        EmployeeId = employeeId,
                        OrganizationStructureId = unitId,
                        EffectiveFrom = effectiveFrom,
                        IsPrimary = unitId == primaryId
                    });
                }
            }

            foreach (var row in existing.Where(a =>
                         !selectedIds.Contains(a.OrganizationStructureId) &&
                         EmployeeOrganizationHelper.IsAssignmentActive(a.EffectiveTo)))
            {
                row.EffectiveTo = DateTime.Today;
                if (row.IsPrimary)
                    row.IsPrimary = false;
                _db.EmployeeOrganizationAssignments.Update(row);
            }

            await _db.SaveChangesAsync();
            await EmployeeOrganizationHelper.EnsurePrimaryAssignmentAsync(_db, employeeId);
            if (primaryId > 0)
                await EmployeeOrganizationHelper.SetPrimaryOrganizationStructureAsync(_db, employeeId, primaryId);
            await _db.SaveChangesAsync();
        }

        private void ValidateOrganizationUnits(EmployeeViewModel model)
        {
            if (model.OrganizationStructureIds == null || !model.OrganizationStructureIds.Any())
            {
                ModelState.AddModelError(
                    nameof(model.OrganizationStructureIds),
                    "Select at least one organization unit.");
                return;
            }

            if (model.PrimaryOrganizationStructureId is > 0 &&
                !model.OrganizationStructureIds.Contains(model.PrimaryOrganizationStructureId.Value))
            {
                ModelState.AddModelError(
                    nameof(model.PrimaryOrganizationStructureId),
                    "Primary schedule unit must be one of the selected organization units.");
            }
        }

        //=======================================================Get Employee List=============================================================\

        [Permission("EMPLOYEE_VIEW")]
        public async Task<IActionResult> Index(string? employmentBasis = null)
        {
            ViewBag.FilterEmploymentBasis = employmentBasis;

            var employeesQuery = _db.Employees
                .Where(e => !e.IsDeleted && e.IsActive);

            if (!string.IsNullOrWhiteSpace(employmentBasis)
                && Enum.TryParse<EmploymentBasis>(employmentBasis, ignoreCase: true, out var basis))
            {
                employeesQuery = employeesQuery.Where(e => e.EmploymentBasis == basis);
                ViewBag.FilterEmploymentBasis = basis.ToString();
            }

            ViewBag.Employees = await employeesQuery
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

            var orgUnits = await _db.OrganizationStructures
                .Where(o => o.IsActive)
                .OrderBy(o => o.Level).ThenBy(o => o.Name)
                .AsNoTracking()
                .ToListAsync();
            ViewBag.OrganizationUnits = orgUnits
                .Select(o => new { o.Id, DisplayText = $"{GetEnumDisplayName(o.Level)} — {o.Name}" })
                .ToList();

            return View();
        }

        //=======================================================Create Employee==============================================================\

        [Permission("EMPLOYEE_VIEW")]
        public async Task<ActionResult> CreateEmployee()
        {
            var viewModel = new EmployeeViewModel();
            await PopulateDropdownsAsync(viewModel);
            var roles = await _roleManager.Roles
                .Where(r => r.IsActive && !r.IsDeleted)
                .AsNoTracking()
                .ToListAsync();
            ViewBag.RoleId = new SelectList(roles, "Id", "Name");
            return PartialView("Partials/_CreateEmployee", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("EMPLOYEE_VIEW")]
        public async Task<ActionResult> CreateEmployee(EmployeeViewModel model)
        {
            // Require login account fields when creating a new employee
            if (string.IsNullOrWhiteSpace(model.UserName))
                ModelState.AddModelError(nameof(model.UserName), "Username is required so the employee can sign in.");
            if (string.IsNullOrWhiteSpace(model.EmailAddress))
                ModelState.AddModelError(nameof(model.EmailAddress), "Email is required for the login account.");
            if (string.IsNullOrWhiteSpace(model.Password))
                ModelState.AddModelError(nameof(model.Password), "Password is required.");
            if (string.IsNullOrWhiteSpace(model.RoleId))
                ModelState.AddModelError(nameof(model.RoleId), "System role is required.");

            ValidateOrganizationUnits(model);
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            ApplicationUser? newUser = null;
            try
            {
                if (await _db.Employees.AnyAsync(e =>
                    e.EmployeeIdNumber == model.EmployeeIdNumber && !e.IsDeleted))
                {
                    return Json(new { success = false, message = "Employee ID number already exists." });
                }
                if (await _userManager.FindByNameAsync(model.UserName!) != null)
                {
                    return Json(new { success = false, message = "Username is already in use." });
                }
                if (await _userManager.FindByEmailAsync(model.EmailAddress!) != null)
                {
                    return Json(new { success = false, message = "Email is already in use for another account." });
                }

                var fullName = $"{model.FirstName} {model.MiddleName} {model.Surname}".Replace("  ", " ").Trim();
                newUser = new ApplicationUser
                {
                    UserName = model.UserName,
                    Email = model.EmailAddress,
                    FirstName = model.FirstName,
                    MiddleName = model.MiddleName,
                    LastName = model.Surname,
                    FullName = fullName,
                    RoleId = model.RoleId!,
                    DesignationId = model.DesignationId,
                    PhoneNumber = model.MobileNo ?? model.TelephoneNo ?? "",
                    IsActive = true,
                    CreatedById = _userManager.GetUserId(User),
                    CreatedOn = DateTime.UtcNow
                };

                var createResult = await _userManager.CreateAsync(newUser, model.Password!);
                if (!createResult.Succeeded)
                {
                    return Json(new { success = false, message = string.Join(" ", createResult.Errors.Select(e => e.Description)) });
                }

                var role = await _roleManager.FindByIdAsync(model.RoleId!);
                if (role != null)
                {
                    var roleResult = await _userManager.AddToRoleAsync(newUser, role.Name);
                    if (!roleResult.Succeeded)
                    {
                        await _userManager.DeleteAsync(newUser);
                        return Json(new { success = false, message = "Failed to assign role. Account not created." });
                    }
                }

                var employee = new Employee
                {
                    EmployeeIdNumber = model.EmployeeIdNumber,
                    Surname = model.Surname,
                    FirstName = model.FirstName,
                    MiddleName = model.MiddleName,
                    NameExtension = model.NameExtension,
                    DateOfBirth = model.DateOfBirth,
                    Sex = model.Sex,
                    TelephoneNo = model.TelephoneNo,
                    MobileNo = model.MobileNo,
                    EmailAddress = model.EmailAddress,
                    EmploymentBasis = model.EmploymentBasis,
                    DateHired = model.DateHired,
                    DefaultRateTypeId = model.DefaultRateTypeId,
                    BiometricId = model.BiometricId,
                    IsActive = model.IsActive,
                    ApplicationUserId = newUser.Id,
                    CreatedById = _userManager.GetUserId(User),
                    CreatedOn = DateTime.UtcNow
                };

                _db.Employees.Add(employee);
                var result = await _db.SaveChangesAsync();

                if (result > 0)
                {
                    await SyncEmployeeOrganizationAssignmentsAsync(
                        employee.Id, model.OrganizationStructureIds, model.PrimaryOrganizationStructureId, model.DateHired);
                    await SyncEmployeeDesignationAsync(
                        employee.Id, model.DesignationId, model.DateHired);
                }

                if (result <= 0)
                {
                    await _userManager.DeleteAsync(newUser);
                    _logger.LogError("Failed to save employee: {EmployeeIdNumber}", model.EmployeeIdNumber);
                    return Json(new { success = false, message = "Failed to create employee." });
                }

                await LogAuditTrailAsync("Create Employee Account", null, new { UserId = newUser.Id, EmployeeId = employee.Id });
                return Json(new { success = true, message = "Employee and login account created successfully. The employee can now sign in with the username and password you set." });
            }
            catch (Exception ex)
            {
                if (newUser != null)
                {
                    try { await _userManager.DeleteAsync(newUser); } catch { /* best effort */ }
                }
                _logger.LogError(ex, "Error creating employee: {EmployeeIdNumber}", model.EmployeeIdNumber);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }


        //=======================================================Edit Employee===================================================================\


        [HttpGet]
        [Permission("EMPLOYEE_VIEW")]
        public async Task<ActionResult> EditEmployee(Guid id)
        {
            var employee = await _db.Employees
                .Include(e => e.ApplicationUser)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();

            var viewModel = new EmployeeViewModel
            {
                Id = employee.Id,
                EmployeeIdNumber = employee.EmployeeIdNumber,
                Surname = employee.Surname,
                FirstName = employee.FirstName,
                MiddleName = employee.MiddleName,
                NameExtension = employee.NameExtension,
                DateOfBirth = employee.DateOfBirth,
                Sex = employee.Sex,
                TelephoneNo = employee.TelephoneNo,
                MobileNo = employee.MobileNo,
                EmailAddress = employee.EmailAddress,
                EmploymentBasis = employee.EmploymentBasis,
                DefaultRateTypeId = employee.DefaultRateTypeId,
                DateHired = employee.DateHired,
                DesignationId = await EmployeeOrganizationHelper.GetActiveDesignationIdAsync(_db, employee.Id) ?? 0,
                BiometricId = employee.BiometricId,
                IsActive = employee.IsActive,
                UserName = employee.ApplicationUser?.UserName,
                LoginEmail = employee.ApplicationUser?.Email,
                RoleId = employee.ApplicationUser?.RoleId,
                OrganizationStructureIds = await GetActiveOrganizationStructureIdsAsync(employee.Id),
                PrimaryOrganizationStructureId = await EmployeeOrganizationHelper.GetPrimaryOrganizationStructureIdAsync(_db, employee.Id)
            };
            await PopulateDropdownsAsync(viewModel);
            var roles = await _roleManager.Roles
                .Where(r => r.IsActive && !r.IsDeleted)
                .AsNoTracking()
                .ToListAsync();
            ViewBag.RoleId = new SelectList(roles, "Id", "Name", viewModel.RoleId);
            ViewBag.HasLinkedAccount = employee.ApplicationUserId != null;
            return PartialView("Partials/_EditEmployee", viewModel);
        }

        [HttpPost]
        [Permission("EMPLOYEE_VIEW")]
        public async Task<ActionResult> EditEmployee(Guid id, EmployeeViewModel model)
        {
            // Password optional on edit; if provided, validate confirmation
            if (!string.IsNullOrEmpty(model.Password) && model.Password != model.ConfirmPassword)
                ModelState.AddModelError(nameof(model.ConfirmPassword), "New password and confirmation do not match.");
            if (!string.IsNullOrEmpty(model.Password) && model.Password.Length < 8)
                ModelState.AddModelError(nameof(model.Password), "Password must be at least 8 characters.");

            ValidateOrganizationUnits(model);
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            var employee = await _db.Employees
                .Include(e => e.ApplicationUser)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null)
            {
                _logger.LogError("Employee with ID {Id} not found.", id);
                return Json(new { success = false, message = "Employee not found." });
            }

            if (employee.EmployeeIdNumber != model.EmployeeIdNumber &&
                await _db.Employees.AnyAsync(e =>
                    e.EmployeeIdNumber == model.EmployeeIdNumber && e.Id != id && !e.IsDeleted))
            {
                return Json(new { success = false, message = "Another employee with the same ID number already exists." });
            }

            // Create a clean snapshot for audit logging (no navigation properties)
            var oldEmployeeData = new
            {
                employee.Id,
                employee.EmployeeIdNumber,
                employee.Surname,
                employee.FirstName,
                employee.MiddleName,
                employee.NameExtension,
                employee.DateOfBirth,
                employee.Sex,
                employee.TelephoneNo,
                employee.MobileNo,
                employee.EmailAddress,
                employee.EmploymentBasis,
                employee.DefaultRateTypeId,
                employee.DateHired,
                DesignationId = await EmployeeOrganizationHelper.GetActiveDesignationIdAsync(_db, employee.Id),
                employee.BiometricId,
                employee.IsActive,
                employee.ApplicationUserId
            };

            // Update linked login account if present
            if (employee.ApplicationUser != null)
            {
                var user = employee.ApplicationUser;
                if (user.Email != model.EmailAddress)
                {
                    var existingByEmail = await _userManager.FindByEmailAsync(model.EmailAddress ?? "");
                    if (existingByEmail != null && existingByEmail.Id != user.Id)
                    {
                        return Json(new { success = false, message = "Email is already in use by another account." });
                    }
                    user.Email = model.EmailAddress ?? user.Email;
                }
                if (!string.IsNullOrWhiteSpace(model.RoleId) && user.RoleId != model.RoleId)
                {
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    var newRole = await _roleManager.FindByIdAsync(model.RoleId);
                    if (newRole != null)
                        await _userManager.AddToRoleAsync(user, newRole.Name);
                    user.RoleId = model.RoleId;
                }
                user.FirstName = model.FirstName;
                user.MiddleName = model.MiddleName;
                user.LastName = model.Surname;
                user.FullName = $"{model.FirstName} {model.MiddleName} {model.Surname}".Replace("  ", " ").Trim();
                user.DesignationId = model.DesignationId;
                user.PhoneNumber = model.MobileNo ?? model.TelephoneNo ?? "";
                user.UpdatedById = _userManager.GetUserId(User);
                user.UpdatedOn = DateTime.UtcNow;

                user.UserName = model.UserName;
                var updateUserResult = await _userManager.UpdateAsync(user);
                if (!updateUserResult.Succeeded)
                {
                    return Json(new { success = false, message = string.Join(" ", updateUserResult.Errors.Select(e => e.Description)) });
                }

                if (!string.IsNullOrEmpty(model.Password))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var resetResult = await _userManager.ResetPasswordAsync(user, token, model.Password);
                    if (!resetResult.Succeeded)
                    {
                        return Json(new { success = false, message = "Failed to set new password: " + string.Join(" ", resetResult.Errors.Select(e => e.Description)) });
                    }
                }
            }

            // Update employee
            employee.EmployeeIdNumber = model.EmployeeIdNumber;
            employee.Surname = model.Surname;
            employee.FirstName = model.FirstName;
            employee.MiddleName = model.MiddleName;
            employee.NameExtension = model.NameExtension;
            employee.DateOfBirth = model.DateOfBirth;
            employee.Sex = model.Sex;
            employee.TelephoneNo = model.TelephoneNo;
            employee.MobileNo = model.MobileNo;
            employee.EmailAddress = model.EmailAddress;
            employee.EmploymentBasis = model.EmploymentBasis;
            employee.DefaultRateTypeId = model.DefaultRateTypeId;
            employee.DateHired = model.DateHired;
            employee.BiometricId = model.BiometricId;
            employee.IsActive = model.IsActive;
            employee.UpdatedById = _userManager.GetUserId(User);
            employee.UpdatedOn = DateTime.UtcNow;

            try
            {
                _db.Employees.Update(employee);
                var result = await _db.SaveChangesAsync();

                if (result <= 0 && employee.ApplicationUser == null)
                {
                    _logger.LogError("No records updated for employee ID {Id}", id);
                    return Json(new { success = false, message = "No changes were made." });
                }

                await SyncEmployeeOrganizationAssignmentsAsync(
                    employee.Id, model.OrganizationStructureIds, model.PrimaryOrganizationStructureId, model.DateHired);
                await SyncEmployeeDesignationAsync(
                    employee.Id, model.DesignationId, model.DateHired);

                await LogAuditTrailAsync("Update Employee", oldEmployeeData, employee);
                return Json(new { success = true, message = "Employee and account updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Employee ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while updating." });
            }
        }


        //=======================================================View Employee===================================================================\


        [HttpGet]
        [Permission("EMPLOYEE_VIEW")]
        public async Task<ActionResult> ViewEmployee(Guid id)
        {
            var employee = await _db.Employees
                .Include(e => e.DefaultRateType)
                .Where(e => e.Id == id && !e.IsDeleted)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                _logger.LogWarning("Employee with ID {Id} not found.", id);
                return NotFound();
            }

            var viewModel = new EmployeeViewModel
            {
                Id = employee.Id,
                EmployeeIdNumber = employee.EmployeeIdNumber,
                Surname = employee.Surname,
                FirstName = employee.FirstName,
                MiddleName = employee.MiddleName,
                NameExtension = employee.NameExtension,
                DateOfBirth = employee.DateOfBirth,
                Sex = employee.Sex,
                TelephoneNo = employee.TelephoneNo,
                MobileNo = employee.MobileNo,
                EmailAddress = employee.EmailAddress,
                EmploymentBasis = employee.EmploymentBasis,
                DefaultRateTypeId = employee.DefaultRateTypeId, // Changed from RateTypeId
                DateHired = employee.DateHired,
                DesignationId = await EmployeeOrganizationHelper.GetActiveDesignationIdAsync(_db, employee.Id) ?? 0,
                DesignationName = (await EmployeeOrganizationHelper.GetActiveDesignationNamesAsync(_db, new[] { employee.Id }))
                    .GetValueOrDefault(employee.Id),
                BiometricId = employee.BiometricId,
                IsActive = employee.IsActive,
                OrganizationStructureIds = await GetActiveOrganizationStructureIdsAsync(employee.Id),
                PrimaryOrganizationStructureId = await EmployeeOrganizationHelper.GetPrimaryOrganizationStructureIdAsync(_db, employee.Id)
            };
            await PopulateDropdownsAsync(viewModel);
            viewModel.OrganizationUnitsDisplay = string.Join(", ",
                viewModel.OrganizationUnits
                    .Where(o => viewModel.OrganizationStructureIds.Contains(o.Id))
                    .Select(o => o.DisplayText));
            await LoadOrganizationPlacementsAsync(viewModel, employee.Id);
            return PartialView("Partials/_ViewEmployee", viewModel);
        }


        //=======================================================Delete Employee===================================================================\

        [HttpGet]
        [Permission("EMPLOYEE_VIEW")]
        public async Task<ActionResult> DeleteEmployee(Guid id)
        {
            var employee = await _db.Employees
                .AsNoTracking()
                .Where(e => e.Id == id && !e.IsDeleted)
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                _logger.LogWarning("Employee with ID {Id} not found.", id);
                return NotFound();
            }

            // Check for related PersonalInformation records
            var hasPersonalInformation = await _db.PersonalInformation.AnyAsync(pi => pi.EmployeeId == employee.Id);

            // If any restrictions exist, show warning
            if (hasPersonalInformation)
            {
                var viewModel = new EmployeeViewModel
                {
                    Id = employee.Id,
                    EmployeeIdNumber = employee.EmployeeIdNumber,
                    Surname = employee.Surname,
                    FirstName = employee.FirstName,
                    MiddleName = employee.MiddleName,
                    NameExtension = employee.NameExtension,
                };
                ViewBag.HasPersonalInformation = hasPersonalInformation;
                return PartialView("Partials/_DeleteWarningEmployee", viewModel);
            }

            // If no restrictions, show normal delete confirmation
            var deleteViewModel = new EmployeeViewModel
            {
                Id = employee.Id,
                EmployeeIdNumber = employee.EmployeeIdNumber,
                Surname = employee.Surname,
                FirstName = employee.FirstName,
                MiddleName = employee.MiddleName,
                NameExtension = employee.NameExtension,
            };

            return PartialView("Partials/_DeleteEmployee", deleteViewModel);
        }

        [HttpPost]
        [Permission("EMPLOYEE_VIEW")]
        public async Task<ActionResult> DeleteConfirmed(Guid id)
        {
            var employee = await _db.Employees.FindAsync(id);
            if (employee == null || employee.IsDeleted)
            {
                _logger.LogWarning("Employee with ID {Id} not found or already deleted.", id);
                return Json(new { success = false, message = "Employee not found or already deleted." });
            }

            // Check for related PersonalInformation records
            var hasPersonalInformation = await _db.PersonalInformation.AnyAsync(pi => pi.EmployeeId == employee.Id);
            if (hasPersonalInformation)
            {
                _logger.LogWarning("Cannot delete employee with associated personal information: {FullName}", employee.FullName);
                return Json(new
                {
                    success = false,
                    message = "Cannot delete employee because it has associated personal information. Please delete personal information first."
                });
            }

            var oldData = new EmployeeViewModel
            {
                Id = employee.Id,
                EmployeeIdNumber = employee.EmployeeIdNumber,
                Surname = employee.Surname,
                FirstName = employee.FirstName,
                MiddleName = employee.MiddleName,
                NameExtension = employee.NameExtension,
                DateOfBirth = employee.DateOfBirth,
                Sex = employee.Sex,
                TelephoneNo = employee.TelephoneNo,
                MobileNo = employee.MobileNo,
                EmailAddress = employee.EmailAddress,
                EmploymentBasis = employee.EmploymentBasis,
                DateHired = employee.DateHired,
                DesignationId = await EmployeeOrganizationHelper.GetActiveDesignationIdAsync(_db, employee.Id) ?? 0,
                BiometricId = employee.BiometricId,
                IsActive = employee.IsActive,
                DefaultRateTypeId = employee.DefaultRateTypeId // Changed from RateTypeId
            };

            employee.IsDeleted = true;
            employee.UpdatedById = _userManager.GetUserId(User);
            employee.UpdatedOn = DateTime.UtcNow;

            try
            {
                _db.Employees.Update(employee);
                var result = await _db.SaveChangesAsync();

                if (result > 0)
                {
                    await LogAuditTrailAsync("Delete Employee", oldData, null);
                    return Json(new { success = true, message = "Employee deleted successfully." });
                }

                _logger.LogError("Failed to delete employee ID {Id}.", id);
                return Json(new { success = false, message = "Failed to delete employee." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting employee ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while deleting." });
            }
        }

        //==========================================================================================================================

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Permission("EMPLOYEE_VIEW")]
        public async Task<IActionResult> GetEmployeesJson()
        {
            var draw = HttpContext.Request.Form["draw"].FirstOrDefault() ?? "1";
            var start = int.TryParse(HttpContext.Request.Form["start"].FirstOrDefault(), out var st) ? st : 0;
            var length = int.TryParse(HttpContext.Request.Form["length"].FirstOrDefault(), out var ln) ? ln : 25;
            var sortColIdx = HttpContext.Request.Form["order[0][column]"].FirstOrDefault() ?? "2";
            var sortColumn = HttpContext.Request.Form[$"columns[{sortColIdx}][data]"].FirstOrDefault();
            var sortDir = HttpContext.Request.Form["order[0][dir]"].FirstOrDefault() ?? "asc";
            var searchValue = HttpContext.Request.Form["search[value]"].FirstOrDefault();

            // Custom filters
            var filterEmployeeId = HttpContext.Request.Form["employeeId"].FirstOrDefault();
            var filterFullName = HttpContext.Request.Form["fullName"].FirstOrDefault();
            var filterDesignationId = HttpContext.Request.Form["designationId"].FirstOrDefault();
            var filterOrganizationStructureId = HttpContext.Request.Form["organizationStructureId"].FirstOrDefault();
            var filterStatus = HttpContext.Request.Form["status"].FirstOrDefault();
            var filterEmploymentBasis = HttpContext.Request.Form["employmentBasis"].FirstOrDefault();

            var query = _db.Employees
                .Where(e => !e.IsDeleted)
                .AsNoTracking()
                .AsQueryable();

            int recordsTotal = await query.CountAsync();

            // ── Apply advanced filters ─────────────────────────────────────────────────

            if (!string.IsNullOrWhiteSpace(filterEmploymentBasis)
                && Enum.TryParse<EmploymentBasis>(filterEmploymentBasis, ignoreCase: true, out var basisFilter))
            {
                query = query.Where(e => e.EmploymentBasis == basisFilter);
            }

            if (!string.IsNullOrWhiteSpace(filterEmployeeId))
                query = query.Where(e => e.EmployeeIdNumber.ToLower().Contains(filterEmployeeId.ToLower()));

            // FullName filter — dropdown sends exact full name string.
            // Since FullName may be a computed property, match against stored columns instead.
            if (!string.IsNullOrWhiteSpace(filterFullName))
            {
                var name = filterFullName.ToLower();
                query = query.Where(e =>
                    (e.FirstName + " " + e.MiddleName + " " + e.Surname).ToLower().Contains(name) ||
                    (e.FirstName + " " + e.Surname).ToLower().Contains(name));
            }

            if (!string.IsNullOrWhiteSpace(filterDesignationId) && int.TryParse(filterDesignationId, out var designationInt))
                query = query.FilterByActiveDesignation(designationInt);

            if (!string.IsNullOrWhiteSpace(filterOrganizationStructureId) &&
                int.TryParse(filterOrganizationStructureId, out var orgUnitInt))
                query = query.FilterByActiveOrganizationUnit(_db, orgUnitInt);

            if (!string.IsNullOrWhiteSpace(filterStatus) && bool.TryParse(filterStatus, out bool isActive))
                query = query.Where(e => e.IsActive == isActive);

            // ── Apply global search ────────────────────────────────────────────────────

            if (!string.IsNullOrWhiteSpace(searchValue))
            {
                var s = searchValue.ToLower();
                query = query.Where(e =>
                    e.EmployeeIdNumber.ToLower().Contains(s) ||
                    e.FirstName.ToLower().Contains(s) ||
                    e.Surname.ToLower().Contains(s) ||
                    (e.MiddleName != null && e.MiddleName.ToLower().Contains(s)) ||
                    _db.EmployeeDesignations.Any(d =>
                        d.EmployeeId == e.Id &&
                        d.EffectiveTo == null &&
                        d.Designation != null &&
                        d.Designation.DesignationName.ToLower().Contains(s)) ||
                    _db.EmployeeOrganizationAssignments.Any(a =>
                        a.EmployeeId == e.Id &&
                        a.OrganizationStructure != null &&
                        a.OrganizationStructure.Name.ToLower().Contains(s)) ||
                    // To support full name searches like "Juan Cruz" or "Juan Dela Cruz"
                    (e.FirstName + " " + e.Surname).ToLower().Contains(s) ||
                    (e.FirstName + " " + e.MiddleName + " " + e.Surname).ToLower().Contains(s) ||
                    (e.Surname + ", " + e.FirstName).ToLower().Contains(s)
                );
            }

            int recordsFiltered = await query.CountAsync();

            // ── Sorting ────────────────────────────────────────────────────────────────

            query = sortColumn switch
            {
                "employeeIdNumber" => sortDir == "asc"
                    ? query.OrderBy(e => e.EmployeeIdNumber)
                    : query.OrderByDescending(e => e.EmployeeIdNumber),

                "fullName" => sortDir == "asc"
                    ? query.OrderBy(e => e.Surname).ThenBy(e => e.FirstName)
                    : query.OrderByDescending(e => e.Surname).ThenByDescending(e => e.FirstName),

                "designationName" => sortDir == "asc"
                    ? query.OrderBy(e => e.Designations
                        .Where(d => d.EffectiveTo == null)
                        .Select(d => d.Designation!.DesignationName)
                        .FirstOrDefault() ?? "")
                    : query.OrderByDescending(e => e.Designations
                        .Where(d => d.EffectiveTo == null)
                        .Select(d => d.Designation!.DesignationName)
                        .FirstOrDefault() ?? ""),

                "organizationUnitName" => sortDir == "asc"
                    ? query.OrderBy(e => e.Surname).ThenBy(e => e.FirstName)
                    : query.OrderByDescending(e => e.Surname).ThenByDescending(e => e.FirstName),

                "isActive" => sortDir == "asc"
                    ? query.OrderBy(e => e.IsActive)
                    : query.OrderByDescending(e => e.IsActive),

                _ => query.OrderBy(e => e.Surname).ThenBy(e => e.FirstName)
            };

            // ── Paging & projection ────────────────────────────────────────────────────

            var page = await query
                .Skip(start)
                .Take(length)
                .Select(e => new
                {
                    id = e.Id,
                    employeeIdNumber = e.EmployeeIdNumber,
                    fullName = (e.FirstName + " " +
                                       (e.MiddleName != null && e.MiddleName != "" ? e.MiddleName + " " : "") +
                                       e.Surname).Trim(),
                    designationName = e.Designations
                        .Where(d => d.EffectiveTo == null)
                        .Select(d => d.Designation!.DesignationName)
                        .FirstOrDefault() ?? "",
                    isActive = e.IsActive
                })
                .ToListAsync();

            var unitNames = await EmployeeOrganizationHelper.GetActiveUnitDisplayNamesAsync(
                _db, page.Select(p => p.id));

            var data = page.Select(p => new
            {
                p.id,
                p.employeeIdNumber,
                p.fullName,
                p.designationName,
                organizationUnitName = unitNames.GetValueOrDefault(p.id, ""),
                p.isActive
            }).ToList();

            return Json(new
            {
                draw = int.Parse(draw),
                recordsTotal,
                recordsFiltered,
                data
            });
        }
    }
}
