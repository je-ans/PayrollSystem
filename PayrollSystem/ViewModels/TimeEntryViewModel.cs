using System;
using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class TimeEntryViewModel
    {
        public Guid? Id { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public Guid? EmployeeId { get; set; }

        [Required]
        [Display(Name = "Entry Type")]
        public EntryType EntryType { get; set; }

        [Required]
        [Display(Name = "Entry Date & Time")]
        [DataType(DataType.DateTime)]
        public DateTime? EntryDateTime { get; set; }

        [MaxLength(50)]
        [Display(Name = "Biometric Device ID")]
        public string? BiometricDeviceId { get; set; }

        [MaxLength(100)]
        [Display(Name = "Location")]
        public string? Location { get; set; }

        [MaxLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Is Manual Entry")]
        public bool IsManualEntry { get; set; } = true;
    }
}

