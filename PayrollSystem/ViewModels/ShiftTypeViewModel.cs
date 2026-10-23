using System.ComponentModel.DataAnnotations;
using PayrollSystem.Utility;

namespace PayrollSystem.ViewModels
{
    public class ShiftTypeViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Code is required")]
        [StringLength(50)]
        [Display(Name = "Code")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Label is required")]
        [StringLength(100)]
        [Display(Name = "Label")]
        public string Label { get; set; } = string.Empty;

        [Required(ErrorMessage = "Hours is required")]
        [Range(0, 24, ErrorMessage = "Credited hours must be between 0 and 24")]
        [Display(Name = "Credited Hours")]
        public decimal Hours { get; set; }

        [Display(Name = "Is Leave / Off")]
        public bool IsLeave { get; set; }

        [Display(Name = "Flexible (no fixed window)")]
        public bool IsFlexible { get; set; }

        [Display(Name = "Start Time")]
        public TimeSpan? StartTime { get; set; }

        [Display(Name = "End Time")]
        public TimeSpan? EndTime { get; set; }

        [Display(Name = "Spans Next Day")]
        public bool SpansNextDay { get; set; }

        [Range(0, 180, ErrorMessage = "Grace period must be between 0 and 180 minutes")]
        [Display(Name = "Grace Period (minutes)")]
        public int? GracePeriodMinutes { get; set; }

        [Display(Name = "Has Lunch Break")]
        public bool HasLunchBreak { get; set; }

        [Display(Name = "Break is paid")]
        public bool IsBreakPaid { get; set; }

        [Display(Name = "Lunch Start")]
        public TimeSpan? LunchStart { get; set; }

        [Display(Name = "Lunch End")]
        public TimeSpan? LunchEnd { get; set; }

        [Required(ErrorMessage = "Color is required")]
        [StringLength(10)]
        [Display(Name = "Color")]
        public string ColorHex { get; set; } = "#6b7280";

        [Display(Name = "Sort Order")]
        public int SortOrder { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        public string ScheduleDisplay => ShiftTypeScheduleHelper.FormatTimeRange(this);
    }
}
