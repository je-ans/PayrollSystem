using PayrollSystem.Models;

namespace PayrollSystem.Models
{
    public class ScheduleEntry
    {
        public int Id { get; set; }

        public int WorkScheduleId { get; set; }
        public virtual WorkSchedule WorkSchedule { get; set; } = null!;

        public Guid EmployeeId { get; set; }
        public virtual Employee Employee { get; set; } = null!;

        public int Day { get; set; }

        public int ShiftTypeId { get; set; }
        public virtual ShiftType ShiftType { get; set; } = null!;

        public string? Remarks { get; set; }

        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}