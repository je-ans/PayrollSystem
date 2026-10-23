using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Services;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    public class WorkingCalendarController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWorkingCalendarService _calendarService;

        public WorkingCalendarController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IWorkingCalendarService calendarService)
        {
            _db = db;
            _userManager = userManager;
            _calendarService = calendarService;
        }

        [Permission("WORKING_CALENDAR_VIEW")]
        public async Task<IActionResult> Index(int? year = null, int? month = null)
        {
            ViewBag.ActiveMenu = "MasterData";
            ViewBag.ActiveLink = "WorkingCalendar";

            var y = year ?? DateTime.Today.Year;
            var m = month ?? DateTime.Today.Month;
            ViewBag.Year = y;
            ViewBag.Month = m;

            var user = await _userManager.GetUserAsync(User);
            var hasAny = await _db.WorkingCalendars.AnyAsync(c => c.Date.Year == y && !c.IsDeleted);
            if (!hasAny)
            {
                await _calendarService.GenerateYearAsync(y, user?.Id);
            }
            else
            {
                // Keep holiday markers in sync when holidays are added/edited after year generation
                await _calendarService.SyncHolidaysForYearAsync(y, user?.Id);
            }

            var start = new DateOnly(y, m, 1);
            var end = start.AddMonths(1).AddDays(-1);

            var days = await _db.WorkingCalendars
                .AsNoTracking()
                .Include(c => c.DayType)
                .Include(c => c.Holiday)
                .Where(c => c.Date >= start && c.Date <= end && !c.IsDeleted)
                .OrderBy(c => c.Date)
                .Select(c => new WorkingCalendarDayViewModel
                {
                    Date = c.Date,
                    DayTypeCode = c.DayType.Code,
                    DayTypeName = c.DayType.Name,
                    HolidayName = c.Holiday != null ? c.Holiday.Name : null,
                    HolidayType = c.Holiday != null ? (HolidayType?)c.Holiday.HolidayType : null,
                    IsWeekend = c.IsWeekend,
                    IsWorkingDay = c.IsWorkingDay,
                    IsLocked = c.IsLocked,
                    Remarks = c.Remarks
                })
                .ToListAsync();

            var yearHolidays = await _db.Holidays
                .AsNoTracking()
                .Include(h => h.DayType)
                .Where(h => h.IsActive && !h.IsDeleted)
                .Where(h => h.IsRecurring || h.Date.Year == y)
                .OrderBy(h => h.Date.Month)
                .ThenBy(h => h.Date.Day)
                .ThenBy(h => h.Name)
                .Select(h => new HolidayListItemViewModel
                {
                    Id = h.Id,
                    Date = h.Date,
                    Name = h.Name,
                    HolidayType = h.HolidayType,
                    DayTypeName = h.DayType.Name,
                    DayTypeCode = h.DayType.Code,
                    IsRecurring = h.IsRecurring,
                    IsActive = h.IsActive
                })
                .ToListAsync();

            var model = new WorkingCalendarMonthViewModel
            {
                Year = y,
                Month = m,
                MonthName = start.ToString("MMMM yyyy"),
                Days = days,
                YearHolidays = yearHolidays,
                HolidayCount = days.Count(d => d.IsHoliday),
                NonWorkingCount = days.Count(d => !d.IsWorkingDay),
                WorkingCount = days.Count(d => d.IsWorkingDay),
                YearHolidayCount = yearHolidays.Count
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("WORKING_CALENDAR_VIEW")]
        public async Task<IActionResult> Generate(WorkingCalendarGenerateViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Invalid year." });

            var user = await _userManager.GetUserAsync(User);
            var (success, message, _) = await _calendarService.GenerateYearAsync(
                model.Year, user?.Id, model.OverwriteUnlocked);

            return Json(new { success, message });
        }

        [Permission("WORKING_CALENDAR_VIEW")]
        public IActionResult GenerateForm()
        {
            return PartialView("Partials/_GenerateYear", new WorkingCalendarGenerateViewModel
            {
                Year = DateTime.Today.Year,
                OverwriteUnlocked = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("WORKING_CALENDAR_VIEW")]
        public async Task<IActionResult> ToggleLock(DateOnly date)
        {
            var row = await _db.WorkingCalendars.FirstOrDefaultAsync(c => c.Date == date && !c.IsDeleted);
            if (row == null)
                return Json(new { success = false, message = "Calendar day not found. Generate the year first." });

            var user = await _userManager.GetUserAsync(User);
            row.IsLocked = !row.IsLocked;
            row.UpdatedById = user?.Id;
            row.UpdatedOn = DateTime.Now;
            await _db.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = row.IsLocked ? "Day locked." : "Day unlocked.",
                isLocked = row.IsLocked
            });
        }
    }
}
