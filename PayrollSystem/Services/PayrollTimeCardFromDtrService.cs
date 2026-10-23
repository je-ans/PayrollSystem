using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.Services
{
    public class EmployeeTimeCardFromDtrResult
    {
        public Guid EmployeeId { get; set; }
        public int LateMinutes { get; set; }
        public int LateIncidents { get; set; }
        public int UndertimeMinutes { get; set; }

        /// <summary>Worked / Basic hours keyed by payroll DayType id (schedule-aware, not weekday-only).</summary>
        public Dictionary<int, decimal> HoursByDayTypeId { get; set; } = new();

        /// <summary>Night differential hours keyed by payroll DayType id.</summary>
        public Dictionary<int, decimal> NightDifferentialHoursByDayTypeId { get; set; } = new();

        /// <summary>Legacy buckets kept for callers that still map Regular/Sat/Sun by weekday name.</summary>
        public decimal RegularHours { get; set; }
        public decimal SaturdayHours { get; set; }
        public decimal SundayHours { get; set; }
        public decimal NightDifferentialHours { get; set; }
        public decimal PotentialOvertimeHours { get; set; }
        public decimal AbsentHours { get; set; }
        public int PresentDays { get; set; }
        public int AbsentDays { get; set; }
        public int LeaveDays { get; set; }
        public string? Notes { get; set; }
    }

    public class DailyAttendanceTrendPoint
    {
        public DateOnly Date { get; set; }
        public int Present { get; set; }
        public int Late { get; set; }
        public int Absent { get; set; }
        public int Leave { get; set; }
    }

    public interface IPayrollTimeCardFromDtrService
    {
        Task<EmployeeTimeCardFromDtrResult> BuildForEmployeeAsync(
            Guid employeeId,
            DateOnly periodStart,
            DateOnly periodEnd);
        Task<IReadOnlyList<EmployeeTimeCardFromDtrResult>> BuildForEmployeesAsync(
            IEnumerable<Guid> employeeIds,
            DateOnly periodStart,
            DateOnly periodEnd);

        /// <summary>
        /// Daily attendance counts across employees for dashboard charts (Present / Late / Absent / Leave).
        /// </summary>
        Task<IReadOnlyList<DailyAttendanceTrendPoint>> BuildDailyAttendanceTrendAsync(
            IEnumerable<Guid> employeeIds,
            DateOnly periodStart,
            DateOnly periodEnd);

        /// <summary>
        /// Undertime minutes computed from DTR for one employee on one calendar date.
        /// </summary>
        Task<int> GetUndertimeMinutesForDateAsync(Guid employeeId, DateOnly date);
    }

    public class PayrollTimeCardFromDtrService : IPayrollTimeCardFromDtrService
    {
        private static readonly TimeOnly Noon = new(12, 0);

        private readonly ApplicationDbContext _db;
        private readonly DtrAttendanceCalculationService _attendance;
        private readonly IAttendanceComputationService _attendanceComputation;
        private readonly IWorkingCalendarService _workingCalendar;
        private readonly IScheduleChangeRequestService _scheduleChange;

        public PayrollTimeCardFromDtrService(
            ApplicationDbContext db,
            DtrAttendanceCalculationService attendance,
            IAttendanceComputationService attendanceComputation,
            IWorkingCalendarService workingCalendar,
            IScheduleChangeRequestService scheduleChange)
        {
            _db = db;
            _attendance = attendance;
            _attendanceComputation = attendanceComputation;
            _workingCalendar = workingCalendar;
            _scheduleChange = scheduleChange;
        }

        public async Task<IReadOnlyList<EmployeeTimeCardFromDtrResult>> BuildForEmployeesAsync(
            IEnumerable<Guid> employeeIds,
            DateOnly periodStart,
            DateOnly periodEnd)
        {
            var results = new List<EmployeeTimeCardFromDtrResult>();
            foreach (var employeeId in employeeIds.Distinct())
            {
                results.Add(await BuildForEmployeeAsync(employeeId, periodStart, periodEnd));
            }

            return results;
        }

        public async Task<IReadOnlyList<DailyAttendanceTrendPoint>> BuildDailyAttendanceTrendAsync(
            IEnumerable<Guid> employeeIds,
            DateOnly periodStart,
            DateOnly periodEnd)
        {
            var ids = employeeIds.Distinct().ToList();
            if (ids.Count == 0 || periodEnd < periodStart)
                return Array.Empty<DailyAttendanceTrendPoint>();

            var byDate = new Dictionary<DateOnly, DailyAttendanceTrendPoint>();
            for (var d = periodStart; d <= periodEnd; d = d.AddDays(1))
                byDate[d] = new DailyAttendanceTrendPoint { Date = d };

            foreach (var employeeId in ids)
                await AccumulateDailyFlagsAsync(employeeId, periodStart, periodEnd, byDate);

            return byDate.Values.OrderBy(p => p.Date).ToList();
        }

        /// <summary>
        /// Classifies each day for one employee into Present / Late / Absent / Leave counters.
        /// Shares the same schedule + punch resolution rules as time-card build, without hour credit.
        /// </summary>
        private async Task AccumulateDailyFlagsAsync(
            Guid employeeId,
            DateOnly periodStart,
            DateOnly periodEnd,
            Dictionary<DateOnly, DailyAttendanceTrendPoint> byDate)
        {
            var config = await _db.PayrollConfigurations
                .AsNoTracking()
                .Where(c => c.IsActive && !c.IsDeleted)
                .OrderByDescending(c => c.EffectiveDate)
                .FirstOrDefaultAsync();

            var allDayTypes = await _db.DayTypes
                .AsNoTracking()
                .Where(d => d.IsActive && !d.IsDeleted)
                .ToListAsync();

            var dayTypesByDate = await _workingCalendar.GetDayTypesForRangeAsync(periodStart, periodEnd);
            var holidayDates = await _db.WorkingCalendars
                .AsNoTracking()
                .Where(c => c.Date >= periodStart && c.Date <= periodEnd && !c.IsDeleted && c.HolidayId != null)
                .Select(c => c.Date)
                .ToListAsync();
            var holidayDateSet = holidayDates.ToHashSet();

            var entryStart = periodStart.AddDays(-1);
            var entryEnd = periodEnd.AddDays(1);
            var timeEntries = await _db.TimeEntries
                .AsNoTracking()
                .Where(te => te.EmployeeId == employeeId &&
                             te.EntryDate >= entryStart &&
                             te.EntryDate <= entryEnd)
                .ToListAsync();
            var entriesByDate = timeEntries
                .GroupBy(te => te.EntryDate)
                .ToDictionary(g => g.Key, g => g.ToList());

            var shiftsByDate = await LoadShiftsForRangeAsync(
                employeeId, periodStart.AddDays(-1), periodEnd);

            for (var date = periodStart; date <= periodEnd; date = date.AddDays(1))
            {
                if (!byDate.TryGetValue(date, out var point))
                    continue;

                shiftsByDate.TryGetValue(date, out var shift);
                var isScheduledLeave = shift != null &&
                                       !ShiftTypeScheduleHelper.IsUnassigned(shift) &&
                                       shift.IsLeave;

                if (isScheduledLeave)
                {
                    point.Leave++;
                    continue;
                }

                entriesByDate.TryGetValue(date, out var entries);
                entries ??= new List<TimeEntry>();
                var displayEntries = GetDisplayEntries(entries);
                entriesByDate.TryGetValue(date.AddDays(1), out var nextDayEntries);
                nextDayEntries ??= new List<TimeEntry>();
                var nextDayDisplay = GetDisplayEntries(nextDayEntries);
                entriesByDate.TryGetValue(date.AddDays(-1), out var prevDayEntries);
                prevDayEntries ??= new List<TimeEntry>();
                var prevDayDisplay = GetDisplayEntries(prevDayEntries);

                shiftsByDate.TryGetValue(date.AddDays(-1), out var prevShift);
                var scheduleContext = _attendance.ResolveSchedule(config, shift);
                var prevScheduleContext = _attendance.ResolveSchedule(config, prevShift);
                var currentIsOvernight = DtrPunchResolutionService.IsOvernightSchedule(scheduleContext);
                var previousWasOvernight = DtrPunchResolutionService.IsOvernightSchedule(prevScheduleContext);
                var shiftResolved = DtrPunchResolutionService.ResolveShift(
                    scheduleContext, displayEntries, nextDayDisplay);
                var previousShiftResolved = DtrPunchResolutionService.ResolveShift(
                    prevScheduleContext, prevDayDisplay, displayEntries);

                dayTypesByDate.TryGetValue(date, out var calendarDayType);
                var dayType = DayTypeHelper.ResolveDayType(
                    shift,
                    calendarDayType,
                    isHoliday: holidayDateSet.Contains(date) || DayTypeHelper.IsHolidayDayType(calendarDayType),
                    allDayTypes);
                var assessAttendance = ShouldAssessAttendance(config, date, shift, scheduleContext);

                var attendance = _attendanceComputation.Compute(new AttendanceComputationInput
                {
                    Date = date,
                    Config = config,
                    Shift = shift,
                    PreviousShift = prevShift,
                    Schedule = scheduleContext,
                    PreviousSchedule = prevScheduleContext,
                    ShiftResolved = shiftResolved,
                    PreviousShiftResolved = previousShiftResolved,
                    CurrentIsOvernight = currentIsOvernight,
                    PreviousWasOvernight = previousWasOvernight,
                    DayType = dayType,
                    AssessAttendance = assessAttendance
                });

                if (shift?.IsFlexible == true)
                {
                    if (!assessAttendance) continue;
                    var hasFlexLog = displayEntries.Any();
                    if (!hasFlexLog) { point.Absent++; continue; }
                    point.Present++;
                    if (attendance.TardinessMinutes > 0) point.Late++;
                    continue;
                }

                var hasAnyLog = displayEntries.Any() || nextDayDisplay.Any();
                if (!hasAnyLog)
                {
                    if (assessAttendance) point.Absent++;
                    continue;
                }

                if (ShouldCreditWorkedDay(scheduleContext, currentIsOvernight, shiftResolved))
                {
                    point.Present++;
                    if (attendance.TardinessMinutes > 0) point.Late++;
                }
                else if (assessAttendance && hasAnyLog && !shiftResolved.HasCompletePair)
                {
                    point.Absent++;
                }
            }
        }

        public async Task<EmployeeTimeCardFromDtrResult> BuildForEmployeeAsync(
            Guid employeeId,
            DateOnly periodStart,
            DateOnly periodEnd)
        {
            await SyncScheduleChangesForRangeAsync(employeeId, periodStart, periodEnd);

            var config = await _attendance.GetActiveConfigurationAsync();
            var dayTypesByDate = await _workingCalendar.GetDayTypesForRangeAsync(periodStart, periodEnd);
            var allDayTypes = await _db.DayTypes
                .AsNoTracking()
                .Where(d => d.IsActive && !d.IsDeleted)
                .ToListAsync();
            var holidayDates = await _db.WorkingCalendars
                .AsNoTracking()
                .Where(c => c.Date >= periodStart &&
                            c.Date <= periodEnd &&
                            !c.IsDeleted &&
                            c.HolidayId != null)
                .Select(c => c.Date)
                .ToListAsync();
            var holidayDateSet = holidayDates.ToHashSet();

            // Include adjacent days for overnight shift ends/starts that cross the period boundary.
            var entryStart = periodStart.AddDays(-1);
            var entryEnd = periodEnd.AddDays(1);

            var timeEntries = await _db.TimeEntries
                .AsNoTracking()
                .Where(te => te.EmployeeId == employeeId &&
                             te.EntryDate >= entryStart &&
                             te.EntryDate <= entryEnd)
                .ToListAsync();

            var entriesByDate = timeEntries
                .GroupBy(te => te.EntryDate)
                .ToDictionary(g => g.Key, g => g.ToList());

            var shiftsByDate = await LoadShiftsForRangeAsync(
                employeeId,
                periodStart.AddDays(-1),
                periodEnd);

            var result = new EmployeeTimeCardFromDtrResult { EmployeeId = employeeId };

            for (var date = periodStart; date <= periodEnd; date = date.AddDays(1))
            {
                shiftsByDate.TryGetValue(date, out var shift);
                var isScheduledLeave = shift != null &&
                                       !ShiftTypeScheduleHelper.IsUnassigned(shift) &&
                                       shift.IsLeave;

                entriesByDate.TryGetValue(date, out var entries);
                entries ??= new List<TimeEntry>();
                var displayEntries = GetDisplayEntries(entries);

                entriesByDate.TryGetValue(date.AddDays(1), out var nextDayEntries);
                nextDayEntries ??= new List<TimeEntry>();
                var nextDayDisplay = GetDisplayEntries(nextDayEntries);

                entriesByDate.TryGetValue(date.AddDays(-1), out var prevDayEntries);
                prevDayEntries ??= new List<TimeEntry>();
                var prevDayDisplay = GetDisplayEntries(prevDayEntries);

                shiftsByDate.TryGetValue(date.AddDays(-1), out var prevShift);
                var scheduleContext = _attendance.ResolveSchedule(config, shift);
                var prevScheduleContext = _attendance.ResolveSchedule(config, prevShift);
                var currentIsOvernight = DtrPunchResolutionService.IsOvernightSchedule(scheduleContext);
                var previousWasOvernight = DtrPunchResolutionService.IsOvernightSchedule(prevScheduleContext);

                var shiftResolved = DtrPunchResolutionService.ResolveShift(
                    scheduleContext, displayEntries, nextDayDisplay);
                var previousShiftResolved = DtrPunchResolutionService.ResolveShift(
                    prevScheduleContext, prevDayDisplay, displayEntries);

                dayTypesByDate.TryGetValue(date, out var calendarDayType);
                var dayType = DayTypeHelper.ResolveDayType(
                    shift,
                    calendarDayType,
                    isHoliday: holidayDateSet.Contains(date) || DayTypeHelper.IsHolidayDayType(calendarDayType),
                    allDayTypes);
                var assessAttendance = ShouldAssessAttendance(config, date, shift, scheduleContext);

                var attendance = _attendanceComputation.Compute(new AttendanceComputationInput
                {
                    Date = date,
                    Config = config,
                    Shift = shift,
                    PreviousShift = prevShift,
                    Schedule = scheduleContext,
                    PreviousSchedule = prevScheduleContext,
                    ShiftResolved = shiftResolved,
                    PreviousShiftResolved = previousShiftResolved,
                    CurrentIsOvernight = currentIsOvernight,
                    PreviousWasOvernight = previousWasOvernight,
                    DayType = dayType,
                    AssessAttendance = assessAttendance
                });

                AddTardiness(result, attendance.TardinessMinutes);
                result.UndertimeMinutes += attendance.UndertimeMinutes;

                if (isScheduledLeave)
                {
                    result.LeaveDays++;
                    if (ShiftTypeScheduleHelper.CountsTowardDtrTotal(shift))
                    {
                        CreditHours(result, dayType, shift!.Hours);
                    }

                    continue;
                }

                if (shift?.IsFlexible == true)
                {
                    ProcessFlexibleDay(result, config, date, displayEntries, shift, scheduleContext, dayType);
                    continue;
                }

                var hasAnyLog = displayEntries.Any() || nextDayDisplay.Any();

                if (!hasAnyLog)
                {
                    if (assessAttendance)
                    {
                        result.AbsentDays++;
                        result.AbsentHours += GetAbsentCreditHours(shift);
                    }

                    continue;
                }

                if (ShouldCreditWorkedDay(scheduleContext, currentIsOvernight, shiftResolved))
                {
                    // Basic Hours = attendance WorkedHours (full scheduled shift), never reduced by undertime.
                    var hours = attendance.WorkedHours > 0
                        ? attendance.WorkedHours
                        : (shift?.Hours > 0 ? shift.Hours : 8m);
                    CreditHours(result, dayType, hours);
                    if (attendance.NightDifferentialHours > 0)
                        CreditNightDifferential(result, dayType, attendance.NightDifferentialHours);
                    result.PotentialOvertimeHours += attendance.PotentialOvertimeHours;
                    result.PresentDays++;
                }
                else if (assessAttendance && hasAnyLog && !shiftResolved.HasCompletePair)
                {
                    result.AbsentDays++;
                    result.AbsentHours += GetAbsentCreditHours(shift);
                }
            }

            result.Notes = BuildNotes(result);
            return result;
        }

        public async Task<int> GetUndertimeMinutesForDateAsync(Guid employeeId, DateOnly date)
        {
            await SyncScheduleChangesForRangeAsync(employeeId, date, date);

            var config = await _attendance.GetActiveConfigurationAsync();
            var dayTypesByDate = await _workingCalendar.GetDayTypesForRangeAsync(date, date);
            var allDayTypes = await _db.DayTypes
                .AsNoTracking()
                .Where(d => d.IsActive && !d.IsDeleted)
                .ToListAsync();
            var isHoliday = await _db.WorkingCalendars
                .AsNoTracking()
                .AnyAsync(c => c.Date == date && !c.IsDeleted && c.HolidayId != null);
            var holidayDateSet = isHoliday ? new HashSet<DateOnly> { date } : new HashSet<DateOnly>();

            var entryStart = date.AddDays(-1);
            var entryEnd = date.AddDays(1);

            var timeEntries = await _db.TimeEntries
                .AsNoTracking()
                .Where(te => te.EmployeeId == employeeId &&
                             te.EntryDate >= entryStart &&
                             te.EntryDate <= entryEnd)
                .ToListAsync();

            var entriesByDate = timeEntries
                .GroupBy(te => te.EntryDate)
                .ToDictionary(g => g.Key, g => g.ToList());

            var shiftsByDate = await LoadShiftsForRangeAsync(employeeId, date.AddDays(-1), date);

            shiftsByDate.TryGetValue(date, out var shift);
            entriesByDate.TryGetValue(date, out var entries);
            entries ??= new List<TimeEntry>();
            var displayEntries = GetDisplayEntries(entries);

            entriesByDate.TryGetValue(date.AddDays(1), out var nextDayEntries);
            nextDayEntries ??= new List<TimeEntry>();
            var nextDayDisplay = GetDisplayEntries(nextDayEntries);

            entriesByDate.TryGetValue(date.AddDays(-1), out var prevDayEntries);
            prevDayEntries ??= new List<TimeEntry>();
            var prevDayDisplay = GetDisplayEntries(prevDayEntries);

            shiftsByDate.TryGetValue(date.AddDays(-1), out var prevShift);
            var scheduleContext = _attendance.ResolveSchedule(config, shift);
            var prevScheduleContext = _attendance.ResolveSchedule(config, prevShift);
            var currentIsOvernight = DtrPunchResolutionService.IsOvernightSchedule(scheduleContext);
            var previousWasOvernight = DtrPunchResolutionService.IsOvernightSchedule(prevScheduleContext);

            dayTypesByDate.TryGetValue(date, out var calendarDayType);
            var dayType = DayTypeHelper.ResolveDayType(
                shift,
                calendarDayType,
                isHoliday: holidayDateSet.Contains(date) || DayTypeHelper.IsHolidayDayType(calendarDayType),
                allDayTypes);
            var assessAttendance = ShouldAssessAttendance(config, date, shift, scheduleContext);

            if (shift?.IsFlexible == true)
            {
                var timeIn = displayEntries
                    .Where(e => e.EntryType == EntryType.TimeIn)
                    .OrderBy(e => e.EntryTime)
                    .Select(e => (TimeOnly?)e.EntryTime)
                    .FirstOrDefault();
                var lunchOut = displayEntries
                    .Where(e => e.EntryType is EntryType.LunchStart or EntryType.BreakStart)
                    .OrderBy(e => e.EntryTime)
                    .Select(e => (TimeOnly?)e.EntryTime)
                    .FirstOrDefault();
                var lunchIn = displayEntries
                    .Where(e => e.EntryType is EntryType.LunchEnd or EntryType.BreakEnd)
                    .OrderBy(e => e.EntryTime)
                    .Select(e => (TimeOnly?)e.EntryTime)
                    .FirstOrDefault();
                var timeOut = displayEntries
                    .Where(e => e.EntryType == EntryType.TimeOut)
                    .OrderByDescending(e => e.EntryTime)
                    .Select(e => (TimeOnly?)e.EntryTime)
                    .FirstOrDefault();

                var resolved = new DtrResolvedDayPunches
                {
                    TimeIn = timeIn,
                    LunchOut = lunchOut,
                    LunchIn = lunchIn,
                    TimeOut = timeOut,
                    UsesOvernightLayout = false
                };

                return _attendanceComputation.Compute(new AttendanceComputationInput
                {
                    Date = date,
                    Config = config,
                    Shift = shift,
                    Schedule = scheduleContext,
                    ShiftResolved = resolved,
                    DayType = dayType,
                    AssessAttendance = assessAttendance
                }).UndertimeMinutes;
            }

            var shiftResolved = DtrPunchResolutionService.ResolveShift(
                scheduleContext, displayEntries, nextDayDisplay);
            var previousShiftResolved = DtrPunchResolutionService.ResolveShift(
                prevScheduleContext, prevDayDisplay, displayEntries);

            return _attendanceComputation.Compute(new AttendanceComputationInput
            {
                Date = date,
                Config = config,
                Shift = shift,
                PreviousShift = prevShift,
                Schedule = scheduleContext,
                PreviousSchedule = prevScheduleContext,
                ShiftResolved = shiftResolved,
                PreviousShiftResolved = previousShiftResolved,
                CurrentIsOvernight = currentIsOvernight,
                PreviousWasOvernight = previousWasOvernight,
                DayType = dayType,
                AssessAttendance = assessAttendance
            }).UndertimeMinutes;
        }

        private void ProcessFlexibleDay(
            EmployeeTimeCardFromDtrResult result,
            PayrollConfiguration? config,
            DateOnly date,
            List<TimeEntry> displayEntries,
            ShiftType shift,
            DtrDayScheduleContext? scheduleContext,
            DayType? dayType)
        {
            if (!ShouldAssessAttendance(config, date, shift, scheduleContext))
                return;

            var timeIn = displayEntries
                .Where(e => e.EntryType == EntryType.TimeIn)
                .OrderBy(e => e.EntryTime)
                .Select(e => (TimeOnly?)e.EntryTime)
                .FirstOrDefault();
            var lunchOut = displayEntries
                .Where(e => e.EntryType is EntryType.LunchStart or EntryType.BreakStart)
                .OrderBy(e => e.EntryTime)
                .Select(e => (TimeOnly?)e.EntryTime)
                .FirstOrDefault();
            var lunchIn = displayEntries
                .Where(e => e.EntryType is EntryType.LunchEnd or EntryType.BreakEnd)
                .OrderBy(e => e.EntryTime)
                .Select(e => (TimeOnly?)e.EntryTime)
                .FirstOrDefault();
            var timeOut = displayEntries
                .Where(e => e.EntryType == EntryType.TimeOut)
                .OrderByDescending(e => e.EntryTime)
                .Select(e => (TimeOnly?)e.EntryTime)
                .FirstOrDefault();

            var resolved = new DtrResolvedDayPunches
            {
                TimeIn = timeIn,
                LunchOut = lunchOut,
                LunchIn = lunchIn,
                TimeOut = timeOut,
                UsesOvernightLayout = false
            };

            var attendance = _attendanceComputation.Compute(new AttendanceComputationInput
            {
                Date = date,
                Config = config,
                Shift = shift,
                Schedule = scheduleContext,
                ShiftResolved = resolved,
                DayType = dayType,
                AssessAttendance = true
            });

            AddTardiness(result, attendance.TardinessMinutes);
            result.UndertimeMinutes += attendance.UndertimeMinutes;

            if (timeIn.HasValue && timeOut.HasValue)
            {
                var hours = attendance.WorkedHours > 0
                    ? attendance.WorkedHours
                    : (shift.Hours > 0 ? shift.Hours : 8m);
                CreditHours(result, dayType, hours);
                if (attendance.NightDifferentialHours > 0)
                    CreditNightDifferential(result, dayType, attendance.NightDifferentialHours);
                result.PotentialOvertimeHours += attendance.PotentialOvertimeHours;
                result.PresentDays++;
                return;
            }

            if (displayEntries.Any())
            {
                result.AbsentDays++;
                result.AbsentHours += GetAbsentCreditHours(shift);
            }
            else
            {
                result.AbsentDays++;
                result.AbsentHours += GetAbsentCreditHours(shift);
            }
        }

        private static void AddTardiness(EmployeeTimeCardFromDtrResult result, int tardinessMinutes)
        {
            if (tardinessMinutes <= 0)
                return;

            result.LateMinutes += tardinessMinutes;
            result.LateIncidents++;
        }

        private static bool ShouldCreditWorkedDay(
            DtrDayScheduleContext? scheduleContext,
            bool currentIsOvernight,
            DtrResolvedDayPunches shiftResolved)
        {
            if (!shiftResolved.HasCompletePair)
                return false;

            if (DtrPunchResolutionService.IsOvernightSchedule(scheduleContext))
                return currentIsOvernight;

            return scheduleContext == null || !scheduleContext.SkipAttendanceAssessment;
        }

        private static decimal GetAbsentCreditHours(ShiftType? shift)
        {
            if (shift?.Hours > 0)
                return shift.Hours;

            return 8m;
        }

        private async Task SyncScheduleChangesForRangeAsync(
            Guid employeeId,
            DateOnly periodStart,
            DateOnly periodEnd)
        {
            var cursor = new DateOnly(periodStart.Year, periodStart.Month, 1);
            var endMonth = new DateOnly(periodEnd.Year, periodEnd.Month, 1);

            while (cursor <= endMonth)
            {
                await _scheduleChange.SyncApprovedChangesForEmployeeMonthAsync(
                    employeeId, cursor.Month, cursor.Year);
                cursor = cursor.AddMonths(1);
            }
        }

        private async Task<Dictionary<DateOnly, ShiftType?>> LoadShiftsForRangeAsync(
            Guid employeeId,
            DateOnly periodStart,
            DateOnly periodEnd)
        {
            var result = new Dictionary<DateOnly, ShiftType?>();
            var cursor = new DateOnly(periodStart.Year, periodStart.Month, 1);
            var endMonth = new DateOnly(periodEnd.Year, periodEnd.Month, 1);

            while (cursor <= endMonth)
            {
                var monthShifts = await _attendance.GetEmployeeShiftTypesByDayAsync(
                    employeeId, cursor.Month, cursor.Year, approvedOnly: true);

                foreach (var (day, shift) in monthShifts)
                {
                    var date = new DateOnly(cursor.Year, cursor.Month, day);
                    if (date >= periodStart && date <= periodEnd)
                        result[date] = shift;
                }

                cursor = cursor.AddMonths(1);
            }

            return result;
        }

        private static void CreditHours(
            EmployeeTimeCardFromDtrResult result,
            DayType? dayType,
            decimal hours)
        {
            if (hours <= 0)
                return;

            if (dayType != null)
            {
                if (!result.HoursByDayTypeId.ContainsKey(dayType.Id))
                    result.HoursByDayTypeId[dayType.Id] = 0m;
                result.HoursByDayTypeId[dayType.Id] += hours;
            }

            // Legacy weekday buckets for older UI paths — only true Regular Day goes to RegularHours.
            // Rest Day / Holiday hours stay in HoursByDayTypeId so they are not miscounted as basic regular.
            var name = dayType?.Name ?? string.Empty;
            if (name.Equals("Saturday", StringComparison.OrdinalIgnoreCase))
                result.SaturdayHours += hours;
            else if (name.Equals("Sunday", StringComparison.OrdinalIgnoreCase))
                result.SundayHours += hours;
            else if (name.Contains("Regular", StringComparison.OrdinalIgnoreCase) &&
                     !name.Contains("Holiday", StringComparison.OrdinalIgnoreCase))
                result.RegularHours += hours;
        }

        private static void CreditNightDifferential(
            EmployeeTimeCardFromDtrResult result,
            DayType? dayType,
            decimal hours)
        {
            if (hours <= 0)
                return;

            result.NightDifferentialHours += hours;
            if (dayType == null)
                return;

            if (!result.NightDifferentialHoursByDayTypeId.ContainsKey(dayType.Id))
                result.NightDifferentialHoursByDayTypeId[dayType.Id] = 0m;
            result.NightDifferentialHoursByDayTypeId[dayType.Id] += hours;
        }

        private static bool ShouldAssessAttendance(
            PayrollConfiguration? config,
            DateOnly date,
            ShiftType? shift,
            DtrDayScheduleContext? scheduleContext)
        {
            if (shift?.IsLeave == true)
                return false;

            if (scheduleContext == null || scheduleContext.SkipAttendanceAssessment)
                return false;

            if (shift != null)
                return true;

            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            return config?.UseWeekendAsDayOff != true || !isWeekend;
        }

        private static List<TimeEntry> GetDisplayEntries(IEnumerable<TimeEntry> entries) =>
            entries.Where(e => !IsArrangePoolEntry(e)).ToList();

        private static bool IsArrangePoolEntry(TimeEntry e)
        {
            if (e.Status != EntryStatus.Flagged ||
                string.IsNullOrEmpty(e.ValidationNotes) ||
                !e.ValidationNotes.Contains("Unassigned punch", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return e.EntryTime < Noon;
        }

        private static string BuildNotes(EmployeeTimeCardFromDtrResult result)
        {
            var parts = new List<string> { "Imported from DTR" };
            if (result.PresentDays > 0)
                parts.Add($"{result.PresentDays} present");
            if (result.AbsentDays > 0)
                parts.Add($"{result.AbsentDays} absent");
            if (result.LeaveDays > 0)
                parts.Add($"{result.LeaveDays} leave/off");
            if (result.NightDifferentialHours > 0)
                parts.Add($"{result.NightDifferentialHours:0.##}h ND");
            if (result.PotentialOvertimeHours > 0)
                parts.Add($"{result.PotentialOvertimeHours:0.##}h potential OT");
            return string.Join(" · ", parts);
        }
    }
}
