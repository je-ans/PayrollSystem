using PayrollSystem.Models;

namespace PayrollSystem.Services
{
    public enum DtrScheduleLayout
    {
        Overnight,
        SimpleDay,
        WithLunch
    }

    // Shift-based punch resolution (arrange, justify, attendance). Overnight shifts pair evening on the
    // shift-start date with morning on the next calendar date.
    public sealed class DtrResolvedDayPunches
    {
        public TimeOnly? TimeIn { get; init; }
        public TimeOnly? LunchOut { get; init; }
        public TimeOnly? LunchIn { get; init; }
        public TimeOnly? TimeOut { get; init; }
        public Guid? TimeInEntryId { get; init; }
        public Guid? LunchOutEntryId { get; init; }
        public Guid? LunchInEntryId { get; init; }
        public Guid? TimeOutEntryId { get; init; }
        public bool UsesOvernightLayout { get; init; }

        public bool HasCompletePair => UsesOvernightLayout
            ? TimeIn.HasValue && TimeOut.HasValue
            : TimeIn.HasValue && TimeOut.HasValue;
    }

    // Maps raw time entries to DTR slots. Arrange/justify use shift-start pairing; DTR tables remap
    // overnight punches to calendar-day columns (morning out on the date it occurs).
    public static class DtrPunchResolutionService
    {
        private static readonly TimeOnly Noon = new(12, 0);

        public static bool IsOvernightSchedule(DtrDayScheduleContext? schedule) =>
            schedule != null &&
            !schedule.SkipAttendanceAssessment &&
            !schedule.HasLunchBreak &&
            (schedule.SpansNextDay || schedule.ScheduledOut <= schedule.ScheduledIn);

        public static DtrScheduleLayout GetLayout(DtrDayScheduleContext? schedule)
        {
            if (IsOvernightSchedule(schedule))
                return DtrScheduleLayout.Overnight;

            if (schedule?.HasLunchBreak == true)
                return DtrScheduleLayout.WithLunch;

            return DtrScheduleLayout.SimpleDay;
        }

        public static List<TimeEntry> FilterForDisplay(IEnumerable<TimeEntry> entries) =>
            FilterAvailableEntries(entries);

        public static List<TimeEntry> FilterForArrange(
            IEnumerable<TimeEntry> entries,
            ISet<Guid> consumedEntryIds) =>
            entries
                .Where(e => !consumedEntryIds.Contains(e.Id))
                .OrderBy(e => e.EntryTime)
                .ToList();

        //Shift-start resolution used by arrange, justify, and attendance metrics
        public static DtrResolvedDayPunches ResolveShift(
            DtrDayScheduleContext? schedule,
            IReadOnlyList<TimeEntry> dayEntries,
            IReadOnlyList<TimeEntry>? nextDayEntries = null)
        {
            var displayEntries = FilterAvailableEntries(dayEntries);
            var nextDisplay = FilterAvailableEntries(nextDayEntries ?? Array.Empty<TimeEntry>());

            if (IsOvernightSchedule(schedule))
                return ResolveOvernightShift(schedule!, displayEntries, nextDisplay);

            return ResolveStandard(schedule, displayEntries);
        }

        private static List<TimeEntry> FilterAvailableEntries(IEnumerable<TimeEntry> entries) =>
            entries
                .Where(e => !IsArrangePoolEntry(e))
                .OrderBy(e => e.EntryTime)
                .ToList();

        private static DtrResolvedDayPunches ResolveStandard(
            DtrDayScheduleContext? schedule,
            List<TimeEntry> displayEntries)
        {
            var expectsLunch = schedule?.HasLunchBreak == true;

            var timeInEntry = FindFirstEntry(displayEntries, EntryType.TimeIn);
            var lunchOutEntry = FindFirstEntry(displayEntries, EntryType.LunchStart, EntryType.BreakStart);
            var lunchInEntry = FindFirstEntry(displayEntries, EntryType.LunchEnd, EntryType.BreakEnd);
            var timeOutEntry = FindLastEntry(displayEntries, EntryType.TimeOut);

            if (!expectsLunch)
            {
                lunchOutEntry = null;
                lunchInEntry = null;

                if (timeInEntry == null)
                    timeInEntry = displayEntries.FirstOrDefault(e => e.EntryTime < Noon);

                if (timeOutEntry == null)
                    timeOutEntry = displayEntries.LastOrDefault(e => e.EntryTime >= Noon);
            }

            return new DtrResolvedDayPunches
            {
                TimeIn = timeInEntry?.EntryTime,
                LunchOut = lunchOutEntry?.EntryTime,
                LunchIn = lunchInEntry?.EntryTime,
                TimeOut = timeOutEntry?.EntryTime,
                TimeInEntryId = timeInEntry?.Id,
                LunchOutEntryId = lunchOutEntry?.Id,
                LunchInEntryId = lunchInEntry?.Id,
                TimeOutEntryId = timeOutEntry?.Id,
                UsesOvernightLayout = false
            };
        }

        private static DtrResolvedDayPunches ResolveOvernightShift(
            DtrDayScheduleContext schedule,
            List<TimeEntry> dayEntries,
            List<TimeEntry> nextDayEntries)
        {
            var (eveningIn, eveningInId) = FindEveningPunch(dayEntries, schedule.ScheduledIn);
            var (nextMorningOut, nextMorningOutId) = FindMorningPunch(nextDayEntries, schedule.ScheduledOut);

            // Some devices store the next-morning out on the shift-start date.
            if (!nextMorningOut.HasValue)
                (nextMorningOut, nextMorningOutId) = FindMorningPunch(dayEntries, schedule.ScheduledOut);

            return new DtrResolvedDayPunches
            {
                TimeIn = eveningIn,
                TimeInEntryId = eveningInId,
                TimeOut = nextMorningOut,
                TimeOutEntryId = nextMorningOutId,
                UsesOvernightLayout = true
            };
        }

        private static (TimeOnly? Time, Guid? EntryId) FindEveningPunch(
            IReadOnlyList<TimeEntry> entries,
            TimeOnly scheduledIn)
        {
            var evening = entries
                .Where(e => e.EntryTime >= Noon)
                .OrderBy(e => e.EntryTime)
                .ToList();

            if (evening.Count == 0)
                return (null, null);

            var typedIn = evening.FirstOrDefault(e => e.EntryType == EntryType.TimeIn);
            if (typedIn != null)
                return (typedIn.EntryTime, typedIn.Id);

            var match = evening
                .OrderBy(e => Math.Abs((e.EntryTime.ToTimeSpan() - scheduledIn.ToTimeSpan()).TotalMinutes))
                .First();

            return (match.EntryTime, match.Id);
        }

        private static (TimeOnly? Time, Guid? EntryId) FindMorningPunch(
            IReadOnlyList<TimeEntry> entries,
            TimeOnly scheduledOut)
        {
            var morning = entries
                .Where(e => e.EntryTime < Noon)
                .OrderBy(e => e.EntryTime)
                .ToList();

            if (morning.Count == 0)
                return (null, null);

            var typedOut = morning
                .Where(e => e.EntryType == EntryType.TimeOut)
                .OrderBy(e => Math.Abs((e.EntryTime.ToTimeSpan() - scheduledOut.ToTimeSpan()).TotalMinutes))
                .FirstOrDefault();

            if (typedOut != null)
                return (typedOut.EntryTime, typedOut.Id);

            var match = morning
                .OrderBy(e => Math.Abs((e.EntryTime.ToTimeSpan() - scheduledOut.ToTimeSpan()).TotalMinutes))
                .ThenByDescending(e => e.EntryTime)
                .First();

            return (match.EntryTime, match.Id);
        }

        private static TimeEntry? FindFirstEntry(IEnumerable<TimeEntry> entries, params EntryType[] types) =>
            entries
                .Where(e => types.Contains(e.EntryType))
                .OrderBy(e => e.EntryTime)
                .FirstOrDefault();

        private static TimeEntry? FindLastEntry(IEnumerable<TimeEntry> entries, EntryType type) =>
            entries
                .Where(e => e.EntryType == type)
                .OrderByDescending(e => e.EntryTime)
                .FirstOrDefault();

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

        public static string FormatJustificationRequestedTimes(
            TimeLogJustification request,
            DtrScheduleLayout layout)
        {
            var parts = new List<string>();

            switch (layout)
            {
                case DtrScheduleLayout.Overnight:
                    if (request.RequestedPmOut.HasValue)
                        parts.Add($"Shift Start (PM): {request.RequestedPmOut.Value:hh:mm tt}");
                    if (request.RequestedAmIn.HasValue)
                    {
                        var nextDay = request.EntryDate.AddDays(1);
                        parts.Add($"Shift End (AM {nextDay:MMM dd}): {request.RequestedAmIn.Value:hh:mm tt}");
                    }
                    break;
                case DtrScheduleLayout.SimpleDay:
                    if (request.RequestedAmIn.HasValue)
                        parts.Add($"Time In: {request.RequestedAmIn.Value:hh:mm tt}");
                    if (request.RequestedPmOut.HasValue)
                        parts.Add($"PM Out: {request.RequestedPmOut.Value:hh:mm tt}");
                    break;
                default:
                    if (request.RequestedAmIn.HasValue)
                        parts.Add($"AM In: {request.RequestedAmIn.Value:hh:mm tt}");
                    if (request.RequestedLunchOut.HasValue)
                        parts.Add($"Lunch Out: {request.RequestedLunchOut.Value:hh:mm tt}");
                    if (request.RequestedLunchIn.HasValue)
                        parts.Add($"Lunch In: {request.RequestedLunchIn.Value:hh:mm tt}");
                    if (request.RequestedPmOut.HasValue)
                        parts.Add($"PM Out: {request.RequestedPmOut.Value:hh:mm tt}");
                    break;
            }

            return parts.Count > 0 ? string.Join(" | ", parts) : "-";
        }
    }
}
