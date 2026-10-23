using System;
using System.ComponentModel.DataAnnotations;

namespace PayrollSystem.Models
{
    public class BiometricLog
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Display(Name = "Employee")]
        public Guid EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        [Required]
        [Display(Name = "Timestamp")]
        public DateTime Timestamp { get; set; }

        [Required]
        [MaxLength(50)]
        [Display(Name = "Device ID")]
        public string DeviceId { get; set; }

        [MaxLength(100)]
        [Display(Name = "Location")]
        public string? Location { get; set; }

        [MaxLength(50)]
        [Display(Name = "Biometric ID")]
        public string? BiometricId { get; set; }

        [MaxLength(100)]
        [Display(Name = "Raw Data")]
        public string? RawData { get; set; }

        // Processing metadata
        [Display(Name = "Imported At")]
        public DateTime ImportedAt { get; set; } = DateTime.Now;

        [Display(Name = "Imported By")]
        public string? ImportedBy { get; set; }

        [Display(Name = "Is Processed")]
        public bool IsProcessed { get; set; } = false;

        [Display(Name = "Processed At")]
        public DateTime? ProcessedAt { get; set; }

        // Link to processed entry
        [Display(Name = "Time Entry ID")]
        public Guid? TimeEntryId { get; set; }
        public virtual TimeEntry? TimeEntry { get; set; }

        // Error tracking
        [Display(Name = "Has Error")]
        public bool HasError { get; set; } = false;

        [MaxLength(500)]
        [Display(Name = "Error Message")]
        public string? ErrorMessage { get; set; }
    }
}