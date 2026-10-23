namespace PayrollSystem.Models
{
    public class ShiftType
    {
        public int Id { get; set; }

        // Short code shown in schedule cell, e.g. "REG" or "7AM-3PM"
        public string Code { get; set; } = string.Empty;

        // readable label for dropdowns
        public string Label { get; set; } = string.Empty;

        // Working hours credited when this shift is used (payroll / schedule totals)
        public decimal Hours { get; set; }

        // True for DAY OFF, HOLIDAY, SPL, SL, VL — counted as Leave/OFF
        public bool IsLeave { get; set; }

        // Hex color used in the UI, e.g. "#2563eb"
        public string ColorHex { get; set; } = "#6b7280";

        // Sort order in dropdowns
        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;

        // When true, credit Hours only — no fixed window for attendance (optional).
        public bool IsFlexible { get; set; }

        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }

        // Required when EndTime is earlier than StartTime (e.g. 19:00 → 07:00)
        public bool SpansNextDay { get; set; }

        // Null = use PayrollConfiguration.DefaultGracePeriodMinutes
        public int? GracePeriodMinutes { get; set; }

        public bool HasLunchBreak { get; set; }
        public TimeSpan? LunchStart { get; set; }
        public TimeSpan? LunchEnd { get; set; }

        // When false (default), lunch/break duration is unpaid (excluded from ND / flexible netting).
        // Credited Worked hours use ShiftType.Hours and are not derived from punch span.
        public bool IsBreakPaid { get; set; }

        public virtual ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();
    }
}
