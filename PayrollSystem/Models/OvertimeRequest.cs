using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PayrollSystem.Models.Workflow;

namespace PayrollSystem.Models
{
    // Ahead-of-time overtime authorization for an employee over a date range
    // with a planned daily time window. Payable OT comes from approved requests, not DTR eligibility.
    public class OvertimeRequest : UserActivity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid EmployeeId { get; set; }
        public virtual Employee Employee { get; set; } = null!;

        [Required]
        [DataType(DataType.Date)]
        public DateOnly StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateOnly EndDate { get; set; }

        [Required]
        public TimeOnly RequestedStartTime { get; set; }

        [Required]
        public TimeOnly RequestedEndTime { get; set; }

        // When true, unpaid lunch is deducted using <see cref="LunchStart"/> / <see cref="LunchEnd"/>
        public bool HasLunchBreak { get; set; }

        public TimeOnly? LunchStart { get; set; }

        public TimeOnly? LunchEnd { get; set; }

        // Cached lunch duration (minutes) for reporting; derived from lunch times when set
        [Range(0, 24 * 60)]
        public int BreakMinutes { get; set; }

        public TimeOnly? ApprovedStartTime { get; set; }

        public TimeOnly? ApprovedEndTime { get; set; }

        public bool? ApprovedHasLunchBreak { get; set; }

        public TimeOnly? ApprovedLunchStart { get; set; }

        public TimeOnly? ApprovedLunchEnd { get; set; }

        public int? ApprovedBreakMinutes { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Reason { get; set; } = string.Empty;

        public OvertimeStatus Status { get; set; } = OvertimeStatus.Pending;

        [MaxLength(1000)]
        public string? ReviewNotes { get; set; }

        [MaxLength(256)]
        public string? ReviewedById { get; set; }
        public virtual ApplicationUser? ReviewedBy { get; set; }

        public DateTime? ReviewedOn { get; set; }

        [MaxLength(256)]
        public string? SubmittedBy { get; set; }

        public DateTime SubmittedOn { get; set; } = DateTime.Now;

        public int? ApprovalRequestId { get; set; }
        public virtual ApprovalRequest? ApprovalRequest { get; set; }

        public bool IsProcessedToPayroll { get; set; }

        public DateTime? ProcessedToPayrollOn { get; set; }

        [MaxLength(256)]
        public string? ProcessedToPayrollBy { get; set; }

        public int? PayrollPeriodId { get; set; }
        public virtual PayrollPeriod? PayrollPeriod { get; set; }

        public int? DayTypeId { get; set; }
        public virtual DayType? DayType { get; set; }

        [NotMapped]
        public int DayCount => Math.Max(0, EndDate.DayNumber - StartDate.DayNumber + 1);

        [NotMapped]
        public bool EffectiveHasLunchBreak =>
            ApprovedHasLunchBreak ?? HasLunchBreak;

        [NotMapped]
        public TimeOnly? EffectiveLunchStart =>
            ApprovedLunchStart ?? LunchStart;

        [NotMapped]
        public TimeOnly? EffectiveLunchEnd =>
            ApprovedLunchEnd ?? LunchEnd;

        [NotMapped]
        public int EffectiveBreakMinutes =>
            ResolveBreakMinutes(
                EffectiveHasLunchBreak,
                EffectiveLunchStart,
                EffectiveLunchEnd,
                ApprovedBreakMinutes ?? BreakMinutes);

        [NotMapped]
        public decimal HoursPerDay =>
            ComputeHours(RequestedStartTime, RequestedEndTime, HasLunchBreak, LunchStart, LunchEnd, BreakMinutes);

        [NotMapped]
        public decimal GrossHoursPerDay =>
            ComputeGrossHours(RequestedStartTime, RequestedEndTime);

        [NotMapped]
        public decimal RequestedHours => HoursPerDay * DayCount;

        [NotMapped]
        public decimal? ApprovedHoursPerDay =>
            ApprovedStartTime.HasValue && ApprovedEndTime.HasValue
                ? ComputeHours(
                    ApprovedStartTime.Value,
                    ApprovedEndTime.Value,
                    EffectiveHasLunchBreak,
                    EffectiveLunchStart,
                    EffectiveLunchEnd,
                    EffectiveBreakMinutes)
                : null;

        [NotMapped]
        public decimal? ApprovedHours =>
            ApprovedHoursPerDay.HasValue ? ApprovedHoursPerDay.Value * DayCount : null;

        [NotMapped]
        public string? LunchBreakDisplay =>
            FormatLunchBreak(EffectiveHasLunchBreak, EffectiveLunchStart, EffectiveLunchEnd, EffectiveBreakMinutes);

        public bool CoversDate(DateOnly date) => date >= StartDate && date <= EndDate;

        public bool OverlapsRange(DateOnly rangeStart, DateOnly rangeEnd) =>
            StartDate <= rangeEnd && EndDate >= rangeStart;

        public IEnumerable<DateOnly> EnumerateDays()
        {
            for (var d = StartDate; d <= EndDate; d = d.AddDays(1))
                yield return d;
        }

        public IEnumerable<DateOnly> EnumerateDaysInPeriod(DateOnly periodStart, DateOnly periodEnd)
        {
            var from = StartDate > periodStart ? StartDate : periodStart;
            var to = EndDate < periodEnd ? EndDate : periodEnd;
            for (var d = from; d <= to; d = d.AddDays(1))
                yield return d;
        }

        public static string FormatDateRange(DateOnly start, DateOnly end) =>
            start == end
                ? start.ToString("MMM dd, yyyy")
                : $"{start:MMM dd, yyyy} – {end:MMM dd, yyyy}";

        public static string FormatTimeRange(TimeOnly start, TimeOnly end) =>
            $"{start:h:mm tt} – {end:h:mm tt}";

        public static string? FormatLunchBreak(
            bool hasLunchBreak,
            TimeOnly? lunchStart,
            TimeOnly? lunchEnd,
            int fallbackMinutes = 0)
        {
            if (!hasLunchBreak)
                return null;

            if (lunchStart.HasValue && lunchEnd.HasValue)
                return $"{FormatTimeRange(lunchStart.Value, lunchEnd.Value)} ({ComputeLunchBreakMinutes(lunchStart, lunchEnd)} min)";

            return fallbackMinutes > 0 ? $"{fallbackMinutes} min" : null;
        }

        public static int ComputeLunchBreakMinutes(TimeOnly? lunchStart, TimeOnly? lunchEnd)
        {
            if (!lunchStart.HasValue || !lunchEnd.HasValue)
                return 0;

            var startDt = DateTime.Today.Add(lunchStart.Value.ToTimeSpan());
            var endDt = DateTime.Today.Add(lunchEnd.Value.ToTimeSpan());
            if (endDt <= startDt)
                endDt = endDt.AddDays(1);

            return Math.Max(0, (int)Math.Round((endDt - startDt).TotalMinutes, MidpointRounding.AwayFromZero));
        }

        public static int ResolveBreakMinutes(
            bool hasLunchBreak,
            TimeOnly? lunchStart,
            TimeOnly? lunchEnd,
            int fallbackMinutes = 0)
        {
            if (!hasLunchBreak)
                return 0;

            var fromTimes = ComputeLunchBreakMinutes(lunchStart, lunchEnd);
            return fromTimes > 0 ? fromTimes : Math.Max(0, fallbackMinutes);
        }

        public static decimal ComputeGrossHours(TimeOnly start, TimeOnly end)
        {
            var startDt = DateTime.Today.Add(start.ToTimeSpan());
            var endDt = DateTime.Today.Add(end.ToTimeSpan());
            if (endDt <= startDt)
                endDt = endDt.AddDays(1);

            return Math.Round((decimal)(endDt - startDt).TotalHours, 2, MidpointRounding.AwayFromZero);
        }

        public static decimal ComputeHours(
            TimeOnly start,
            TimeOnly end,
            bool hasLunchBreak = false,
            TimeOnly? lunchStart = null,
            TimeOnly? lunchEnd = null,
            int fallbackBreakMinutes = 0)
        {
            var gross = ComputeGrossHours(start, end);
            var breakMinutes = ResolveBreakMinutes(hasLunchBreak, lunchStart, lunchEnd, fallbackBreakMinutes);
            var net = gross - breakMinutes / 60m;
            return Math.Round(Math.Max(0m, net), 2, MidpointRounding.AwayFromZero);
        }

        public static (bool valid, string? error) ValidateLunchBreak(
            TimeOnly otStart,
            TimeOnly otEnd,
            bool hasLunchBreak,
            TimeOnly? lunchStart,
            TimeOnly? lunchEnd)
        {
            if (!hasLunchBreak)
                return (true, null);

            if (!lunchStart.HasValue || !lunchEnd.HasValue)
                return (false, "Lunch start and end are required when overtime includes a lunch break.");

            var lunchMinutes = ComputeLunchBreakMinutes(lunchStart, lunchEnd);
            if (lunchMinutes <= 0)
                return (false, "Lunch end must be after lunch start.");

            var gross = ComputeGrossHours(otStart, otEnd);
            if (lunchMinutes / 60m >= gross)
                return (false, "Lunch break cannot be equal to or longer than the overtime window.");

            return (true, null);
        }
    }
}
