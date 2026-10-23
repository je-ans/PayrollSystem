using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class Holiday : UserActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateOnly Date { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        public HolidayType HolidayType { get; set; } = HolidayType.Regular;

        [Required]
        public int DayTypeId { get; set; }
        public virtual DayType DayType { get; set; } = null!;

        // When true, applies every year on the same month/day.
        public bool IsRecurring { get; set; }

        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        public string? Description { get; set; }
    }
}
