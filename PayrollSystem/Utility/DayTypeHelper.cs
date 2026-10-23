using PayrollSystem.Models;

namespace PayrollSystem.Utility
{
    // Day Type classification helpers used across attendance, overtime, time-card import, and payroll.
    // Employee Day Type (standard business rule) — use
    // Working Calendar weekday rows alone must not decide Rest Day for rostered weekend work.

    // Calendar-only is for generating/filling the Working Calendar
    // (holiday → weekend → regular). Do not use it alone to classify an employee's payroll hours.

    public static class DayTypeHelper
    {
        public static DayType? GetDayTypeForDate(DateOnly date, List<DayType> allDayTypes) =>
            GetDayTypeForDate(date, allDayTypes, holidays: null, weekendIsDayOff: true);

        public static DayType? GetDayTypeForDate(
            DateOnly date,
            List<DayType> allDayTypes,
            IEnumerable<Holiday>? holidays,
            bool weekendIsDayOff = true)
        {
            if (allDayTypes == null || allDayTypes.Count == 0)
                return null;

            var active = allDayTypes.Where(d => d.IsActive).ToList();

            if (holidays != null)
            {
                var holiday = holidays
                    .Where(h => h.IsActive && !h.IsDeleted)
                    .Where(h => h.Date == date || (h.IsRecurring && h.Date.Month == date.Month && h.Date.Day == date.Day))
                    .OrderByDescending(h => !h.IsRecurring)
                    .ThenByDescending(h => h.Date)
                    .FirstOrDefault();

                if (holiday != null)
                {
                    var holidayDayType = active.FirstOrDefault(d => d.Id == holiday.DayTypeId)
                                         ?? holiday.DayType;
                    if (holidayDayType != null)
                        return holidayDayType;
                }
            }

            if (weekendIsDayOff)
            {
                if (date.DayOfWeek == DayOfWeek.Saturday)
                {
                    return Find(active, "SAT", "Saturday")
                           ?? Find(active, "REST-DAY", "Rest Day")
                           ?? Find(active, "REG-DAY", "Regular Day");
                }

                if (date.DayOfWeek == DayOfWeek.Sunday)
                {
                    return Find(active, "SUN", "Sunday")
                           ?? Find(active, "REST-DAY", "Rest Day")
                           ?? Find(active, "REG-DAY", "Regular Day");
                }
            }

            return Find(active, "REG-DAY", "Regular Day");
        }

        // Standard employee Day Type resolution for attendance, overtime, time-card import, and payroll.
        public static DayType? ResolveDayType(
            ShiftType? assignedShift,
            DayType? calendarDayType,
            bool isHoliday,
            IReadOnlyList<DayType> allDayTypes) =>
            ResolveForEmployeeSchedule(assignedShift, calendarDayType, isHoliday, allDayTypes);

        // Standard employee Day Type resolution for attendance, overtime, time-card import, and payroll.
        public static DayType? ResolveForEmployeeSchedule(
            ShiftType? assignedShift,
            DayType? calendarDayType,
            bool isHoliday,
            IReadOnlyList<DayType> allDayTypes)
        {
            // Day Type precedence:
            //
            // 1. Holiday from Working Calendar
            // 2. Employee's assigned working shift -> Regular Day
            // 3. Employee's assigned Day Off shift -> Rest Day
            // 4. Calendar fallback when no schedule exists
            //
            // NOTE:
            // Saturday/Sunday do NOT automatically mean Rest Day.
            // Employees may have rotating or weekend shifts.

            if (allDayTypes == null || allDayTypes.Count == 0)
                return calendarDayType;

            var active = allDayTypes.Where(d => d.IsActive && !d.IsDeleted).ToList();
            if (active.Count == 0)
                active = allDayTypes.Where(d => d.IsActive).ToList();

            var regular = Find(active, "REG-DAY", "Regular Day");
            var rest = Find(active, "REST-DAY", "Rest Day");

            // 1. Holiday from Working Calendar
            if (isHoliday || IsHolidayDayType(calendarDayType))
                return calendarDayType ?? FindHolidayFallback(active) ?? regular;

            // 2. Employee's assigned working shift -> Regular Day
            if (IsWorkingShift(assignedShift))
                return regular ?? calendarDayType;

            // 3. Employee's assigned Day Off / Rest shift -> Rest Day
            if (IsRestOrDayOffShift(assignedShift))
                return rest ?? calendarDayType;

            // Paid leave with credited hours (VL/SL) — not rest-day premium
            if (assignedShift is { IsLeave: true, Hours: > 0 })
                return regular ?? calendarDayType;

            // 4. Calendar fallback when no schedule exists
            return calendarDayType ?? regular;
        }

        public static bool IsHolidayDayType(DayType? dayType)
        {
            if (dayType == null)
                return false;

            var code = dayType.Code ?? string.Empty;
            var name = dayType.Name ?? string.Empty;

            if (name.Contains("Holiday", StringComparison.OrdinalIgnoreCase))
                return true;

            if (code.Contains("HOL", StringComparison.OrdinalIgnoreCase))
                return true;

            return code.Equals("RH", StringComparison.OrdinalIgnoreCase)
                   || code.Equals("SNWH", StringComparison.OrdinalIgnoreCase)
                   || code.Equals("REG-HOL", StringComparison.OrdinalIgnoreCase)
                   || code.Equals("SPC-HOL", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsWorkingShift(ShiftType? shift) =>
            !ShiftTypeScheduleHelper.IsUnassigned(shift) && shift is { IsLeave: false };

        // Day Off / Rest / other zero-hour leave used as the employee's scheduled rest day.
        public static bool IsRestOrDayOffShift(ShiftType? shift)
        {
            if (ShiftTypeScheduleHelper.IsUnassigned(shift) || shift is not { IsLeave: true })
                return false;

            if (shift.Hours <= 0)
                return true;

            var code = shift.Code ?? string.Empty;
            var label = shift.Label ?? string.Empty;

            return ContainsRestOrDayOffToken(code) || ContainsRestOrDayOffToken(label);
        }

        private static bool ContainsRestOrDayOffToken(string value) =>
            value.Contains("DAY OFF", StringComparison.OrdinalIgnoreCase)
            || value.Contains("DAYOFF", StringComparison.OrdinalIgnoreCase)
            || value.Contains("REST", StringComparison.OrdinalIgnoreCase)
            || value.Equals("OFF", StringComparison.OrdinalIgnoreCase);

        private static DayType? FindHolidayFallback(IEnumerable<DayType> dayTypes) =>
            Find(dayTypes, "REG-HOL", "Regular Holiday")
            ?? Find(dayTypes, "SPC-HOL", "Special Holiday")
            ?? dayTypes.FirstOrDefault(IsHolidayDayType);

        private static DayType? Find(IEnumerable<DayType> dayTypes, string code, string name) =>
            dayTypes.FirstOrDefault(d =>
                string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
