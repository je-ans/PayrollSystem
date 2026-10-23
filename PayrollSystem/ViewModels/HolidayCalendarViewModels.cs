using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class HolidayListItemViewModel
    {
        public int Id { get; set; }
        public DateOnly Date { get; set; }
        public string Name { get; set; } = string.Empty;
        public HolidayType HolidayType { get; set; }
        public string DayTypeName { get; set; } = string.Empty;
        public string DayTypeCode { get; set; } = string.Empty;
        public bool IsRecurring { get; set; }
        public bool IsActive { get; set; }
    }

    public class HolidayFormViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required]
        [MaxLength(150)]
        [Display(Name = "Holiday Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Holiday Type")]
        public HolidayType HolidayType { get; set; } = HolidayType.Regular;

        [Required]
        [Display(Name = "Day Type")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a day type.")]
        public int DayTypeId { get; set; }

        [Display(Name = "Recurring annually")]
        public bool IsRecurring { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        public string? DayTypeName { get; set; }
        public string? DayTypeCode { get; set; }

        public List<SelectListItem> DayTypeOptions { get; set; } = new();
    }

    public class WorkingCalendarMonthViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public List<WorkingCalendarDayViewModel> Days { get; set; } = new();
        public List<HolidayListItemViewModel> YearHolidays { get; set; } = new();
        public int HolidayCount { get; set; }
        public int NonWorkingCount { get; set; }
        public int WorkingCount { get; set; }
        public int YearHolidayCount { get; set; }
    }

    public class WorkingCalendarDayViewModel
    {
        public DateOnly Date { get; set; }
        public string DayTypeCode { get; set; } = string.Empty;
        public string DayTypeName { get; set; } = string.Empty;
        public string? HolidayName { get; set; }
        public HolidayType? HolidayType { get; set; }
        public bool IsWeekend { get; set; }
        public bool IsWorkingDay { get; set; }
        public bool IsLocked { get; set; }
        public bool IsHoliday => !string.IsNullOrWhiteSpace(HolidayName);
        public string? Remarks { get; set; }
    }

    public class WorkingCalendarGenerateViewModel
    {
        [Required]
        [Range(2000, 2100)]
        [Display(Name = "Year")]
        public int Year { get; set; } = DateTime.Today.Year;

        [Display(Name = "Overwrite unlocked days")]
        public bool OverwriteUnlocked { get; set; } = true;
    }
}
