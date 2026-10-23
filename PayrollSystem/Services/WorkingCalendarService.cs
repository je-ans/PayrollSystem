using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;

namespace PayrollSystem.Services
{
    public interface IWorkingCalendarService
    {
        Task<DayType?> GetDayTypeForDateAsync(DateOnly date, CancellationToken ct = default);
        Task<IReadOnlyDictionary<DateOnly, DayType>> GetDayTypesForRangeAsync(
            DateOnly start,
            DateOnly end,
            CancellationToken ct = default);
        Task<WorkingCalendar?> GetEntryAsync(DateOnly date, CancellationToken ct = default);
        Task<(bool success, string message, int rowsAffected)> GenerateYearAsync(
            int year,
            string? actorUserId = null,
            bool overwriteUnlocked = true,
            CancellationToken ct = default);
        Task RefreshDateAsync(DateOnly date, string? actorUserId = null, CancellationToken ct = default);
        Task RefreshHolidayDatesAsync(Holiday holiday, string? actorUserId = null, CancellationToken ct = default);

        // Re-applies active holidays onto existing calendar rows for the year
        // (skips locked days). Also clears stale holiday markers.
        Task SyncHolidaysForYearAsync(int year, string? actorUserId = null, CancellationToken ct = default);
    }

    public class WorkingCalendarService : IWorkingCalendarService
    {
        private readonly ApplicationDbContext _db;

        public WorkingCalendarService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<DayType?> GetDayTypeForDateAsync(DateOnly date, CancellationToken ct = default)
        {
            var entry = await _db.WorkingCalendars
                .AsNoTracking()
                .Include(c => c.DayType)
                .FirstOrDefaultAsync(c => c.Date == date && !c.IsDeleted, ct);

            if (entry?.DayType != null && entry.DayType.IsActive)
                return entry.DayType;

            // Fallback: resolve without persisted calendar (holiday → weekend → regular)
            return await ResolveDayTypeFallbackAsync(date, ct);
        }

        public async Task<IReadOnlyDictionary<DateOnly, DayType>> GetDayTypesForRangeAsync(
            DateOnly start,
            DateOnly end,
            CancellationToken ct = default)
        {
            if (end < start)
                return new Dictionary<DateOnly, DayType>();

            var calendarRows = await _db.WorkingCalendars
                .AsNoTracking()
                .Include(c => c.DayType)
                .Where(c => c.Date >= start && c.Date <= end && !c.IsDeleted)
                .ToListAsync(ct);

            var result = new Dictionary<DateOnly, DayType>();
            foreach (var row in calendarRows)
            {
                if (row.DayType != null && row.DayType.IsActive && !row.DayType.IsDeleted)
                    result[row.Date] = row.DayType;
            }

            for (var date = start; date <= end; date = date.AddDays(1))
            {
                if (result.ContainsKey(date))
                    continue;

                var fallback = await ResolveDayTypeFallbackAsync(date, ct);
                if (fallback != null)
                    result[date] = fallback;
            }

            return result;
        }

        public Task<WorkingCalendar?> GetEntryAsync(DateOnly date, CancellationToken ct = default) =>
            _db.WorkingCalendars
                .AsNoTracking()
                .Include(c => c.DayType)
                .Include(c => c.Holiday)
                .FirstOrDefaultAsync(c => c.Date == date && !c.IsDeleted, ct);

        public async Task<(bool success, string message, int rowsAffected)> GenerateYearAsync(
            int year,
            string? actorUserId = null,
            bool overwriteUnlocked = true,
            CancellationToken ct = default)
        {
            if (year < 2000 || year > 2100)
                return (false, "Year must be between 2000 and 2100.", 0);

            var dayTypes = await _db.DayTypes
                .AsNoTracking()
                .Where(d => d.IsActive && !d.IsDeleted)
                .ToListAsync(ct);

            var regular = FindDayType(dayTypes, "REG-DAY", "Regular Day");
            var saturday = FindDayType(dayTypes, "SAT", "Saturday") ?? FindDayType(dayTypes, "REST-DAY", "Rest Day");
            var sunday = FindDayType(dayTypes, "SUN", "Sunday") ?? saturday ?? FindDayType(dayTypes, "REST-DAY", "Rest Day");

            if (regular == null)
                return (false, "Active Regular Day (REG-DAY) DayType is required before generating the calendar.", 0);

            var config = await _db.PayrollConfigurations
                .AsNoTracking()
                .Where(c => c.IsActive && !c.IsDeleted)
                .OrderByDescending(c => c.EffectiveDate)
                .FirstOrDefaultAsync(ct);

            var weekendIsDayOff = config?.UseWeekendAsDayOff ?? true;

            var holidays = await _db.Holidays
                .AsNoTracking()
                .Include(h => h.DayType)
                .Where(h => h.IsActive && !h.IsDeleted)
                .Where(h => h.Date.Year == year || h.IsRecurring)
                .ToListAsync(ct);

            var existing = await _db.WorkingCalendars
                .Where(c => c.Date.Year == year && !c.IsDeleted)
                .ToListAsync(ct);
            var byDate = existing.ToDictionary(c => c.Date);

            var start = new DateOnly(year, 1, 1);
            var end = new DateOnly(year, 12, 31);
            var now = DateTime.Now;
            var affected = 0;

            for (var date = start; date <= end; date = date.AddDays(1))
            {
                var resolved = ResolveForDate(date, holidays, regular, saturday, sunday, weekendIsDayOff);

                if (byDate.TryGetValue(date, out var row))
                {
                    if (row.IsLocked)
                        continue;

                    if (!overwriteUnlocked &&
                        row.DayTypeId == resolved.DayTypeId &&
                        row.HolidayId == resolved.HolidayId &&
                        row.IsWorkingDay == resolved.IsWorkingDay &&
                        row.IsWeekend == resolved.IsWeekend)
                        continue;

                    row.DayTypeId = resolved.DayTypeId;
                    row.HolidayId = resolved.HolidayId;
                    row.IsWeekend = resolved.IsWeekend;
                    row.IsWorkingDay = resolved.IsWorkingDay;
                    row.Remarks = resolved.Remarks;
                    row.UpdatedById = actorUserId;
                    row.UpdatedOn = now;
                    affected++;
                }
                else
                {
                    _db.WorkingCalendars.Add(new WorkingCalendar
                    {
                        Date = date,
                        DayTypeId = resolved.DayTypeId,
                        HolidayId = resolved.HolidayId,
                        IsWeekend = resolved.IsWeekend,
                        IsWorkingDay = resolved.IsWorkingDay,
                        Remarks = resolved.Remarks,
                        CreatedById = actorUserId,
                        CreatedOn = now
                    });
                    affected++;
                }
            }

            await _db.SaveChangesAsync(ct);
            return (true, $"Working calendar generated for {year} ({affected} day(s) updated).", affected);
        }

        public async Task RefreshDateAsync(DateOnly date, string? actorUserId = null, CancellationToken ct = default)
        {
            var dayTypes = await _db.DayTypes
                .AsNoTracking()
                .Where(d => d.IsActive && !d.IsDeleted)
                .ToListAsync(ct);

            var regular = FindDayType(dayTypes, "REG-DAY", "Regular Day");
            if (regular == null)
                return;

            var saturday = FindDayType(dayTypes, "SAT", "Saturday") ?? FindDayType(dayTypes, "REST-DAY", "Rest Day");
            var sunday = FindDayType(dayTypes, "SUN", "Sunday") ?? saturday ?? FindDayType(dayTypes, "REST-DAY", "Rest Day");

            var config = await _db.PayrollConfigurations
                .AsNoTracking()
                .Where(c => c.IsActive && !c.IsDeleted)
                .OrderByDescending(c => c.EffectiveDate)
                .FirstOrDefaultAsync(ct);

            var holidays = await _db.Holidays
                .AsNoTracking()
                .Where(h => h.IsActive && !h.IsDeleted)
                .Where(h => h.Date == date || (h.IsRecurring && h.Date.Month == date.Month && h.Date.Day == date.Day))
                .ToListAsync(ct);

            var resolved = ResolveForDate(
                date, holidays, regular, saturday, sunday, config?.UseWeekendAsDayOff ?? true);

            var row = await _db.WorkingCalendars
                .FirstOrDefaultAsync(c => c.Date == date && !c.IsDeleted, ct);

            var now = DateTime.Now;
            if (row == null)
            {
                _db.WorkingCalendars.Add(new WorkingCalendar
                {
                    Date = date,
                    DayTypeId = resolved.DayTypeId,
                    HolidayId = resolved.HolidayId,
                    IsWeekend = resolved.IsWeekend,
                    IsWorkingDay = resolved.IsWorkingDay,
                    Remarks = resolved.Remarks,
                    CreatedById = actorUserId,
                    CreatedOn = now
                });
            }
            else if (!row.IsLocked)
            {
                row.DayTypeId = resolved.DayTypeId;
                row.HolidayId = resolved.HolidayId;
                row.IsWeekend = resolved.IsWeekend;
                row.IsWorkingDay = resolved.IsWorkingDay;
                row.Remarks = resolved.Remarks;
                row.UpdatedById = actorUserId;
                row.UpdatedOn = now;
            }

            await _db.SaveChangesAsync(ct);
        }

        public async Task RefreshHolidayDatesAsync(Holiday holiday, string? actorUserId = null, CancellationToken ct = default)
        {
            if (holiday.IsRecurring)
            {
                var years = await _db.WorkingCalendars
                    .AsNoTracking()
                    .Where(c => !c.IsDeleted)
                    .Select(c => c.Date.Year)
                    .Distinct()
                    .ToListAsync(ct);

                if (years.Count == 0)
                    years.Add(DateTime.Today.Year);

                foreach (var year in years)
                {
                    var date = SafeDate(year, holiday.Date.Month, holiday.Date.Day);
                    if (date.HasValue)
                        await RefreshDateAsync(date.Value, actorUserId, ct);
                }
            }
            else
            {
                await RefreshDateAsync(holiday.Date, actorUserId, ct);
            }
        }

        public async Task SyncHolidaysForYearAsync(int year, string? actorUserId = null, CancellationToken ct = default)
        {
            var holidays = await _db.Holidays
                .AsNoTracking()
                .Where(h => h.IsActive && !h.IsDeleted)
                .Where(h => h.Date.Year == year || h.IsRecurring)
                .ToListAsync(ct);

            var dates = new HashSet<DateOnly>();
            foreach (var holiday in holidays)
            {
                if (holiday.IsRecurring)
                {
                    var d = SafeDate(year, holiday.Date.Month, holiday.Date.Day);
                    if (d.HasValue) dates.Add(d.Value);
                }
                else if (holiday.Date.Year == year)
                {
                    dates.Add(holiday.Date);
                }
            }

            // Also clear stale holiday markers on dates that are no longer holidays
            var calendarHolidayDates = await _db.WorkingCalendars
                .AsNoTracking()
                .Where(c => !c.IsDeleted && c.Date.Year == year && c.HolidayId != null)
                .Select(c => c.Date)
                .ToListAsync(ct);

            foreach (var d in calendarHolidayDates)
                dates.Add(d);

            foreach (var date in dates.OrderBy(d => d))
                await RefreshDateAsync(date, actorUserId, ct);
        }

        private async Task<DayType?> ResolveDayTypeFallbackAsync(DateOnly date, CancellationToken ct)
        {
            var holidays = await _db.Holidays
                .AsNoTracking()
                .Include(h => h.DayType)
                .Where(h => h.IsActive && !h.IsDeleted)
                .Where(h => h.Date == date || (h.IsRecurring && h.Date.Month == date.Month && h.Date.Day == date.Day))
                .ToListAsync(ct);

            var holiday = holidays
                .OrderByDescending(h => !h.IsRecurring)
                .ThenByDescending(h => h.Date)
                .FirstOrDefault(h => h.DayType != null && h.DayType.IsActive);

            if (holiday?.DayType != null)
                return holiday.DayType;

            var dayTypes = await _db.DayTypes
                .AsNoTracking()
                .Where(d => d.IsActive && !d.IsDeleted)
                .ToListAsync(ct);

            var config = await _db.PayrollConfigurations
                .AsNoTracking()
                .Where(c => c.IsActive && !c.IsDeleted)
                .OrderByDescending(c => c.EffectiveDate)
                .FirstOrDefaultAsync(ct);

            if (config?.UseWeekendAsDayOff == true)
            {
                if (date.DayOfWeek == DayOfWeek.Saturday)
                    return FindDayType(dayTypes, "SAT", "Saturday")
                           ?? FindDayType(dayTypes, "REST-DAY", "Rest Day")
                           ?? FindDayType(dayTypes, "REG-DAY", "Regular Day");

                if (date.DayOfWeek == DayOfWeek.Sunday)
                    return FindDayType(dayTypes, "SUN", "Sunday")
                           ?? FindDayType(dayTypes, "REST-DAY", "Rest Day")
                           ?? FindDayType(dayTypes, "REG-DAY", "Regular Day");
            }

            return FindDayType(dayTypes, "REG-DAY", "Regular Day");
        }

        private static (int DayTypeId, int? HolidayId, bool IsWeekend, bool IsWorkingDay, string? Remarks)
            ResolveForDate(
                DateOnly date,
                List<Holiday> holidays,
                DayType regular,
                DayType? saturday,
                DayType? sunday,
                bool weekendIsDayOff)
        {
            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            var holiday = holidays
                .Where(h => h.Date == date || (h.IsRecurring && h.Date.Month == date.Month && h.Date.Day == date.Day))
                .OrderByDescending(h => !h.IsRecurring)
                .ThenByDescending(h => h.Date)
                .FirstOrDefault();

            if (holiday != null)
            {
                return (
                    holiday.DayTypeId,
                    holiday.Id,
                    isWeekend,
                    IsWorkingDay: false,
                    Remarks: holiday.Name);
            }

            if (weekendIsDayOff && date.DayOfWeek == DayOfWeek.Saturday && saturday != null)
                return (saturday.Id, null, true, false, "Saturday");

            if (weekendIsDayOff && date.DayOfWeek == DayOfWeek.Sunday && sunday != null)
                return (sunday.Id, null, true, false, "Sunday");

            return (regular.Id, null, isWeekend, true, null);
        }

        private static DayType? FindDayType(IEnumerable<DayType> dayTypes, string code, string name) =>
            dayTypes.FirstOrDefault(d =>
                string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

        private static DateOnly? SafeDate(int year, int month, int day)
        {
            try
            {
                if (month == 2 && day == 29 && !DateTime.IsLeapYear(year))
                    return null;
                return new DateOnly(year, month, day);
            }
            catch
            {
                return null;
            }
        }
    }
}
