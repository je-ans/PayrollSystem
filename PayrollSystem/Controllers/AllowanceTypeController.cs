using System.Text.Json;
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
    public class AllowanceTypeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<AllowanceTypeController> _logger;

        public AllowanceTypeController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<AllowanceTypeController> logger)
        {
            _userManager = userManager;
            _db = db;
            _auditHelper = auditHelper;
            _logger = logger;
        }

        private async Task LogAuditTrailAsync(string action, object oldData, object newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "AllowanceType", "Allowance Types", oldData, newData);
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

        [Permission("ALLOWANCETYPE_VIEW")]
        public async Task<ActionResult> Index()
        {
            var allowanceTypes = await _db.AllowanceTypes
                .AsNoTracking()
                .Where(at => !at.IsDeleted)
                .OrderBy(at => at.Name)
                .ToListAsync();

            var typesInUse = new List<int>();
            foreach (var at in allowanceTypes)
            {
                var inUse = await _db.Allowances.AnyAsync(a => a.AllowanceTypeId == at.Id && !a.IsDeleted);
                if (inUse) typesInUse.Add(at.Id);
            }
            ViewBag.AllowanceTypesInUse = typesInUse;

            return View(allowanceTypes);
        }

        [Permission("ALLOWANCETYPE_VIEW")]
        public IActionResult Create()
        {
            return PartialView("Partials/_CreateAllowanceType");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("ALLOWANCETYPE_VIEW")]
        public async Task<ActionResult> Create(AllowanceTypeViewModel model)
        {
            if (!ModelState.IsValid)
                return ValidationErrorResponse();

            try
            {
                if (await _db.AllowanceTypes.AnyAsync(at => at.Name == model.Name && !at.IsDeleted))
                {
                    return Json(new { success = false, message = "Allowance type name already exists." });
                }

                var allowanceType = new AllowanceType
                {
                    Name = model.Name,
                    Code = model.Code,
                    Description = model.Description,
                    IsActive = model.IsActive,
                    CreatedById = _userManager.GetUserId(User),
                    CreatedOn = DateTime.UtcNow
                };

                _db.AllowanceTypes.Add(allowanceType);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("Failed to save allowance type: {Name}", model.Name);
                    return Json(new { success = false, message = "Failed to create allowance type." });
                }

                await LogAuditTrailAsync("Create Allowance Type", null, allowanceType);
                return Json(new { success = true, message = "Allowance type created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating allowance type: {Name}", model.Name);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }

        [HttpGet]
        [Permission("ALLOWANCETYPE_VIEW")]
        public async Task<ActionResult> Edit(int id)
        {
            var allowanceType = await _db.AllowanceTypes.FindAsync(id);
            if (allowanceType == null) return NotFound();

            var viewModel = new AllowanceTypeViewModel
            {
                Id = allowanceType.Id,
                Name = allowanceType.Name,
                Code = allowanceType.Code,
                Description = allowanceType.Description,
                IsActive = allowanceType.IsActive
            };

            return PartialView("Partials/_EditAllowanceType", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("ALLOWANCETYPE_VIEW")]
        public async Task<ActionResult> Edit(int id, AllowanceTypeViewModel model)
        {
            if (!ModelState.IsValid)
                return ValidationErrorResponse();

            var allowanceType = await _db.AllowanceTypes.FindAsync(id);
            if (allowanceType == null)
            {
                _logger.LogError("Allowance type with ID {Id} not found.", id);
                return Json(new { success = false, message = "Allowance type not found." });
            }

            if (allowanceType.Name != model.Name &&
                await _db.AllowanceTypes.AnyAsync(at => at.Name == model.Name && at.Id != id && !at.IsDeleted))
            {
                return Json(new { success = false, message = "Another allowance type with the same name already exists." });
            }

            var oldData = JsonSerializer.Deserialize<AllowanceType>(JsonSerializer.Serialize(allowanceType));

            allowanceType.Name = model.Name;
            allowanceType.Code = model.Code;
            allowanceType.Description = model.Description;
            allowanceType.IsActive = model.IsActive;
            allowanceType.UpdatedById = _userManager.GetUserId(User);
            allowanceType.UpdatedOn = DateTime.UtcNow;

            try
            {
                _db.AllowanceTypes.Update(allowanceType);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("No records updated for allowance type ID {Id}", id);
                    return Json(new { success = false, message = "No changes were made." });
                }

                await LogAuditTrailAsync("Update Allowance Type", oldData, allowanceType);
                return Json(new { success = true, message = "Allowance type updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Allowance Type ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while updating." });
            }
        }

        [HttpGet]
        [Permission("ALLOWANCETYPE_VIEW")]
        public async Task<ActionResult> View(int id)
        {
            var allowanceType = await _db.AllowanceTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(at => at.Id == id && !at.IsDeleted);

            if (allowanceType == null) return NotFound();

            var viewModel = new AllowanceTypeViewModel
            {
                Id = allowanceType.Id,
                Name = allowanceType.Name,
                Code = allowanceType.Code,
                Description = allowanceType.Description,
                IsActive = allowanceType.IsActive
            };

            return PartialView("Partials/_ViewAllowanceType", viewModel);
        }

        [HttpGet]
        [Permission("ALLOWANCETYPE_VIEW")]
        public async Task<ActionResult> Delete(int id)
        {
            var allowanceType = await _db.AllowanceTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(at => at.Id == id && !at.IsDeleted);

            if (allowanceType == null)
            {
                _logger.LogWarning("Allowance type with ID {Id} not found.", id);
                return NotFound();
            }

            var inUse = await _db.Allowances.AnyAsync(a => a.AllowanceTypeId == allowanceType.Id && !a.IsDeleted);

            if (inUse)
            {
                ViewBag.InUse = true;
                return PartialView("Partials/_DeleteWarningAllowanceType", new AllowanceTypeViewModel { Id = allowanceType.Id, Name = allowanceType.Name });
            }

            return PartialView("Partials/_DeleteAllowanceType", new AllowanceTypeViewModel { Id = allowanceType.Id, Name = allowanceType.Name });
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Permission("ALLOWANCETYPE_VIEW")]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            var allowanceType = await _db.AllowanceTypes.FindAsync(id);
            if (allowanceType == null || allowanceType.IsDeleted)
            {
                _logger.LogWarning("Allowance type with ID {Id} not found or already deleted.", id);
                return Json(new { success = false, message = "Allowance type not found or already deleted." });
            }

            var inUse = await _db.Allowances.AnyAsync(a => a.AllowanceTypeId == allowanceType.Id && !a.IsDeleted);
            if (inUse)
            {
                return Json(new
                {
                    success = false,
                    message = "Cannot delete allowance type because it is assigned to one or more employee allowances. Remove or reassign those allowances first."
                });
            }

            var oldData = new AllowanceTypeViewModel
            {
                Id = allowanceType.Id,
                Name = allowanceType.Name,
                Code = allowanceType.Code,
                Description = allowanceType.Description,
                IsActive = allowanceType.IsActive
            };

            try
            {
                allowanceType.IsDeleted = true;
                allowanceType.UpdatedById = _userManager.GetUserId(User);
                allowanceType.UpdatedOn = DateTime.UtcNow;

                _db.AllowanceTypes.Update(allowanceType);
                var result = await _db.SaveChangesAsync();

                if (result > 0)
                {
                    await LogAuditTrailAsync("Delete Allowance Type", oldData, null);
                    return Json(new { success = true, message = "Allowance type deleted successfully." });
                }

                _logger.LogError("Failed to delete allowance type ID {Id}.", id);
                return Json(new { success = false, message = "Failed to delete allowance type." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting allowance type ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while deleting." });
            }
        }
    }
}
