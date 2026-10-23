using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Services;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    public class HolidayController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWorkingCalendarService _calendarService;
        private readonly ILogger<HolidayController> _logger;

        public HolidayController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IWorkingCalendarService calendarService,
            ILogger<HolidayController> logger)
        {
            _db = db;
            _userManager = userManager;
            _calendarService = calendarService;
            _logger = logger;
        }

        private JsonResult ValidationErrorResponse()
        {
            var errorMessages = ModelStateHelper.GetModelErrors(ModelState);
            return Json(new { success = false, message = string.Join("; ", errorMessages) });
        }

        private static HolidayFormViewModel MapEntityToViewModel(Holiday holiday, List<SelectListItem>? dayTypeOptions = null) => new()
        {
            Id = holiday.Id,
            Date = holiday.Date,
            Name = holiday.Name,
            HolidayType = holiday.HolidayType,
            DayTypeId = holiday.DayTypeId,
            DayTypeName = holiday.DayType?.Name,
            DayTypeCode = holiday.DayType?.Code,
            IsRecurring = holiday.IsRecurring,
            IsActive = holiday.IsActive,
            Description = holiday.Description,
            DayTypeOptions = dayTypeOptions ?? new List<SelectListItem>()
        };

        private static void MapViewModelToEntity(HolidayFormViewModel model, Holiday holiday)
        {
            holiday.Date = model.Date;
            holiday.Name = model.Name.Trim();
            holiday.HolidayType = model.HolidayType;
            holiday.DayTypeId = model.DayTypeId;
            holiday.IsRecurring = model.IsRecurring;
            holiday.IsActive = model.IsActive;
            holiday.Description = model.Description?.Trim();
        }

        [Permission("HOLIDAY_VIEW")]
        public async Task<IActionResult> Index(int? year = null)
        {
            ViewBag.ActiveMenu = "MasterData";
            ViewBag.ActiveLink = "Holiday";

            var filterYear = year ?? DateTime.Today.Year;
            ViewBag.Year = filterYear;

            var items = await _db.Holidays
                .AsNoTracking()
                .Include(h => h.DayType)
                .Where(h => !h.IsDeleted)
                .Where(h => h.IsRecurring || h.Date.Year == filterYear)
                .OrderBy(h => h.Date.Month)
                .ThenBy(h => h.Date.Day)
                .ThenBy(h => h.Name)
                .ToListAsync();

            return View(items);
        }

        [Permission("HOLIDAY_VIEW")]
        public async Task<IActionResult> CreateHoliday()
        {
            var vm = new HolidayFormViewModel
            {
                Date = DateOnly.FromDateTime(DateTime.Today),
                IsActive = true,
                DayTypeOptions = await LoadDayTypeOptionsAsync()
            };

            PreferRegHolDefault(vm);
            return PartialView("Partials/_CreateHoliday", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("HOLIDAY_VIEW")]
        public async Task<IActionResult> CreateHoliday(HolidayFormViewModel model)
        {
            if (!ModelState.IsValid)
                return ValidationErrorResponse();

            try
            {
                var user = await _userManager.GetUserAsync(User);
                var holiday = new Holiday
                {
                    CreatedById = user?.Id,
                    CreatedOn = DateTime.Now
                };
                MapViewModelToEntity(model, holiday);

                _db.Holidays.Add(holiday);
                var result = await _db.SaveChangesAsync();
                if (result <= 0)
                    return Json(new { success = false, message = "Failed to create holiday." });

                await _calendarService.RefreshHolidayDatesAsync(holiday, user?.Id);
                return Json(new { success = true, message = "Holiday created successfully. Working calendar updated." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating holiday: {Name}", model.Name);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }

        [HttpGet]
        [Permission("HOLIDAY_VIEW")]
        public async Task<IActionResult> EditHoliday(int id)
        {
            var holiday = await _db.Holidays
                .Include(h => h.DayType)
                .FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);
            if (holiday == null) return NotFound();

            var vm = MapEntityToViewModel(holiday, await LoadDayTypeOptionsAsync());
            return PartialView("Partials/_EditHoliday", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("HOLIDAY_VIEW")]
        public async Task<IActionResult> EditHoliday(int id, HolidayFormViewModel model)
        {
            if (!ModelState.IsValid)
                return ValidationErrorResponse();

            var holiday = await _db.Holidays.FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);
            if (holiday == null)
                return Json(new { success = false, message = "Holiday not found." });

            try
            {
                var user = await _userManager.GetUserAsync(User);
                var previousDate = holiday.Date;
                var wasRecurring = holiday.IsRecurring;

                MapViewModelToEntity(model, holiday);
                holiday.UpdatedById = user?.Id;
                holiday.UpdatedOn = DateTime.Now;

                _db.Holidays.Update(holiday);
                var result = await _db.SaveChangesAsync();
                if (result <= 0)
                    return Json(new { success = false, message = "No changes were made." });

                if (previousDate != model.Date || wasRecurring != model.IsRecurring)
                    await _calendarService.RefreshDateAsync(previousDate, user?.Id);

                await _calendarService.RefreshHolidayDatesAsync(holiday, user?.Id);
                return Json(new { success = true, message = "Holiday updated successfully. Working calendar refreshed." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating holiday ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while updating." });
            }
        }

        [HttpGet]
        [Permission("HOLIDAY_VIEW")]
        public async Task<IActionResult> ViewHoliday(int id)
        {
            var holiday = await _db.Holidays
                .AsNoTracking()
                .Include(h => h.DayType)
                .FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);

            if (holiday == null) return NotFound();

            return PartialView("Partials/_ViewHoliday", MapEntityToViewModel(holiday));
        }

        [HttpGet]
        [Permission("HOLIDAY_VIEW")]
        public async Task<IActionResult> DeleteHoliday(int id)
        {
            var holiday = await _db.Holidays
                .AsNoTracking()
                .Include(h => h.DayType)
                .FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);

            if (holiday == null) return NotFound();

            return PartialView("Partials/_DeleteHoliday", MapEntityToViewModel(holiday));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("HOLIDAY_VIEW")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (id <= 0 && int.TryParse(Request.Form["Id"], out var formId))
                id = formId;
            var holiday = await _db.Holidays.FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);
            if (holiday == null)
                return Json(new { success = false, message = "Holiday not found or already deleted." });

            try
            {
                var user = await _userManager.GetUserAsync(User);
                holiday.IsDeleted = true;
                holiday.IsActive = false;
                holiday.UpdatedById = user?.Id;
                holiday.UpdatedOn = DateTime.Now;

                _db.Holidays.Update(holiday);
                var result = await _db.SaveChangesAsync();
                if (result <= 0)
                    return Json(new { success = false, message = "Failed to delete holiday." });

                var linked = await _db.WorkingCalendars
                    .Where(c => c.HolidayId == id && !c.IsDeleted)
                    .ToListAsync();

                foreach (var row in linked)
                {
                    row.HolidayId = null;
                    if (!row.IsLocked)
                        await _calendarService.RefreshDateAsync(row.Date, user?.Id);
                }

                if (linked.Count == 0)
                    await _calendarService.RefreshHolidayDatesAsync(holiday, user?.Id);

                return Json(new { success = true, message = "Holiday deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting holiday ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while deleting." });
            }
        }

        private static void PreferRegHolDefault(HolidayFormViewModel vm)
        {
            var regHol = vm.DayTypeOptions.FirstOrDefault(o =>
                string.Equals(o.Text?.Split('—').FirstOrDefault()?.Trim(), "REG-HOL", StringComparison.OrdinalIgnoreCase) ||
                (o.Text?.Contains("Regular Holiday", StringComparison.OrdinalIgnoreCase) ?? false));
            if (regHol != null && int.TryParse(regHol.Value, out var dayTypeId))
                vm.DayTypeId = dayTypeId;
        }

        private async Task<List<SelectListItem>> LoadDayTypeOptionsAsync()
        {
            return await _db.DayTypes
                .AsNoTracking()
                .Where(d => d.IsActive && !d.IsDeleted)
                .OrderBy(d => d.Name)
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = $"{d.Code} — {d.Name}"
                })
                .ToListAsync();
        }
    }
}
