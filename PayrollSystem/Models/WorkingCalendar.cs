using System.ComponentModel.DataAnnotations;

namespace PayrollSystem.Models
{
    // Global company calendar — one row per date. 
    public class WorkingCalendar : UserActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateOnly Date { get; set; }

        [Required]
        public int DayTypeId { get; set; }
        public virtual DayType DayType { get; set; } = null!;

        public int? HolidayId { get; set; }
        public virtual Holiday? Holiday { get; set; }

        public bool IsWeekend { get; set; }

        public bool IsWorkingDay { get; set; } = true;

        // When true, regenerate/year tools will not overwrite this row
        public bool IsLocked { get; set; }

        [MaxLength(500)]
        public string? Remarks { get; set; }
    }
}
