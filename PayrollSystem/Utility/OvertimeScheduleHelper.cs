using PayrollSystem.Models;

namespace PayrollSystem.Utility
{
    // Validates ahead-of-time OT windows against assigned work-schedule duty hours.
    public static class OvertimeScheduleHelper
    {
        // Fixed-window working shift (not leave, day off, or flexible).
        public static bool HasFixedDutyWindow(ShiftType? shift) =>
            shift != null
            && !ShiftTypeScheduleHelper.IsUnassigned(shift)
            && !shift.IsLeave
            && !shift.IsFlexible
            && shift.StartTime.HasValue
            && shift.EndTime.HasValue;

        public static bool TryGetDutyWindow(
            DateOnly date,
            ShiftType shift,
            out DateTime dutyStart,
            out DateTime dutyEnd)
        {
            dutyStart = default;
            dutyEnd = default;

            if (!HasFixedDutyWindow(shift))
                return false;

            dutyStart = date.ToDateTime(TimeOnly.FromTimeSpan(shift.StartTime!.Value));
            dutyEnd = date.ToDateTime(TimeOnly.FromTimeSpan(shift.EndTime!.Value));
            if (shift.SpansNextDay || dutyEnd <= dutyStart)
                dutyEnd = date.AddDays(1).ToDateTime(TimeOnly.FromTimeSpan(shift.EndTime!.Value));

            return true;
        }

        // Minutes where the OT window overlaps the scheduled duty window on the given date.
        public static int GetDutyOverlapMinutes(
            DateOnly date,
            TimeOnly otStart,
            TimeOnly otEnd,
            ShiftType shift)
        {
            if (!TryGetDutyWindow(date, shift, out var dutyStart, out var dutyEnd))
                return 0;

            var otStartDt = date.ToDateTime(otStart);
            var otEndDt = date.ToDateTime(otEnd);
            if (otEndDt <= otStartDt)
                otEndDt = otEndDt.AddDays(1);

            var overlapStart = otStartDt > dutyStart ? otStartDt : dutyStart;
            var overlapEnd = otEndDt < dutyEnd ? otEndDt : dutyEnd;
            if (overlapEnd <= overlapStart)
                return 0;

            return (int)Math.Round((overlapEnd - overlapStart).TotalMinutes);
        }

        public static string FormatTimeOnly(TimeOnly time) =>
            DateTime.Today.Add(time.ToTimeSpan()).ToString("hh:mm tt");

        public static string FormatOvertimeWindow(TimeOnly start, TimeOnly end) =>
            $"{FormatTimeOnly(start)}–{FormatTimeOnly(end)}";

        public static string BuildDutyOverlapMessage(
            DateOnly date,
            ShiftType shift,
            TimeOnly otStart,
            TimeOnly otEnd)
        {
            var shiftLabel = !string.IsNullOrWhiteSpace(shift.Code) ? shift.Code.Trim() : shift.Label;
            var shiftTimes = ShiftTypeScheduleHelper.FormatTimeRange(shift);
            return
                $"Overtime cannot overlap regular duty hours. On {date:MMM d, yyyy}, scheduled {shiftLabel} ({shiftTimes}) conflicts with requested OT {FormatOvertimeWindow(otStart, otEnd)}. Request overtime outside your regular shift (e.g. after shift ends).";
        }
    }
}
