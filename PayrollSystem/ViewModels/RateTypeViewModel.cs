using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class RateTypeViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Rate Type Name is required")]
        [Display(Name = "Rate Type Name")]
        public string Name { get; set; }

        [Display(Name = "Code")]
        public string? Code { get; set; }

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Monthly Salary")]
        public decimal? MonthlySalary { get; set; }

        [Display(Name = "Daily Rate")]
        public decimal? DailyRate { get; set; }

        [Display(Name = "Hourly Rate")]
        public decimal? HourlyRate { get; set; }

        [Required(ErrorMessage = "Rate Basis is required")]
        [Display(Name = "Rate Basis")]
        public PayrollSystem.Models.RateBasis RateBasis { get; set; }

        [Display(Name = "Effective Start Date")]
        public DateTime? EffectiveStartDate { get; set; }

        [Display(Name = "Effective End Date")]
        public DateTime? EffectiveEndDate { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;
    }
}
