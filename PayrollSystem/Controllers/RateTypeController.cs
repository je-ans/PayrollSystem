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
    public class RateTypeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<RateTypeController> _logger;

        public RateTypeController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<RateTypeController> logger)
        {
            _userManager = userManager;
            _db = db;
            _auditHelper = auditHelper;
            _logger = logger;
        }

        //=======================================================Private Method=================================================================

        private async Task LogAuditTrailAsync(string action, object? oldData, object? newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "RateType", "Rate Types", oldData, newData);
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

        //=======================================================Get Rate Type List=============================================================

        [Permission("RATETYPE_VIEW")]
        public async Task<ActionResult> Index()
        {
            var rateTypes = await _db.RateTypes
                .AsNoTracking()
                .Where(rt => !rt.IsDeleted)
                .ToListAsync();

            var rateTypesWithEmployees = new List<int>();
            foreach (var rateType in rateTypes)
            {
                var hasEmployees = await _db.Employees.AnyAsync(e => e.DefaultRateTypeId == rateType.Id && !e.IsDeleted); // Changed from RateTypeId
                if (hasEmployees)
                {
                    rateTypesWithEmployees.Add(rateType.Id);
                }
            }

            ViewBag.RateTypesWithEmployees = rateTypesWithEmployees;
            return View(rateTypes);
        }

        //=======================================================Create Rate Type==============================================================

        [Permission("RATETYPE_VIEW")]
        public ActionResult CreateRateType()
        {
            return PartialView("Partials/_CreateRateType");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("RATETYPE_VIEW")]
        public async Task<ActionResult> CreateRateType(RateTypeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            try
            {
                if (await _db.RateTypes.AnyAsync(rt =>
                    rt.Name == model.Name && !rt.IsDeleted))
                {
                    return Json(new { success = false, message = "Rate type name already exists." });
                }

                var rateType = new RateType
                {
                    Name = model.Name,
                    Code = model.Code,
                    Description = model.Description,
                    MonthlySalary = model.MonthlySalary,
                    DailyRate = model.DailyRate,
                    HourlyRate = model.HourlyRate,
                    RateBasis = model.RateBasis,
                    EffectiveStartDate = model.EffectiveStartDate,
                    EffectiveEndDate = model.EffectiveEndDate,
                    IsActive = model.IsActive,
                    CreatedById = _userManager.GetUserId(User),
                    CreatedOn = DateTime.UtcNow
                };

                _db.RateTypes.Add(rateType);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("Failed to save rate type: {Name}", model.Name);
                    return Json(new { success = false, message = "Failed to create rate type." });
                }

                await LogAuditTrailAsync("Create Rate Type", null, rateType);
                return Json(new { success = true, message = "Rate type created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating rate type: {Name}", model.Name);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }

        //=======================================================Edit Rate Type===================================================================

        [HttpGet]
        [Permission("RATETYPE_VIEW")]
        public async Task<ActionResult> EditRateType(int id)
        {
            var rateType = await _db.RateTypes.FindAsync(id);
            if (rateType == null) return NotFound();

            var viewModel = new RateTypeViewModel
            {
                Id = rateType.Id,
                Name = rateType.Name,
                Code = rateType.Code,
                Description = rateType.Description,
                MonthlySalary = rateType.MonthlySalary,
                DailyRate = rateType.DailyRate,
                HourlyRate = rateType.HourlyRate,
                RateBasis = rateType.RateBasis,
                EffectiveStartDate = rateType.EffectiveStartDate,
                EffectiveEndDate = rateType.EffectiveEndDate,
                IsActive = rateType.IsActive
            };

            return PartialView("Partials/_EditRateType", viewModel);
        }

        [HttpPost]
        [Permission("RATETYPE_VIEW")]
        public async Task<ActionResult> EditRateType(int id, RateTypeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            var rateType = await _db.RateTypes.FindAsync(id);
            if (rateType == null)
            {
                _logger.LogError("Rate type with ID {Id} not found.", id);
                return Json(new { success = false, message = "Rate type not found." });
            }

            if (rateType.Name != model.Name &&
                await _db.RateTypes.AnyAsync(rt =>
                    rt.Name == model.Name && !rt.IsDeleted && rt.Id != id))
            {
                return Json(new { success = false, message = "Another rate type with the same name already exists." });
            }

            var oldRateTypeData = JsonSerializer.Deserialize<RateType>(
                JsonSerializer.Serialize(rateType));

            // Manual mapping
            rateType.Name = model.Name;
            rateType.Code = model.Code;
            rateType.Description = model.Description;
            rateType.MonthlySalary = model.MonthlySalary;
            rateType.DailyRate = model.DailyRate;
            rateType.HourlyRate = model.HourlyRate;
            rateType.RateBasis = model.RateBasis;
            rateType.EffectiveStartDate = model.EffectiveStartDate;
            rateType.EffectiveEndDate = model.EffectiveEndDate;
            rateType.IsActive = model.IsActive;
            rateType.UpdatedById = _userManager.GetUserId(User);
            rateType.UpdatedOn = DateTime.UtcNow;

            try
            {
                _db.RateTypes.Update(rateType);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("No records updated for rate type ID {Id}", id);
                    return Json(new { success = false, message = "No changes were made." });
                }

                await LogAuditTrailAsync("Update Rate Type", oldRateTypeData, rateType);
                return Json(new { success = true, message = "Rate type updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Rate Type ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while updating." });
            }
        }

        //=======================================================View Rate Type===================================================================

        [HttpGet]
        [Permission("RATETYPE_VIEW")]
        public async Task<ActionResult> ViewRateType(int id)
        {
            var rateType = await _db.RateTypes
                .AsNoTracking()
                .Where(rt => rt.Id == id && !rt.IsDeleted)
                .FirstOrDefaultAsync();

            if (rateType == null)
            {
                _logger.LogWarning("Rate type with ID {Id} not found.", id);
                return NotFound();
            }

            var viewModel = new RateTypeViewModel
            {
                Id = rateType.Id,
                Name = rateType.Name,
                Code = rateType.Code,
                Description = rateType.Description,
                MonthlySalary = rateType.MonthlySalary,
                DailyRate = rateType.DailyRate,
                HourlyRate = rateType.HourlyRate,
                RateBasis = rateType.RateBasis,
                EffectiveStartDate = rateType.EffectiveStartDate,
                EffectiveEndDate = rateType.EffectiveEndDate,
                IsActive = rateType.IsActive
            };

            return PartialView("Partials/_ViewRateType", viewModel);
        }

        //=======================================================Delete Rate Type===================================================================

        [HttpGet]
        [Permission("RATETYPE_VIEW")]
        public async Task<ActionResult> DeleteRateType(int id)
        {
            var rateType = await _db.RateTypes
                .AsNoTracking()
                .Where(rt => rt.Id == id && !rt.IsDeleted)
                .FirstOrDefaultAsync();

            if (rateType == null)
            {
                _logger.LogWarning("Rate type with ID {Id} not found.", id);
                return NotFound();
            }

            // Check for employees assigned to this rate type
            var hasEmployees = await _db.Employees.AnyAsync(e => e.DefaultRateTypeId == rateType.Id && !e.IsDeleted); // Changed from RateTypeId

            // If any restrictions exist, show warning
            if (hasEmployees)
            {
                var viewModel = new RateTypeViewModel
                {
                    Id = rateType.Id,
                    Name = rateType.Name
                };
                ViewBag.HasEmployees = hasEmployees;
                return PartialView("Partials/_DeleteWarningRateType", viewModel);
            }

            // If no restrictions, show normal delete confirmation
            var deleteViewModel = new RateTypeViewModel
            {
                Id = rateType.Id,
                Name = rateType.Name
            };

            return PartialView("Partials/_DeleteRateType", deleteViewModel);
        }

        [HttpPost]
        [Permission("RATETYPE_VIEW")]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            var rateType = await _db.RateTypes.FindAsync(id);
            if (rateType == null || rateType.IsDeleted)
            {
                _logger.LogWarning("Rate type with ID {Id} not found or already deleted.", id);
                return Json(new { success = false, message = "Rate type not found or already deleted." });
            }

            // Check for employees assigned to this rate type
            var hasEmployees = await _db.Employees.AnyAsync(e => e.DefaultRateTypeId == rateType.Id && !e.IsDeleted); // Changed from RateTypeId
            if (hasEmployees)
            {
                _logger.LogWarning("Cannot delete rate type with assigned employees: {Name}", rateType.Name);
                return Json(new
                {
                    success = false,
                    message = "Cannot delete rate type because it has assigned employees. Please reassign employees first."
                });
            }

            var oldData = new RateTypeViewModel
            {
                Id = rateType.Id,
                Name = rateType.Name,
                Code = rateType.Code,
                Description = rateType.Description,
                MonthlySalary = rateType.MonthlySalary,
                DailyRate = rateType.DailyRate,
                HourlyRate = rateType.HourlyRate,
                RateBasis = rateType.RateBasis,
                EffectiveStartDate = rateType.EffectiveStartDate,
                EffectiveEndDate = rateType.EffectiveEndDate,
                IsActive = rateType.IsActive
            };

            rateType.IsDeleted = true;
            rateType.UpdatedById = _userManager.GetUserId(User);
            rateType.UpdatedOn = DateTime.UtcNow;

            try
            {
                _db.RateTypes.Update(rateType);
                var result = await _db.SaveChangesAsync();

                if (result > 0)
                {
                    await LogAuditTrailAsync("Delete Rate Type", oldData, null);
                    return Json(new { success = true, message = "Rate type deleted successfully." });
                }

                _logger.LogError("Failed to delete rate type ID {Id}.", id);
                return Json(new { success = false, message = "Failed to delete rate type." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting rate type ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while deleting." });
            }
        }

        //==========================================================================================================================
    }
}
