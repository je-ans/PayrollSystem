using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class PayrollPeriodViewModel
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Display(Name = "Period Name")]
        public string Name { get; set; }

        [MaxLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Period Start Date")]
        public DateOnly? PeriodStartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Period End Date")]
        public DateOnly? PeriodEndDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Pay Date")]
        public DateOnly? PayDate { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Is Locked")]
        public bool IsLocked { get; set; } = false;

        // Computed properties for UI display
        [Display(Name = "Period")]
        public string PeriodRange => PeriodStartDate.HasValue && PeriodEndDate.HasValue
            ? $"{PeriodStartDate:MMM dd, yyyy} - {PeriodEndDate:MMM dd, yyyy}"
            : "Not set";

        [Display(Name = "Days in Period")]
        public int DaysInPeriod => PeriodEndDate.HasValue && PeriodStartDate.HasValue
            ? PeriodEndDate.Value.DayNumber - PeriodStartDate.Value.DayNumber + 1
            : 0;

        [Display(Name = "First Half of Month")]
        public bool IsFirstHalfOfMonth => PeriodStartDate?.Day <= 15 && PeriodEndDate?.Day <= 15;

        [Display(Name = "Second Half of Month")]
        public bool IsSecondHalfOfMonth => PeriodStartDate?.Day >= 16 && PeriodEndDate?.Day >= 16;

        // Detected frequency (calculated, not stored)
        [Display(Name = "Detected Frequency")]
        public string DetectedFrequency
        {
            get
            {
                return DaysInPeriod switch
                {
                    7 => "Weekly",
                    14 => "Bi-Weekly",
                    >= 15 and <= 16 => "Semi-Monthly",
                    >= 28 and <= 31 => "Monthly",
                    _ => $"Custom ({DaysInPeriod} days)"
                };
            }
        }

        // For dropdowns (if needed for other purposes)
        public IEnumerable<PayrollPeriod>? AvailablePeriods { get; set; }

        // For associating employees with this payroll period
        [Display(Name = "Assigned Employees")]
        public List<Guid>? SelectedEmployeeIds { get; set; } = new List<Guid>();

        public IEnumerable<SelectListItem>? AvailableEmployees { get; set; }
    }
}