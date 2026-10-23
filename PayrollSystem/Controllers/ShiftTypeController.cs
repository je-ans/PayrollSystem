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
    public class ShiftTypeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<ShiftTypeController> _logger;

        public ShiftTypeController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<ShiftTypeController> logger)
        {
            _userManager = userManager;
            _db = db;
            _auditHelper = auditHelper;
            _logger = logger;
        }

        private async Task LogAuditTrailAsync(string action, object? oldData, object? newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "ShiftType", "Shift Types", oldData, newData);
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

        private static bool TryValidateSchedule(ShiftTypeViewModel model, out string? errorMessage)
        {
            ShiftTypeScheduleHelper.ApplyLeaveDefaults(model);

            if (!model.IsLeave && !model.HasLunchBreak)
            {
                model.LunchStart = null;
                model.LunchEnd = null;
            }

            if (!model.IsLeave && model.IsFlexible)
            {
                model.StartTime = null;
                model.EndTime = null;
                model.SpansNextDay = false;
                model.HasLunchBreak = false;
                model.LunchStart = null;
                model.LunchEnd = null;
            }

            var errors = ShiftTypeScheduleHelper.ValidateSchedule(model);
            if (errors.Count > 0)
            {
                errorMessage = string.Join("; ", errors);
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static void MapViewModelToEntity(ShiftTypeViewModel model, ShiftType shiftType)
        {
            ShiftTypeScheduleHelper.ApplyLeaveDefaults(model);

            shiftType.Code = model.Code.Trim();
            shiftType.Label = model.Label.Trim();
            shiftType.Hours = model.Hours;
            shiftType.IsLeave = model.IsLeave;
            shiftType.IsFlexible = model.IsFlexible;
            shiftType.StartTime = model.StartTime;
            shiftType.EndTime = model.EndTime;
            shiftType.SpansNextDay = model.SpansNextDay;
            shiftType.GracePeriodMinutes = model.GracePeriodMinutes;
            shiftType.HasLunchBreak = model.HasLunchBreak;
            shiftType.LunchStart = model.HasLunchBreak ? model.LunchStart : null;
            shiftType.LunchEnd = model.HasLunchBreak ? model.LunchEnd : null;
            shiftType.IsBreakPaid = model.HasLunchBreak && model.IsBreakPaid;
            shiftType.ColorHex = model.ColorHex;
            shiftType.SortOrder = model.SortOrder;
            shiftType.IsActive = model.IsActive;
        }

        private static ShiftTypeViewModel MapEntityToViewModel(ShiftType shiftType) => new()
        {
            Id = shiftType.Id,
            Code = shiftType.Code,
            Label = shiftType.Label,
            Hours = shiftType.Hours,
            IsLeave = shiftType.IsLeave,
            IsFlexible = shiftType.IsFlexible,
            StartTime = shiftType.StartTime,
            EndTime = shiftType.EndTime,
            SpansNextDay = shiftType.SpansNextDay,
            GracePeriodMinutes = shiftType.GracePeriodMinutes,
            HasLunchBreak = shiftType.HasLunchBreak,
            IsBreakPaid = shiftType.IsBreakPaid,
            LunchStart = shiftType.LunchStart,
            LunchEnd = shiftType.LunchEnd,
            ColorHex = shiftType.ColorHex,
            SortOrder = shiftType.SortOrder,
            IsActive = shiftType.IsActive
        };

        [Permission("SHIFTTYPE_VIEW")]
        public async Task<ActionResult> Index()
        {
            var shiftTypes = await _db.ShiftTypes
                .AsNoTracking()
                .OrderBy(st => st.SortOrder)
                .ThenBy(st => st.Code)
                .ToListAsync();

            return View(shiftTypes);
        }

        [Permission("SHIFTTYPE_VIEW")]
        public ActionResult CreateShiftType()
        {
            ViewData["FormPrefix"] = "create";
            return PartialView("Partials/_CreateShiftType", new ShiftTypeViewModel
            {
                StartTime = TimeSpan.FromHours(8),
                EndTime = TimeSpan.FromHours(17),
                HasLunchBreak = true,
                LunchStart = TimeSpan.FromHours(12),
                LunchEnd = TimeSpan.FromHours(13),
                Hours = 8
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("SHIFTTYPE_VIEW")]
        public async Task<ActionResult> CreateShiftType(ShiftTypeViewModel model)
        {
            if (!TryValidateSchedule(model, out var scheduleError))
                return Json(new { success = false, message = scheduleError });

            if (!ModelState.IsValid)
                return ValidationErrorResponse();

            try
            {
                if (await _db.ShiftTypes.AnyAsync(st => st.Code == model.Code.Trim()))
                    return Json(new { success = false, message = "Shift type code already exists." });

                var shiftType = new ShiftType();
                MapViewModelToEntity(model, shiftType);

                _db.ShiftTypes.Add(shiftType);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("Failed to save shift type: {Code}", model.Code);
                    return Json(new { success = false, message = "Failed to create shift type." });
                }

                await LogAuditTrailAsync("Create Shift Type", null, shiftType);
                return Json(new { success = true, message = "Shift type created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating shift type: {Code}", model.Code);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }

        [HttpGet]
        [Permission("SHIFTTYPE_VIEW")]
        public async Task<ActionResult> EditShiftType(int id)
        {
            var shiftType = await _db.ShiftTypes.FindAsync(id);
            if (shiftType == null) return NotFound();

            ViewData["FormPrefix"] = "edit";
            return PartialView("Partials/_EditShiftType", MapEntityToViewModel(shiftType));
        }

        [HttpPost]
        [Permission("SHIFTTYPE_VIEW")]
        public async Task<ActionResult> EditShiftType(int id, ShiftTypeViewModel model)
        {
            if (!TryValidateSchedule(model, out var scheduleError))
                return Json(new { success = false, message = scheduleError });

            if (!ModelState.IsValid)
                return ValidationErrorResponse();

            var shiftType = await _db.ShiftTypes.FindAsync(id);
            if (shiftType == null)
                return Json(new { success = false, message = "Shift type not found." });

            if (shiftType.Code != model.Code.Trim() &&
                await _db.ShiftTypes.AnyAsync(st => st.Code == model.Code.Trim() && st.Id != id))
                return Json(new { success = false, message = "Another shift type with the same code already exists." });

            var oldShiftTypeData = JsonSerializer.Deserialize<ShiftType>(JsonSerializer.Serialize(shiftType));

            MapViewModelToEntity(model, shiftType);

            try
            {
                _db.ShiftTypes.Update(shiftType);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                    return Json(new { success = false, message = "No changes were made." });

                await LogAuditTrailAsync("Update Shift Type", oldShiftTypeData, shiftType);
                return Json(new { success = true, message = "Shift type updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Shift Type ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while updating." });
            }
        }

        [HttpGet]
        [Permission("SHIFTTYPE_VIEW")]
        public async Task<ActionResult> ViewShiftType(int id)
        {
            var shiftType = await _db.ShiftTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(st => st.Id == id);

            if (shiftType == null) return NotFound();

            return PartialView("Partials/_ViewShiftType", MapEntityToViewModel(shiftType));
        }

        [HttpGet]
        [Permission("SHIFTTYPE_VIEW")]
        public async Task<ActionResult> DeleteShiftType(int id)
        {
            var shiftType = await _db.ShiftTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(st => st.Id == id);

            if (shiftType == null) return NotFound();

            var isInUse = await _db.ScheduleEntries.AnyAsync(e => e.ShiftTypeId == shiftType.Id);

            if (isInUse)
                return PartialView("Partials/_DeleteWarningShiftType", MapEntityToViewModel(shiftType));

            return PartialView("Partials/_DeleteShiftType", MapEntityToViewModel(shiftType));
        }

        [HttpPost]
        [Permission("SHIFTTYPE_VIEW")]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            var shiftType = await _db.ShiftTypes.FindAsync(id);
            if (shiftType == null)
                return Json(new { success = false, message = "Shift type not found." });

            if (await _db.ScheduleEntries.AnyAsync(e => e.ShiftTypeId == shiftType.Id))
            {
                return Json(new
                {
                    success = false,
                    message = "Cannot delete shift type because it is used on work schedules."
                });
            }

            var oldData = MapEntityToViewModel(shiftType);

            try
            {
                _db.ShiftTypes.Remove(shiftType);
                var result = await _db.SaveChangesAsync();

                if (result > 0)
                {
                    await LogAuditTrailAsync("Delete Shift Type", oldData, null);
                    return Json(new { success = true, message = "Shift type deleted successfully." });
                }

                return Json(new { success = false, message = "Failed to delete shift type." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting shift type ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while deleting." });
            }
        }
    }
}
