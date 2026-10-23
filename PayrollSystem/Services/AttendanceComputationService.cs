using PayrollSystem.Models;

namespace PayrollSystem.Services
{
    // Attendance-only day result. Does not compute payroll amounts.
    // WorkedHours — credited Basic Hours = assigned shift hours (not reduced by late/undertime).
    // ActualPresenceHours — Time Out − Time In (gross presence; internal / diagnostic)
    // PotentialOvertimeHours — time after scheduled end only (pre-shift early arrival is not OT)
    // TardinessMinutes / UndertimeMinutes — tracked for payroll deductions only (never reduce Basic Hours)
    // Approved OT comes from the overtime workflow, not this engine.

    public sealed class AttendanceDayComputation
    {
        public DateOnly Date { get; init; }
        public int? DayTypeId { get; init; }
        public string? DayTypeCode { get; init; }
        public string? DayTypeName { get; init; }
        public int UnpaidBreakMinutes { get; init; }

        //Credited Basic Hours for the day (scheduled shift hours when present)
        public decimal WorkedHours { get; init; }

        //Gross presence (Time Out − Time In), before break semantics
        public decimal ActualPresenceHours { get; init; }

        //Post-shift excess that may become payable OT after approval
        public decimal PotentialOvertimeHours { get; init; }

        //Early arrival before scheduled start (not auto-OT)
        public int EarlyArrivalMinutes { get; init; }

        public decimal NightDifferentialHours { get; init; }
        public int TardinessMinutes { get; init; }
        public int UndertimeMinutes { get; init; }
        public bool HasCompletePair { get; init; }
        public bool AssessAttendance { get; init; }
    }

    public sealed class AttendanceComputationInput
    {
        public DateOnly Date { get; init; }
        public PayrollConfiguration? Config { get; init; }
        public ShiftType? Shift { get; init; }
        public ShiftType? PreviousShift { get; init; }
        public DtrDayScheduleContext? Schedule { get; init; }
        public DtrDayScheduleContext? PreviousSchedule { get; init; }
        public required DtrResolvedDayPunches ShiftResolved { get; init; }
        public DtrResolvedDayPunches? PreviousShiftResolved { get; init; }
        public bool CurrentIsOvernight { get; init; }
        public bool PreviousWasOvernight { get; init; }
        public DayType? DayType { get; init; }
        public bool AssessAttendance { get; init; }
    }

    public interface IAttendanceComputationService
    {
        AttendanceDayComputation Compute(AttendanceComputationInput input);
        int ResolveUnpaidBreakMinutes(ShiftType? shift, PayrollConfiguration? config);
    }

    // Attendance engine: credited Basic Hours (full shift), late/undertime for deductions,
    // ND on scheduled window, potential post-shift OT.
    public class AttendanceComputationService : IAttendanceComputationService
    {
        private readonly DtrAttendanceCalculationService _attendance;

        public AttendanceComputationService(DtrAttendanceCalculationService attendance)
        {
            _attendance = attendance;
        }

        public AttendanceDayComputation Compute(AttendanceComputationInput input)
        {
            var (tardiness, undertime) = ComputeLateAndUndertime(input);
            var unpaidBreak = ResolveUnpaidBreakMinutes(input.Shift, input.Config);
            var hasCompletePair = input.ShiftResolved.HasCompletePair;

            decimal workedHours = 0m;
            decimal actualPresence = 0m;
            decimal potentialOt = 0m;
            decimal ndHours = 0m;
            var earlyArrival = 0;

            if (ShouldComputePresence(input))
            {
                var (actualStart, actualEnd) = BuildWorkInterval(
                    input.Date,
                    input.ShiftResolved.TimeIn!.Value,
                    input.ShiftResolved.TimeOut!.Value,
                    input.CurrentIsOvernight || input.ShiftResolved.UsesOvernightLayout);

                actualPresence = MinutesToHours((actualEnd - actualStart).TotalMinutes);

                // Basic Hours = full scheduled shift hours whenever the day is present/credited.
                // Late and undertime are recorded separately and deducted only in payroll.
                workedHours = ResolveScheduledCreditHours(input);

                if (TryGetScheduledInterval(input, out var scheduledStart, out var scheduledEnd))
                {
                    if (actualStart < scheduledStart)
                        earlyArrival = (int)Math.Round((scheduledStart - actualStart).TotalMinutes);

                    // Potential OT = post-shift only (early arrival is never auto-OT).
                    if (actualEnd > scheduledEnd)
                        potentialOt = MinutesToHours((actualEnd - scheduledEnd).TotalMinutes);

                    // ND on actual ∩ scheduled (excludes pre-shift and potential OT).
                    var creditedStart = actualStart > scheduledStart ? actualStart : scheduledStart;
                    var creditedEnd = actualEnd < scheduledEnd ? actualEnd : scheduledEnd;
                    if (creditedEnd > creditedStart)
                    {
                        ndHours = ComputeNightDifferentialHours(
                            creditedStart,
                            creditedEnd,
                            input.Config,
                            unpaidBreak,
                            input.Shift);
                    }
                }
                else
                {
                    // Flexible / no fixed window: Basic Hours stay at scheduled shift hours.
                    // Excess net presence is eligible OT; shortfall becomes undertime for payroll.
                    var scheduled = ResolveScheduledCreditHours(input);
                    var netPresence = Math.Max(0m, actualPresence - (unpaidBreak / 60m));

                    if (input.Shift?.IsFlexible == true)
                    {
                        if (netPresence > scheduled)
                            potentialOt = netPresence - scheduled;
                        else if (netPresence < scheduled)
                        {
                            var shortfallMinutes = (int)Math.Round((scheduled - netPresence) * 60m,
                                MidpointRounding.AwayFromZero);
                            if (shortfallMinutes > undertime)
                                undertime = shortfallMinutes;
                        }
                    }
                    else if (workedHours <= 0 && netPresence > 0)
                    {
                        workedHours = RoundHours(netPresence);
                    }

                    ndHours = ComputeNightDifferentialHours(
                        actualStart,
                        actualEnd,
                        input.Config,
                        unpaidBreak,
                        input.Shift);
                }
            }

            return new AttendanceDayComputation
            {
                Date = input.Date,
                DayTypeId = input.DayType?.Id,
                DayTypeCode = input.DayType?.Code,
                DayTypeName = input.DayType?.Name,
                UnpaidBreakMinutes = unpaidBreak,
                WorkedHours = RoundHours(workedHours),
                ActualPresenceHours = RoundHours(actualPresence),
                PotentialOvertimeHours = RoundHours(potentialOt),
                EarlyArrivalMinutes = Math.Max(0, earlyArrival),
                NightDifferentialHours = RoundHours(ndHours),
                TardinessMinutes = tardiness,
                UndertimeMinutes = undertime,
                HasCompletePair = hasCompletePair,
                AssessAttendance = input.AssessAttendance
            };
        }

        // Shift lunch duration when unpaid and configured; otherwise PayrollConfiguration.DefaultBreakDurationMinutes.
        // Used for ND break exclusion and flexible presence netting — not for credited Worked hours.
        public int ResolveUnpaidBreakMinutes(ShiftType? shift, PayrollConfiguration? config)
        {
            var defaultMinutes = Math.Max(0, config?.DefaultBreakDurationMinutes ?? 60);

            if (shift == null || !shift.HasLunchBreak)
                return defaultMinutes;

            if (shift.IsBreakPaid)
                return 0;

            if (shift.LunchStart.HasValue && shift.LunchEnd.HasValue)
            {
                var minutes = (int)Math.Round((shift.LunchEnd.Value - shift.LunchStart.Value).TotalMinutes);
                if (minutes < 0)
                    minutes += 24 * 60;
                return Math.Max(0, minutes);
            }

            return defaultMinutes;
        }

        // Credited Basic Hours = assigned shift hours (or schedule required minutes).
        // Late and undertime never reduce this value — they are deducted in payroll separately.
        private static decimal ResolveScheduledCreditHours(AttendanceComputationInput input)
        {
            if (input.Shift?.Hours > 0)
                return input.Shift.Hours;

            if (input.Schedule?.RequiredWorkMinutes > 0)
                return input.Schedule.RequiredWorkMinutes / 60m;

            return 8m;
        }

        private static bool TryGetScheduledInterval(
            AttendanceComputationInput input,
            out DateTime scheduledStart,
            out DateTime scheduledEnd)
        {
            scheduledStart = default;
            scheduledEnd = default;

            var schedule = input.Schedule;
            if (schedule == null || schedule.SkipAttendanceAssessment)
                return false;

            // Prefer explicit shift clock times.
            if (input.Shift?.StartTime.HasValue == true && input.Shift.EndTime.HasValue)
            {
                var start = input.Date.ToDateTime(TimeOnly.FromTimeSpan(input.Shift.StartTime.Value));
                var end = input.Date.ToDateTime(TimeOnly.FromTimeSpan(input.Shift.EndTime.Value));
                if (input.Shift.SpansNextDay || end <= start)
                    end = input.Date.AddDays(1).ToDateTime(TimeOnly.FromTimeSpan(input.Shift.EndTime.Value));

                scheduledStart = start;
                scheduledEnd = end;
                return true;
            }

            scheduledStart = input.Date.ToDateTime(schedule.ScheduledIn);
            scheduledEnd = input.Date.ToDateTime(schedule.ScheduledOut);
            if (schedule.SpansNextDay || scheduledEnd <= scheduledStart)
                scheduledEnd = input.Date.AddDays(1).ToDateTime(schedule.ScheduledOut);

            return true;
        }

        private (int Tardiness, int Undertime) ComputeLateAndUndertime(AttendanceComputationInput input)
        {
            if (input.Shift?.IsLeave == true)
            {
                var leaveUndertime = 0;
                if (input.PreviousWasOvernight && input.PreviousShiftResolved?.TimeOut.HasValue == true)
                {
                    leaveUndertime = _attendance.CalculateOvernightMorningUndertimeMinutes(
                        input.Config,
                        input.PreviousShiftResolved.TimeOut,
                        input.PreviousShift);
                }

                return (0, leaveUndertime);
            }

            if (!input.CurrentIsOvernight && !input.PreviousWasOvernight)
            {
                if (!input.AssessAttendance)
                    return (0, 0);

                var metrics = _attendance.CalculateDaily(
                    input.Config,
                    input.ShiftResolved.TimeIn,
                    input.ShiftResolved.LunchOut,
                    input.ShiftResolved.LunchIn,
                    input.ShiftResolved.TimeOut,
                    input.Shift);
                return (metrics.TardinessMinutes, metrics.UndertimeMinutes);
            }

            var isWeekend = input.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (input.Shift == null &&
                input.Config?.UseWeekendAsDayOff == true &&
                isWeekend &&
                !input.PreviousWasOvernight)
            {
                return (0, 0);
            }

            var tardiness = 0;
            var undertime = 0;

            if (input.CurrentIsOvernight &&
                input.Schedule != null &&
                !input.Schedule.SkipAttendanceAssessment)
            {
                tardiness = _attendance.CalculateOvernightEveningTardinessMinutes(
                    input.Config,
                    input.ShiftResolved.TimeIn,
                    input.Shift);
            }

            if (input.PreviousWasOvernight && input.PreviousShiftResolved?.TimeOut.HasValue == true)
            {
                undertime = _attendance.CalculateOvernightMorningUndertimeMinutes(
                    input.Config,
                    input.PreviousShiftResolved.TimeOut,
                    input.PreviousShift);
            }

            return (tardiness, undertime);
        }

        private static bool ShouldComputePresence(AttendanceComputationInput input)
        {
            if (input.Shift?.IsLeave == true)
                return false;

            if (!input.ShiftResolved.HasCompletePair ||
                !input.ShiftResolved.TimeIn.HasValue ||
                !input.ShiftResolved.TimeOut.HasValue)
            {
                return false;
            }

            // Overnight: attribute full shift metrics to the shift-start (evening) calendar day only.
            if (DtrPunchResolutionService.IsOvernightSchedule(input.Schedule) ||
                input.ShiftResolved.UsesOvernightLayout)
            {
                return input.CurrentIsOvernight;
            }

            if (input.Schedule?.SkipAttendanceAssessment == true && input.Shift?.IsFlexible != true)
                return false;

            return true;
        }

        private static (DateTime Start, DateTime End) BuildWorkInterval(
            DateOnly date,
            TimeOnly timeIn,
            TimeOnly timeOut,
            bool spansNextDay)
        {
            var start = date.ToDateTime(timeIn);
            var end = date.ToDateTime(timeOut);

            if (spansNextDay || end <= start)
                end = date.AddDays(1).ToDateTime(timeOut);

            return (start, end);
        }

        // Hours of the interval that fall inside the ND window, excluding unpaid break overlap when known.
        public static decimal ComputeNightDifferentialHours(
            DateTime workStart,
            DateTime workEnd,
            PayrollConfiguration? config,
            int unpaidBreakMinutes,
            ShiftType? shift)
        {
            if (workEnd <= workStart)
                return 0m;

            var ndStart = config?.NightDifferentialStartTime ?? TimeSpan.FromHours(22);
            var ndEnd = config?.NightDifferentialEndTime ?? TimeSpan.FromHours(6);

            var ndMinutes = OverlapWithWindowMinutes(workStart, workEnd, ndStart, ndEnd);
            if (ndMinutes <= 0)
                return 0m;

            var breakOverlap = 0;
            if (unpaidBreakMinutes > 0 &&
                shift?.HasLunchBreak == true &&
                !shift.IsBreakPaid &&
                shift.LunchStart.HasValue &&
                shift.LunchEnd.HasValue)
            {
                var lunchStart = DateOnly.FromDateTime(workStart).ToDateTime(TimeOnly.FromTimeSpan(shift.LunchStart.Value));
                var lunchEnd = DateOnly.FromDateTime(workStart).ToDateTime(TimeOnly.FromTimeSpan(shift.LunchEnd.Value));
                if (lunchEnd <= lunchStart)
                    lunchEnd = lunchEnd.AddDays(1);

                if (lunchStart < workStart) lunchStart = workStart;
                if (lunchEnd > workEnd) lunchEnd = workEnd;
                if (lunchEnd > lunchStart)
                    breakOverlap = OverlapWithWindowMinutes(lunchStart, lunchEnd, ndStart, ndEnd);
            }
            else if (unpaidBreakMinutes > 0)
            {
                var mid = workStart.AddMinutes((workEnd - workStart).TotalMinutes / 2.0);
                var half = unpaidBreakMinutes / 2.0;
                var breakStart = mid.AddMinutes(-half);
                var breakEnd = mid.AddMinutes(unpaidBreakMinutes - half);
                if (breakStart < workStart) breakStart = workStart;
                if (breakEnd > workEnd) breakEnd = workEnd;
                if (breakEnd > breakStart)
                    breakOverlap = OverlapWithWindowMinutes(breakStart, breakEnd, ndStart, ndEnd);
            }

            return Math.Max(0, ndMinutes - breakOverlap) / 60m;
        }

        public static int OverlapWithWindowMinutes(
            DateTime start,
            DateTime end,
            TimeSpan windowStart,
            TimeSpan windowEnd)
        {
            if (end <= start)
                return 0;

            var total = 0.0;
            var day = start.Date.AddDays(-1);
            var lastDay = end.Date;

            for (; day <= lastDay; day = day.AddDays(1))
            {
                foreach (var (segStart, segEnd) in WindowSegmentsForDay(day, windowStart, windowEnd))
                {
                    var overlapStart = start > segStart ? start : segStart;
                    var overlapEnd = end < segEnd ? end : segEnd;
                    if (overlapEnd > overlapStart)
                        total += (overlapEnd - overlapStart).TotalMinutes;
                }
            }

            return (int)Math.Round(total);
        }

        private static IEnumerable<(DateTime Start, DateTime End)> WindowSegmentsForDay(
            DateTime day,
            TimeSpan windowStart,
            TimeSpan windowEnd)
        {
            if (windowStart == windowEnd)
                yield break;

            if (windowStart < windowEnd)
            {
                yield return (day.Add(windowStart), day.Add(windowEnd));
            }
            else
            {
                yield return (day.Add(windowStart), day.AddDays(1).Add(windowEnd));
            }
        }

        private static decimal MinutesToHours(double minutes) =>
            minutes <= 0 ? 0m : (decimal)minutes / 60m;

        private static decimal RoundHours(decimal hours) =>
            Math.Round(Math.Max(0m, hours), 4, MidpointRounding.AwayFromZero);
    }
}
