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
    public class DayTypeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<DayTypeController> _logger;

        public DayTypeController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<DayTypeController> logger)
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
                await _auditHelper.LogAuditTrail(action, "DayType", "Day Types", oldData, newData);
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

        private static void MapViewModelToEntity(DayTypeViewModel model, DayType dayType)
        {
            dayType.Name = model.Name;
            dayType.Code = model.Code;
            dayType.Description = model.Description;
            dayType.BasicMultiplier = model.BasicMultiplier;
            dayType.OvertimeMultiplier = model.OvertimeMultiplier;
            dayType.NightDifferentialMultiplier = model.NightDifferentialMultiplier;
            dayType.NightDifferentialOvertimeMultiplier = model.NightDifferentialOvertimeMultiplier;
            dayType.EffectiveFrom = model.EffectiveFrom.Date;
            dayType.EffectiveTo = model.EffectiveTo?.Date;
            dayType.IsActive = model.IsActive;
        }

        private static DayTypeViewModel MapEntityToViewModel(DayType dayType) => new()
        {
            Id = dayType.Id,
            Name = dayType.Name,
            Code = dayType.Code,
            Description = dayType.Description,
            BasicMultiplier = dayType.BasicMultiplier,
            OvertimeMultiplier = dayType.OvertimeMultiplier,
            NightDifferentialMultiplier = dayType.NightDifferentialMultiplier,
            NightDifferentialOvertimeMultiplier = dayType.NightDifferentialOvertimeMultiplier,
            EffectiveFrom = dayType.EffectiveFrom,
            EffectiveTo = dayType.EffectiveTo,
            IsActive = dayType.IsActive
        };

        [Permission("DAYTYPE_VIEW")]
        public async Task<ActionResult> Index()
        {
            var dayTypes = await _db.DayTypes
                .AsNoTracking()
                .Where(dt => !dt.IsDeleted)
                .OrderBy(dt => dt.Name)
                .ThenByDescending(dt => dt.EffectiveFrom)
                .ToListAsync();

            return View(dayTypes);
        }

        [Permission("DAYTYPE_VIEW")]
        public ActionResult CreateDayType()
        {
            return PartialView("Partials/_CreateDayType", new DayTypeViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("DAYTYPE_VIEW")]
        public async Task<ActionResult> CreateDayType(DayTypeViewModel model)
        {
            if (!ModelState.IsValid)
                return ValidationErrorResponse();

            try
            {
                if (await _db.DayTypes.AnyAsync(dt => !dt.IsDeleted && dt.Name == model.Name))
                    return Json(new { success = false, message = "Day type name already exists." });

                if (await _db.DayTypes.AnyAsync(dt => !dt.IsDeleted && dt.Code == model.Code))
                    return Json(new { success = false, message = "Day type code already exists." });

                var dayType = new DayType
                {
                    CreatedById = _userManager.GetUserId(User),
                    CreatedOn = DateTime.UtcNow
                };
                MapViewModelToEntity(model, dayType);

                _db.DayTypes.Add(dayType);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("Failed to save day type: {Name}", model.Name);
                    return Json(new { success = false, message = "Failed to create day type." });
                }

                await LogAuditTrailAsync("Create Day Type", null, dayType);
                return Json(new { success = true, message = "Day type created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating day type: {Name}", model.Name);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }

        [HttpGet]
        [Permission("DAYTYPE_VIEW")]
        public async Task<ActionResult> EditDayType(int id)
        {
            var dayType = await _db.DayTypes.FindAsync(id);
            if (dayType == null || dayType.IsDeleted) return NotFound();

            return PartialView("Partials/_EditDayType", MapEntityToViewModel(dayType));
        }

        [HttpPost]
        [Permission("DAYTYPE_VIEW")]
        public async Task<ActionResult> EditDayType(int id, DayTypeViewModel model)
        {
            if (!ModelState.IsValid)
                return ValidationErrorResponse();

            var dayType = await _db.DayTypes.FindAsync(id);
            if (dayType == null || dayType.IsDeleted)
                return Json(new { success = false, message = "Day type not found." });

            if (dayType.Name != model.Name &&
                await _db.DayTypes.AnyAsync(dt => !dt.IsDeleted && dt.Name == model.Name && dt.Id != id))
                return Json(new { success = false, message = "Another day type with the same name already exists." });

            if (dayType.Code != model.Code &&
                await _db.DayTypes.AnyAsync(dt => !dt.IsDeleted && dt.Code == model.Code && dt.Id != id))
                return Json(new { success = false, message = "Another day type with the same code already exists." });

            var oldDayTypeData = JsonSerializer.Deserialize<DayType>(JsonSerializer.Serialize(dayType));

            MapViewModelToEntity(model, dayType);
            dayType.UpdatedById = _userManager.GetUserId(User);
            dayType.UpdatedOn = DateTime.UtcNow;

            try
            {
                _db.DayTypes.Update(dayType);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                    return Json(new { success = false, message = "No changes were made." });

                await LogAuditTrailAsync("Update Day Type", oldDayTypeData, dayType);
                return Json(new { success = true, message = "Day type updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Day Type ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while updating." });
            }
        }

        [HttpGet]
        [Permission("DAYTYPE_VIEW")]
        public async Task<ActionResult> ViewDayType(int id)
        {
            var dayType = await _db.DayTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(dt => dt.Id == id && !dt.IsDeleted);

            if (dayType == null) return NotFound();

            return PartialView("Partials/_ViewDayType", MapEntityToViewModel(dayType));
        }

        [HttpGet]
        [Permission("DAYTYPE_VIEW")]
        public async Task<ActionResult> DeleteDayType(int id)
        {
            var dayType = await _db.DayTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(dt => dt.Id == id && !dt.IsDeleted);

            if (dayType == null) return NotFound();

            var isInUse = await _db.PayrollPeriodTimeCardDetails.AnyAsync(d => d.DayTypeId == dayType.Id);

            if (isInUse)
            {
                ViewBag.HasRates = true;
                return PartialView("Partials/_DeleteWarningDayType", MapEntityToViewModel(dayType));
            }

            return PartialView("Partials/_DeleteDayType", MapEntityToViewModel(dayType));
        }

        [HttpPost]
        [Permission("DAYTYPE_VIEW")]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            var dayType = await _db.DayTypes.FindAsync(id);
            if (dayType == null || dayType.IsDeleted)
                return Json(new { success = false, message = "Day type not found or already deleted." });

            if (await _db.PayrollPeriodTimeCardDetails.AnyAsync(d => d.DayTypeId == dayType.Id))
            {
                return Json(new
                {
                    success = false,
                    message = "Cannot delete day type because it is used on employee time cards."
                });
            }

            var oldData = MapEntityToViewModel(dayType);

            try
            {
                dayType.IsDeleted = true;
                dayType.UpdatedById = _userManager.GetUserId(User);
                dayType.UpdatedOn = DateTime.UtcNow;

                _db.DayTypes.Update(dayType);
                var result = await _db.SaveChangesAsync();

                if (result > 0)
                {
                    await LogAuditTrailAsync("Delete Day Type", oldData, null);
                    return Json(new { success = true, message = "Day type deleted successfully." });
                }

                return Json(new { success = false, message = "Failed to delete day type." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting day type ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while deleting." });
            }
        }
    }
}
