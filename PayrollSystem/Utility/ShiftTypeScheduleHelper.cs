using PayrollSystem.Models;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Utility
{
    public static class ShiftTypeScheduleHelper
    {
        public static readonly ShiftType BlankShift = new()
        {
            Id = 0,
            Code = string.Empty,
            Label = string.Empty,
            Hours = 0,
            ColorHex = "#f3f4f6"
        };

        public static bool IsUnassigned(ShiftType? shift) =>
            shift == null || shift.Id <= 0 || string.IsNullOrWhiteSpace(shift.Code) || shift.Code == "-";

        public static string ToDtrSpecialLabel(ShiftType shift)
        {
            if (IsUnassigned(shift))
                return string.Empty;

            var display = !string.IsNullOrWhiteSpace(shift.Code) ? shift.Code.Trim() : shift.Label?.Trim();
            return string.IsNullOrWhiteSpace(display) ? string.Empty : $"-- {display} --";
        }

        // Standard 8-hour day used to express credited shift hours as day-equivalents on the DTR
        public const decimal StandardCreditedDayHours = 8m;

        // Leave types with credited hours (VL, SL) count toward the DTR total.
        // DAY OFF, HOL, and other 0-hour leave types do not.
        public static bool CountsTowardDtrTotal(ShiftType? shift)
        {
            if (IsUnassigned(shift))
                return false;

            if (!shift!.IsLeave)
                return true;

            return shift.Hours > 0;
        }

        public static decimal GetCreditedDays(ShiftType? shift)
        {
            if (!CountsTowardDtrTotal(shift))
                return 0m;

            return shift!.Hours / StandardCreditedDayHours;
        }

        public static string FormatTime(TimeSpan? time) =>
            time.HasValue ? DateTime.Today.Add(time.Value).ToString("hh:mm tt") : "—";

        public static string FormatTimeRange(ShiftType shift)
        {
            if (shift.IsLeave)
                return "—";

            if (shift.IsFlexible || !shift.StartTime.HasValue || !shift.EndTime.HasValue)
                return shift.Hours > 0 ? $"{shift.Hours:N2}h (flex)" : "—";

            var start = FormatTime24(shift.StartTime);
            var end = FormatTime24(shift.EndTime);
            var suffix = shift.SpansNextDay ? " (+1)" : "";
            return $"{start}–{end}{suffix}";
        }

        public static string FormatTimeRange(ShiftTypeViewModel model)
        {
            if (model.IsLeave)
                return "—";

            if (model.IsFlexible || !model.StartTime.HasValue || !model.EndTime.HasValue)
                return model.Hours > 0 ? $"{model.Hours:N2}h (flex)" : "—";

            var start = FormatTime24(model.StartTime);
            var end = FormatTime24(model.EndTime);
            var suffix = model.SpansNextDay ? " (+1)" : "";
            return $"{start}–{end}{suffix}";
        }

        public static decimal? SuggestCreditedHours(
            TimeSpan? startTime,
            TimeSpan? endTime,
            bool spansNextDay,
            bool hasLunchBreak,
            TimeSpan? lunchStart,
            TimeSpan? lunchEnd)
        {
            if (!startTime.HasValue || !endTime.HasValue)
                return null;

            var spanMinutes = GetShiftSpanMinutes(startTime.Value, endTime.Value, spansNextDay);
            if (spanMinutes <= 0)
                return null;

            if (hasLunchBreak && lunchStart.HasValue && lunchEnd.HasValue)
            {
                var lunchMinutes = (int)(lunchEnd.Value - lunchStart.Value).TotalMinutes;
                if (lunchMinutes > 0)
                    spanMinutes -= lunchMinutes;
            }

            if (spanMinutes <= 0)
                return null;

            return Math.Round(spanMinutes / 60m, 2);
        }

        public static List<string> ValidateSchedule(ShiftTypeViewModel model)
        {
            var errors = new List<string>();

            if (model.IsLeave)
                return errors;

            if (model.IsFlexible)
                return errors;

            if (!model.StartTime.HasValue || !model.EndTime.HasValue)
            {
                errors.Add("Start time and end time are required for working shifts (or mark as Flexible).");
                return errors;
            }

            var start = model.StartTime.Value;
            var end = model.EndTime.Value;

            if (end <= start && !model.SpansNextDay)
                errors.Add("End time must be after start time, or enable Spans Next Day for overnight shifts.");

            if (model.SpansNextDay && end > start)
                errors.Add("Spans Next Day is only needed when the shift crosses midnight.");

            if (model.GracePeriodMinutes is < 0 or > 180)
                errors.Add("Grace period must be between 0 and 180 minutes.");

            if (model.HasLunchBreak)
            {
                if (!model.LunchStart.HasValue || !model.LunchEnd.HasValue)
                {
                    errors.Add("Lunch start and end are required when lunch break is enabled.");
                }
                else if (model.LunchEnd <= model.LunchStart)
                {
                    errors.Add("Lunch end must be after lunch start.");
                }
            }

            if (model.Hours < 0 || model.Hours > 24)
                errors.Add("Credited hours must be between 0 and 24.");

            return errors;
        }

        public static void ApplyLeaveDefaults(ShiftTypeViewModel model)
        {
            if (!model.IsLeave)
                return;

            model.IsFlexible = false;
            model.StartTime = null;
            model.EndTime = null;
            model.SpansNextDay = false;
            model.GracePeriodMinutes = null;
            model.HasLunchBreak = false;
            model.LunchStart = null;
            model.LunchEnd = null;
        }

        private static string FormatTime24(TimeSpan? time) =>
            time.HasValue ? time.Value.ToString(@"hh\:mm") : "";

        private static int GetShiftSpanMinutes(TimeSpan start, TimeSpan end, bool spansNextDay)
        {
            if (spansNextDay)
                return (int)((TimeSpan.FromDays(1) - start + end).TotalMinutes);

            return (int)(end - start).TotalMinutes;
        }
    }
}
