using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class TimeEntry
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Display(Name = "Employee")]
        public Guid EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        [Required]
        [Display(Name = "Entry Type")]
        public EntryType EntryType { get; set; }

        [Required]
        [Display(Name = "Entry Date")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateOnly EntryDate { get; set; }

        [Required]
        [Display(Name = "Entry Time")]
        [DataType(DataType.Time)]
        [DisplayFormat(DataFormatString = "{0:HH:mm}", ApplyFormatInEditMode = true)]
        public TimeOnly EntryTime { get; set; }

        // Source tracking
        [Required]
        [Display(Name = "Entry Source")]
        public EntrySource Source { get; set; } = EntrySource.Biometric;

        [Display(Name = "Biometric Log ID")]
        public Guid? BiometricLogId { get; set; }
        public virtual BiometricLog? BiometricLog { get; set; }

        [MaxLength(50)]
        [Display(Name = "Biometric Device ID")]
        public string? BiometricDeviceId { get; set; }

        [MaxLength(100)]
        [Display(Name = "Location")]
        public string? Location { get; set; }

        // Pairing for attendance calculation
        [Display(Name = "Paired Entry ID")]
        public Guid? PairedEntryId { get; set; }

        [ForeignKey("PairedEntryId")]
        public virtual TimeEntry? PairedEntry { get; set; }

        // Validation and status
        [Display(Name = "Status")]
        public EntryStatus Status { get; set; } = EntryStatus.Valid;

        [MaxLength(500)]
        [Display(Name = "Validation Notes")]
        public string? ValidationNotes { get; set; }

        // Manual entry tracking
        [Display(Name = "Is Manual Entry")]
        public bool IsManualEntry { get; set; } = false;

        [MaxLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        // Audit fields
        [Display(Name = "Created By")]
        public string? CreatedBy { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "Modified By")]
        public string? ModifiedBy { get; set; }

        [Display(Name = "Modified Date")]
        public DateTime? ModifiedDate { get; set; }

        // Computed properties (setter used for form binding from datetime-local)
        [NotMapped]
        public DateTime EntryDateTime
        {
            get => EntryDate.ToDateTime(EntryTime);
            set
            {
                EntryDate = DateOnly.FromDateTime(value);
                EntryTime = TimeOnly.FromDateTime(value);
            }
        }

        // Helper methods
        public bool IsWithinGracePeriod(TimeSpan scheduledTime, int gracePeriodMinutes)
        {
            var entryTimeSpan = EntryTime.ToTimeSpan();
            var gracePeriod = TimeSpan.FromMinutes(gracePeriodMinutes);

            // Handle overnight shifts
            if (scheduledTime > TimeSpan.FromHours(12) && entryTimeSpan < TimeSpan.FromHours(12))
            {
                entryTimeSpan = entryTimeSpan.Add(TimeSpan.FromHours(24));
            }

            return Math.Abs((entryTimeSpan - scheduledTime).TotalMinutes) <= gracePeriod.TotalMinutes;
        }
    }
}
