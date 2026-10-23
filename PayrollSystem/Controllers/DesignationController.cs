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
    public class DesignationController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<DesignationController> _logger;

        public DesignationController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<DesignationController> logger)
        {
            _userManager = userManager;
            _db = db;
            _auditHelper = auditHelper;
            _logger = logger;
        }

        //===========================================================Private Method=======================================================================

        private async Task LogAuditTrailAsync(string action, object oldData, object newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "Designation", "Designations", oldData, newData);
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


        //============================================================Get Designation List========================================================================

        [Permission("DESIGNATION_VIEW")]
        public async Task<ActionResult> Index()
        {
            var designations = await _db.Designations
                .AsNoTracking()
                .Where(d => !d.IsDeleted)
                .ToListAsync();

            // Get all designations that have users assigned
            var designationsWithUsers = new List<int>();
            foreach (var designation in designations)
            {
                var hasUsers = await _db.Users.AnyAsync(u => u.DesignationId == designation.Id && !u.IsDeleted);
                if (hasUsers)
                {
                    designationsWithUsers.Add(designation.Id);
                }
            }

            ViewBag.DesignationsWithUsers = designationsWithUsers;
            return View(designations);
        }


        //============================================================Create Designation========================================================================

        [Permission("DESIGNATION_VIEW")]
        public ActionResult CreateDesignation()
        {
            return PartialView("Partials/CreateDesignation");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("DESIGNATION_VIEW")]
        public async Task<ActionResult> CreateDesignation(DesignationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            try
            {
                if (await _db.Designations.AnyAsync(d =>
                    d.DesignationName == model.DesignationName && !d.IsDeleted))
                {
                    return Json(new { success = false, message = "Designation name already exists." });
                }

                var designation = new Designation
                {
                    DesignationName = model.DesignationName,
                    DesignationDescription = model.DesignationDescription,
                    IsActive = model.IsActive,
                    CreatedById = _userManager.GetUserId(User),
                    CreatedOn = DateTime.UtcNow
                };

                _db.Designations.Add(designation);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("Failed to save designation: {DesignationName}", model.DesignationName);
                    return Json(new { success = false, message = "Failed to create designation." });
                }

                await LogAuditTrailAsync("Create Designation", null, designation);
                return Json(new { success = true, message = "Designation created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating designation: {DesignationName}", model.DesignationName);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }


        //============================================================Edit Designation========================================================================

        [HttpGet]
        [Permission("DESIGNATION_VIEW")]
        public async Task<ActionResult> EditDesignation(int id)
        {
            var designation = await _db.Designations.FindAsync(id);
            if (designation == null) return NotFound();

            var viewModel = new DesignationViewModel
            {
                Id = designation.Id,
                DesignationName = designation.DesignationName,
                DesignationDescription = designation.DesignationDescription,
                IsActive = designation.IsActive
            };

            return PartialView("Partials/EditDesignation", viewModel);
        }


        [HttpPost]
        [Permission("DESIGNATION_VIEW")]
        public async Task<ActionResult> EditDesignation(int id, DesignationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            var designation = await _db.Designations.FindAsync(id);
            if (designation == null)
            {
                _logger.LogError("Designation with ID {Id} not found.", id);
                return Json(new { success = false, message = "Designation not found." });
            }

            if (designation.DesignationName != model.DesignationName &&
                await _db.Designations.AnyAsync(d =>
                    d.DesignationName == model.DesignationName && !d.IsDeleted && d.Id != id))
            {
                return Json(new { success = false, message = "Another designation with the same name already exists." });
            }

            var oldDesignationData = JsonSerializer.Deserialize<Designation>(
                JsonSerializer.Serialize(designation));

            designation.DesignationName = model.DesignationName;
            designation.DesignationDescription = model.DesignationDescription;
            designation.IsActive = model.IsActive;
            designation.UpdatedById = _userManager.GetUserId(User);
            designation.UpdatedOn = DateTime.UtcNow;

            try
            {
                _db.Designations.Update(designation);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("No records updated for designation ID {Id}", id);
                    return Json(new { success = false, message = "No changes were made." });
                }

                await LogAuditTrailAsync("Update Designation", oldDesignationData, designation);
                return Json(new { success = true, message = "Designation updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Designation ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while updating." });
            }
        }


        //============================================================View Designation========================================================================

        [HttpGet]
        [Permission("DESIGNATION_VIEW")]
        public async Task<ActionResult> ViewDesignation(int id)
        {
            var designation = await _db.Designations
                .AsNoTracking()
                .Where(d => d.Id == id && !d.IsDeleted)
                .FirstOrDefaultAsync();

            if (designation == null)
            {
                _logger.LogWarning("Designation with ID {Id} not found.", id);
                return NotFound();
            }

            var viewModel = new DesignationViewModel
            {
                Id = designation.Id,
                DesignationName = designation.DesignationName,
                DesignationDescription = designation.DesignationDescription,
                IsActive = designation.IsActive
            };

            return PartialView("Partials/ViewDesignation", viewModel);
        }


        //============================================================Delete Designation========================================================================

        [HttpGet]
        [Permission("DESIGNATION_VIEW")]
        public async Task<ActionResult> DeleteDesignation(int id)
        {
            var designation = await _db.Designations
                .AsNoTracking()
                .Where(d => d.Id == id && !d.IsDeleted)
                .FirstOrDefaultAsync();

            if (designation == null)
            {
                _logger.LogWarning("Designation with ID {Id} not found.", id);
                return NotFound();
            }

            // Check for users assigned to this designation
            var hasUsers = await _db.Users.AnyAsync(u => u.DesignationId == designation.Id && !u.IsDeleted);

            // If any restrictions exist, show warning
            if (hasUsers)
            {
                var viewModel = new DesignationViewModel
                {
                    Id = designation.Id,
                    DesignationName = designation.DesignationName,
                    DesignationDescription = designation.DesignationDescription,
                    IsActive = designation.IsActive
                };
                ViewBag.HasUsers = hasUsers;
                return PartialView("Partials/DeleteWarningDesignation", viewModel);
            }

            // If no restrictions, show normal delete confirmation
            var deleteViewModel = new DesignationViewModel
            {
                Id = designation.Id,
                DesignationName = designation.DesignationName,
                DesignationDescription = designation.DesignationDescription,
                IsActive = designation.IsActive
            };

            return PartialView("Partials/DeleteDesignation", deleteViewModel);
        }


        [HttpPost]
        [Permission("DESIGNATION_VIEW")]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            var designation = await _db.Designations.FindAsync(id);
            if (designation == null || designation.IsDeleted)
            {
                _logger.LogWarning("Designation with ID {Id} not found or already deleted.", id);
                return Json(new { success = false, message = "Designation not found or already deleted." });
            }

            // Check for users assigned to this designation
            var hasUsers = await _db.Users.AnyAsync(u => u.DesignationId == designation.Id && !u.IsDeleted);
            if (hasUsers)
            {
                _logger.LogWarning("Cannot delete designation with assigned users: {DesignationName}", designation.DesignationName);
                return Json(new
                {
                    success = false,
                    message = "Cannot delete designation because it has assigned users. Please reassign users first."
                });
            }

            var oldData = new DesignationViewModel
            {
                Id = designation.Id,
                DesignationName = designation.DesignationName,
                DesignationDescription = designation.DesignationDescription,
                IsActive = designation.IsActive
            };

            designation.IsDeleted = true;
            designation.UpdatedById = _userManager.GetUserId(User);
            designation.UpdatedOn = DateTime.UtcNow;

            try
            {
                _db.Designations.Update(designation);
                var result = await _db.SaveChangesAsync();

                if (result > 0)
                {
                    await LogAuditTrailAsync("Delete Designation", oldData, null);
                    return Json(new { success = true, message = "Designation deleted successfully." });
                }

                _logger.LogError("Failed to delete designation ID {Id}.", id);
                return Json(new { success = false, message = "Failed to delete designation." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting designation ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while deleting." });
            }
        }

    }
}
