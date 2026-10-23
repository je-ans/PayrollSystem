using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.Services
{
    // Eligible overtime after company policy (minimum minutes, rounding).
    // Pre-shift early arrival is never eligible OT.
    public sealed class EligibleOvertimeResult
    {
        public bool IsEligible { get; init; }
        public TimeOnly? EligibleStartTime { get; init; }
        public TimeOnly? EligibleEndTime { get; init; }
        public decimal RawHours { get; init; }
        public decimal EligibleHours { get; init; }
        public int EligibleMinutes { get; init; }
        public int? DayTypeId { get; init; }
        public string? DayTypeCode { get; init; }
        public string? DayTypeName { get; init; }
        public string? Message { get; init; }
        public bool RequiresApproval { get; init; } = true;
    }

    public interface IOvertimeEngineService
    {
        Task<EligibleOvertimeResult> GetEligibleOvertimeAsync(
            Guid employeeId,
            DateOnly workDate,
            CancellationToken ct = default);

        //Applies min OT + rounding to raw post-shift hours (e.g. DTR Eligible badge)
        decimal ApplyPolicyToHours(decimal rawHours, PayrollConfiguration? config);

        int ApplyRounding(int minutes, OvertimeRoundingRule rule);
    }

    public class OvertimeEngineService : IOvertimeEngineService
    {
        private static readonly TimeOnly Noon = new(12, 0);

        private readonly ApplicationDbContext _db;
        private readonly DtrAttendanceCalculationService _attendance;
        private readonly IAttendanceComputationService _attendanceComputation;
        private readonly IWorkingCalendarService _workingCalendar;

        public OvertimeEngineService(
            ApplicationDbContext db,
            DtrAttendanceCalculationService attendance,
            IAttendanceComputationService attendanceComputation,
            IWorkingCalendarService workingCalendar)
        {
            _db = db;
            _attendance = attendance;
            _attendanceComputation = attendanceComputation;
            _workingCalendar = workingCalendar;
        }

        public async Task<EligibleOvertimeResult> GetEligibleOvertimeAsync(
            Guid employeeId,
            DateOnly workDate,
            CancellationToken ct = default)
        {
            var config = await _attendance.GetActiveConfigurationAsync();
            var requiresApproval = config?.RequireOvertimeApproval ?? true;

            var shift = await _attendance.GetEmployeeShiftTypeForDayAsync(employeeId, workDate, approvedOnly: true);
            if (shift == null)
                shift = await _attendance.GetEmployeeShiftTypeForDayAsync(employeeId, workDate, approvedOnly: false);

            var calendarDayType = await _workingCalendar.GetDayTypeForDateAsync(workDate, ct);
            var calendarEntry = await _workingCalendar.GetEntryAsync(workDate, ct);
            var allDayTypes = await _db.DayTypes
                .AsNoTracking()
                .Where(d => d.IsActive && !d.IsDeleted)
                .ToListAsync(ct);
            var dayType = DayTypeHelper.ResolveDayType(
                shift,
                calendarDayType,
                isHoliday: calendarEntry?.HolidayId != null || DayTypeHelper.IsHolidayDayType(calendarDayType),
                allDayTypes);

            if (shift?.IsLeave == true)
            {
                return NotEligible(
                    dayType,
                    requiresApproval,
                    "Leave / off days are not eligible for overtime from attendance.");
            }

            var prevDate = workDate.AddDays(-1);
            var nextDate = workDate.AddDays(1);

            var entries = await _db.TimeEntries
                .AsNoTracking()
                .Where(te => te.EmployeeId == employeeId &&
                             te.EntryDate >= prevDate &&
                             te.EntryDate <= nextDate)
                .ToListAsync(ct);

            var dayEntries = FilterDisplay(entries.Where(e => e.EntryDate == workDate));
            var nextEntries = FilterDisplay(entries.Where(e => e.EntryDate == nextDate));
            var prevEntries = FilterDisplay(entries.Where(e => e.EntryDate == prevDate));

            var prevShift = await _attendance.GetEmployeeShiftTypeForDayAsync(employeeId, prevDate, approvedOnly: true)
                            ?? await _attendance.GetEmployeeShiftTypeForDayAsync(employeeId, prevDate, approvedOnly: false);

            var schedule = _attendance.ResolveSchedule(config, shift);
            var prevSchedule = _attendance.ResolveSchedule(config, prevShift);
            var currentIsOvernight = DtrPunchResolutionService.IsOvernightSchedule(schedule);
            var previousWasOvernight = DtrPunchResolutionService.IsOvernightSchedule(prevSchedule);

            var shiftResolved = DtrPunchResolutionService.ResolveShift(schedule, dayEntries, nextEntries);
            var previousShiftResolved = DtrPunchResolutionService.ResolveShift(prevSchedule, prevEntries, dayEntries);

            var assessAttendance = ShouldAssessAttendance(config, workDate, shift, schedule);
            var attendance = _attendanceComputation.Compute(new AttendanceComputationInput
            {
                Date = workDate,
                Config = config,
                Shift = shift,
                PreviousShift = prevShift,
                Schedule = schedule,
                PreviousSchedule = prevSchedule,
                ShiftResolved = shiftResolved,
                PreviousShiftResolved = previousShiftResolved,
                CurrentIsOvernight = currentIsOvernight,
                PreviousWasOvernight = previousWasOvernight,
                DayType = dayType,
                AssessAttendance = assessAttendance
            });

            if (attendance.PotentialOvertimeHours <= 0 ||
                !shiftResolved.TimeOut.HasValue)
            {
                return NotEligible(
                    dayType,
                    requiresApproval,
                    "No post-shift overtime detected for this date.");
            }

            // Eligible window: scheduled end → actual time out (post-shift only).
            TimeOnly? eligibleStart = null;
            TimeOnly eligibleEnd = shiftResolved.TimeOut.Value;

            if (TryGetScheduledEnd(workDate, shift, schedule, out var scheduledEnd))
            {
                eligibleStart = TimeOnly.FromDateTime(scheduledEnd);
            }
            else if (shift?.IsFlexible == true && shiftResolved.TimeIn.HasValue)
            {
                // Flexible: OT starts after credited hours from time-in.
                var creditHours = shift.Hours > 0 ? shift.Hours : 8m;
                var unpaid = _attendanceComputation.ResolveUnpaidBreakMinutes(shift, config);
                var otStart = workDate.ToDateTime(shiftResolved.TimeIn.Value)
                    .AddHours((double)creditHours)
                    .AddMinutes(unpaid);
                eligibleStart = TimeOnly.FromDateTime(otStart);
            }

            if (!eligibleStart.HasValue)
            {
                return NotEligible(
                    dayType,
                    requiresApproval,
                    "Cannot determine overtime start without a scheduled shift end.");
            }

            var rawMinutes = (int)Math.Round(attendance.PotentialOvertimeHours * 60m);
            var policyMinutes = ApplyPolicyToMinutes(rawMinutes, config);

            if (policyMinutes <= 0)
            {
                var min = config?.MinimumOvertimeMinutes ?? 30;
                return new EligibleOvertimeResult
                {
                    IsEligible = false,
                    EligibleStartTime = eligibleStart,
                    EligibleEndTime = eligibleEnd,
                    RawHours = RoundHours(rawMinutes / 60m),
                    EligibleHours = 0,
                    EligibleMinutes = 0,
                    DayTypeId = dayType?.Id,
                    DayTypeCode = dayType?.Code,
                    DayTypeName = dayType?.Name,
                    RequiresApproval = requiresApproval,
                    Message = rawMinutes > 0 && rawMinutes < min
                        ? $"Post-shift time ({rawMinutes} min) is below the minimum overtime ({min} min)."
                        : "Overtime rounds to zero under company rounding rules."
                };
            }

            // Align end time to rounded eligible duration from scheduled end.
            var adjustedEnd = workDate.ToDateTime(eligibleStart.Value).AddMinutes(policyMinutes);
            if (adjustedEnd.Date > workDate.ToDateTime(eligibleStart.Value).Date ||
                TimeOnly.FromDateTime(adjustedEnd) < eligibleStart.Value)
            {
                // crossed midnight — fine
            }

            return new EligibleOvertimeResult
            {
                IsEligible = true,
                EligibleStartTime = eligibleStart,
                EligibleEndTime = TimeOnly.FromDateTime(adjustedEnd),
                RawHours = RoundHours(rawMinutes / 60m),
                EligibleHours = RoundHours(policyMinutes / 60m),
                EligibleMinutes = policyMinutes,
                DayTypeId = dayType?.Id,
                DayTypeCode = dayType?.Code,
                DayTypeName = dayType?.Name,
                RequiresApproval = requiresApproval,
                Message = policyMinutes != rawMinutes
                    ? $"Eligible overtime after company rules: {policyMinutes / 60m:0.##}h (raw {rawMinutes / 60m:0.##}h)."
                    : $"Eligible overtime: {policyMinutes / 60m:0.##}h."
            };
        }

        public decimal ApplyPolicyToHours(decimal rawHours, PayrollConfiguration? config)
        {
            if (rawHours <= 0)
                return 0m;

            var rawMinutes = (int)Math.Round(rawHours * 60m, MidpointRounding.AwayFromZero);
            var policyMinutes = ApplyPolicyToMinutes(rawMinutes, config);
            return RoundHours(policyMinutes / 60m);
        }

        public int ApplyRounding(int minutes, OvertimeRoundingRule rule)
        {
            if (minutes <= 0)
                return 0;

            return rule switch
            {
                OvertimeRoundingRule.Nearest15Minutes => (int)Math.Round(minutes / 15.0, MidpointRounding.AwayFromZero) * 15,
                OvertimeRoundingRule.Nearest30Minutes => (int)Math.Round(minutes / 30.0, MidpointRounding.AwayFromZero) * 30,
                OvertimeRoundingRule.RoundUp15Minutes => ((minutes + 14) / 15) * 15,
                OvertimeRoundingRule.RoundUp30Minutes => ((minutes + 29) / 30) * 30,
                _ => minutes
            };
        }

        private int ApplyPolicyToMinutes(int rawMinutes, PayrollConfiguration? config)
        {
            if (rawMinutes <= 0)
                return 0;

            var minRequired = Math.Max(0, config?.MinimumOvertimeMinutes ?? 30);
            if (rawMinutes < minRequired)
                return 0;

            var rounded = ApplyRounding(rawMinutes, config?.OvertimeRoundingRule ?? OvertimeRoundingRule.None);
            if (rounded < minRequired)
                return 0;

            return rounded;
        }

        private static EligibleOvertimeResult NotEligible(
            DayType? dayType,
            bool requiresApproval,
            string message) =>
            new()
            {
                IsEligible = false,
                DayTypeId = dayType?.Id,
                DayTypeCode = dayType?.Code,
                DayTypeName = dayType?.Name,
                RequiresApproval = requiresApproval,
                Message = message
            };

        private static bool TryGetScheduledEnd(
            DateOnly date,
            ShiftType? shift,
            DtrDayScheduleContext? schedule,
            out DateTime scheduledEnd)
        {
            scheduledEnd = default;

            if (shift?.StartTime.HasValue == true && shift.EndTime.HasValue)
            {
                var start = date.ToDateTime(TimeOnly.FromTimeSpan(shift.StartTime.Value));
                var end = date.ToDateTime(TimeOnly.FromTimeSpan(shift.EndTime.Value));
                if (shift.SpansNextDay || end <= start)
                    end = date.AddDays(1).ToDateTime(TimeOnly.FromTimeSpan(shift.EndTime.Value));
                scheduledEnd = end;
                return true;
            }

            if (schedule == null || schedule.SkipAttendanceAssessment)
                return false;

            var sStart = date.ToDateTime(schedule.ScheduledIn);
            var sEnd = date.ToDateTime(schedule.ScheduledOut);
            if (schedule.SpansNextDay || sEnd <= sStart)
                sEnd = date.AddDays(1).ToDateTime(schedule.ScheduledOut);

            scheduledEnd = sEnd;
            return true;
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

        private static List<TimeEntry> FilterDisplay(IEnumerable<TimeEntry> entries) =>
            entries
                .Where(e => !IsArrangePoolEntry(e))
                .OrderBy(e => e.EntryTime)
                .ToList();

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

        private static decimal RoundHours(decimal hours) =>
            Math.Round(Math.Max(0m, hours), 4, MidpointRounding.AwayFromZero);
    }
}
