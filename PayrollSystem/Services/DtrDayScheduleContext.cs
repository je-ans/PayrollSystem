namespace PayrollSystem.Services
{
    // Resolved expected schedule for one DTR day (from assigned work-schedule ShiftType).
    public sealed class DtrDayScheduleContext
    {
        public bool SkipAttendanceAssessment { get; init; }
        public bool UsesShiftTypeSchedule { get; init; }
        public int GracePeriodMinutes { get; init; }
        public TimeOnly ScheduledIn { get; init; }
        public TimeOnly ScheduledOut { get; init; }
        public bool SpansNextDay { get; init; }
        public bool HasLunchBreak { get; init; }
        public TimeOnly? ScheduledLunchOut { get; init; }
        public TimeOnly? ScheduledLunchIn { get; init; }
        public int RequiredWorkMinutes { get; init; }
    }
}
