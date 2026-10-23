using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.Services
{
    public class DtrDailyAttendanceMetrics
    {
        public int TardinessMinutes { get; set; }
        public int UndertimeMinutes { get; set; }
    }

    public class DtrAttendanceCalculationService
    {
        private readonly ApplicationDbContext _db;

        public DtrAttendanceCalculationService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<PayrollConfiguration?> GetActiveConfigurationAsync()
        {
            return await _db.PayrollConfigurations
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.EffectiveDate)
                .FirstOrDefaultAsync();
        }


        // Loads effective scheduled shift types for an employee in a calendar month (day 1–31 → ShiftType).
        // Merges approved schedule entries and overlays fully approved schedule-change requests.
        public async Task<Dictionary<int, ShiftType>> GetEmployeeShiftTypesByDayAsync(
            Guid employeeId, int month, int year, bool approvedOnly = false)
        {
            var entriesQuery = _db.ScheduleEntries
                .AsNoTracking()
                .Include(e => e.ShiftType)
                .Include(e => e.WorkSchedule)
                .Where(e => e.EmployeeId == employeeId)
                .Where(e => e.WorkSchedule.Month == month && e.WorkSchedule.Year == year);

            if (approvedOnly)
                entriesQuery = entriesQuery.Where(e => e.WorkSchedule.Status == WorkScheduleStatus.Approved);

            var entries = await entriesQuery.ToListAsync();

            var result = new Dictionary<int, ShiftType>();
            foreach (var group in entries.GroupBy(e => e.Day))
            {
                var best = group
                    .OrderByDescending(e => e.WorkSchedule.Status == WorkScheduleStatus.Approved)
                    .ThenByDescending(e => e.WorkSchedule.LastModifiedAt ?? e.WorkSchedule.CreatedAt)
                    .ThenByDescending(e => e.WorkScheduleId)
                    .First();
                result[group.Key] = best.ShiftType;
            }

            var approvedChanges = await _db.ScheduleChangeRequests
                .AsNoTracking()
                .Include(r => r.RequestedShiftType)
                .Include(r => r.ApprovalRequest)
                .Include(r => r.WorkSchedule)
                .Where(r => r.EmployeeId == employeeId &&
                            r.WorkSchedule.Month == month &&
                            r.WorkSchedule.Year == year)
                .Where(r =>
                    (r.ApprovalRequest != null && r.ApprovalRequest.Status == ApprovalRequestStatus.Approved) ||
                    r.Status == JustificationStatus.Approved)
                .OrderBy(r => r.SubmittedOn)
                .ToListAsync();

            foreach (var change in approvedChanges
                .GroupBy(c => c.Day)
                .Select(g => g.OrderByDescending(c => c.SubmittedOn).ThenByDescending(c => c.Id).First()))
                result[change.Day] = change.RequestedShiftType;

            return result;
        }

        public async Task<ShiftType?> GetEmployeeShiftTypeForDayAsync(Guid employeeId, DateOnly date, bool approvedOnly = false)
        {
            var map = await GetEmployeeShiftTypesByDayAsync(employeeId, date.Month, date.Year, approvedOnly);
            map.TryGetValue(date.Day, out var shift);
            return shift;
        }

        public DtrDayScheduleContext? ResolveSchedule(PayrollConfiguration? config, ShiftType? shift)
        {
            if (shift != null)
            {
                if (shift.IsLeave || shift.IsFlexible)
                {
                    return new DtrDayScheduleContext { SkipAttendanceAssessment = true, UsesShiftTypeSchedule = true };
                }

                if (shift.StartTime.HasValue && shift.EndTime.HasValue)
                {
                    var scheduledIn = TimeOnly.FromTimeSpan(shift.StartTime.Value);
                    var scheduledOut = TimeOnly.FromTimeSpan(shift.EndTime.Value);
                    TimeOnly? lunchOut = null;
                    TimeOnly? lunchIn = null;

                    if (shift.HasLunchBreak && shift.LunchStart.HasValue && shift.LunchEnd.HasValue)
                    {
                        lunchOut = TimeOnly.FromTimeSpan(shift.LunchStart.Value);
                        lunchIn = TimeOnly.FromTimeSpan(shift.LunchEnd.Value);
                    }

                    return new DtrDayScheduleContext
                    {
                        SkipAttendanceAssessment = false,
                        UsesShiftTypeSchedule = true,
                        GracePeriodMinutes = shift.GracePeriodMinutes ?? config?.DefaultGracePeriodMinutes ?? 0,
                        ScheduledIn = scheduledIn,
                        ScheduledOut = scheduledOut,
                        SpansNextDay = shift.SpansNextDay,
                        HasLunchBreak = shift.HasLunchBreak && lunchOut.HasValue && lunchIn.HasValue,
                        ScheduledLunchOut = lunchOut,
                        ScheduledLunchIn = lunchIn,
                        RequiredWorkMinutes = (int)Math.Round(shift.Hours * 60m)
                    };
                }
            }

            return null;
        }

        // Computes tardiness and undertime for one DTR day using the assigned work-schedule shift only.
        public DtrDailyAttendanceMetrics CalculateDaily(
            PayrollConfiguration? config,
            TimeOnly? timeIn,
            TimeOnly? lunchOut,
            TimeOnly? lunchIn,
            TimeOnly? timeOut,
            ShiftType? scheduledShift = null,
            bool skipForDayOff = false)
        {
            return new DtrDailyAttendanceMetrics
            {
                TardinessMinutes = CalculateTardinessMinutes(config, timeIn, lunchIn, scheduledShift, skipForDayOff),
                UndertimeMinutes = CalculateUndertimeMinutes(config, timeOut, lunchOut, scheduledShift, skipForDayOff)
            };
        }

        //Late evening shift start for overnight duty (same calendar day as punch)
        public int CalculateOvernightEveningTardinessMinutes(
            PayrollConfiguration? config,
            TimeOnly? eveningIn,
            ShiftType? scheduledShift = null)
        {
            if (!eveningIn.HasValue)
                return 0;

            var schedule = ResolveSchedule(config, scheduledShift);
            if (schedule == null || schedule.SkipAttendanceAssessment || !schedule.SpansNextDay)
                return 0;

            // Morning punches on the row must not count as shift-start tardiness.
            if (eveningIn.Value < TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)))
                return 0;

            var late = (int)(eveningIn.Value.ToTimeSpan() - schedule.ScheduledIn.ToTimeSpan()).TotalMinutes;
            if (late <= 0)
                return 0;

            return ApplyGrace(late, schedule.GracePeriodMinutes);
        }

        //Early next-morning shift end for overnight duty
        public int CalculateOvernightMorningUndertimeMinutes(
            PayrollConfiguration? config,
            TimeOnly? morningOut,
            ShiftType? scheduledShift = null)
        {
            if (!morningOut.HasValue)
                return 0;

            var schedule = ResolveSchedule(config, scheduledShift);
            if (schedule == null || schedule.SkipAttendanceAssessment || !schedule.SpansNextDay)
                return 0;

            // Evening punches must not count as shift-end undertime.
            if (morningOut.Value >= TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)))
                return 0;

            var early = (int)(schedule.ScheduledOut.ToTimeSpan() - morningOut.Value.ToTimeSpan()).TotalMinutes;
            if (early <= 0)
                return 0;

            return early;
        }

        //Late arrival / late return from lunch
        public int CalculateTardinessMinutes(
            PayrollConfiguration? config,
            TimeOnly? timeIn,
            TimeOnly? lunchIn,
            ShiftType? scheduledShift = null,
            bool skipForDayOff = false)
        {
            if (skipForDayOff || !timeIn.HasValue)
                return 0;

            var schedule = ResolveSchedule(config, scheduledShift);
            if (schedule == null || schedule.SkipAttendanceAssessment)
                return 0;

            var shiftStart = schedule.ScheduledIn;
            var tardiness = ApplyGrace(
                MinutesAfter(timeIn.Value, schedule.ScheduledIn, shiftStart, schedule.SpansNextDay),
                schedule.GracePeriodMinutes);

            if (schedule.HasLunchBreak && lunchIn.HasValue && schedule.ScheduledLunchIn.HasValue)
            {
                tardiness += ApplyGrace(
                    MinutesAfter(
                        lunchIn.Value,
                        schedule.ScheduledLunchIn.Value,
                        shiftStart,
                        schedule.SpansNextDay),
                    schedule.GracePeriodMinutes);
            }

            return tardiness;
        }

        //Early departure / early lunch out
        public int CalculateUndertimeMinutes(
            PayrollConfiguration? config,
            TimeOnly? timeOut,
            TimeOnly? lunchOut,
            ShiftType? scheduledShift = null,
            bool skipForDayOff = false)
        {
            if (skipForDayOff || !timeOut.HasValue)
                return 0;

            var schedule = ResolveSchedule(config, scheduledShift);
            if (schedule == null || schedule.SkipAttendanceAssessment)
                return 0;

            var shiftStart = schedule.ScheduledIn;
            var undertime = MinutesBefore(
                timeOut.Value,
                schedule.ScheduledOut,
                shiftStart,
                schedule.SpansNextDay);

            if (schedule.HasLunchBreak && lunchOut.HasValue && schedule.ScheduledLunchOut.HasValue)
            {
                undertime += MinutesBefore(
                    lunchOut.Value,
                    schedule.ScheduledLunchOut.Value,
                    shiftStart,
                    schedule.SpansNextDay);
            }

            return undertime;
        }

        public static (string HoursText, string MinutesText) FormatHoursAndMinutes(int totalMinutes)
        {
            if (totalMinutes <= 0)
                return ("-", "-");

            return ((totalMinutes / 60).ToString(), (totalMinutes % 60).ToString());
        }

        private static int ToTimelineMinutes(TimeOnly time, TimeOnly shiftStart, bool spansNextDay)
        {
            var minutes = (int)time.ToTimeSpan().TotalMinutes;
            if (!spansNextDay)
                return minutes;

            var startMinutes = (int)shiftStart.ToTimeSpan().TotalMinutes;
            // Morning/next-day times wrap; shift-start time (e.g. 7:00 PM) stays on the evening axis.
            if (minutes < startMinutes)
                return minutes + (24 * 60);

            return minutes;
        }

        private static int MinutesAfter(TimeOnly actual, TimeOnly scheduled, TimeOnly shiftStart, bool spansNextDay)
        {
            var actualM = ToTimelineMinutes(actual, shiftStart, spansNextDay);
            var scheduledM = ToTimelineMinutes(scheduled, shiftStart, spansNextDay);

            if (actualM <= scheduledM)
                return 0;

            return actualM - scheduledM;
        }

        private static int MinutesBefore(TimeOnly actual, TimeOnly scheduled, TimeOnly shiftStart, bool spansNextDay)
        {
            var actualM = ToTimelineMinutes(actual, shiftStart, spansNextDay);
            var scheduledM = ToTimelineMinutes(scheduled, shiftStart, spansNextDay);

            if (actualM >= scheduledM)
                return 0;

            return scheduledM - actualM;
        }

        private static int ApplyGrace(int lateMinutes, int gracePeriodMinutes)
        {
            if (lateMinutes <= 0)
                return 0;

            if (gracePeriodMinutes <= 0)
                return lateMinutes;

            return Math.Max(0, lateMinutes - gracePeriodMinutes);
        }
    }
}
