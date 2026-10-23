using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class PayrollConfigurationViewModel
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Display(Name = "Configuration Name")]
        public string Name { get; set; } = "Default Payroll Configuration";

        // ========== RATE CALCULATIONS ==========
        [Required]
        [Range(1, 31)]
        [Display(Name = "Working Days Per Month")]
        public int WorkingDaysPerMonth { get; set; } = 22;

        [Required]
        [Range(1, 24)]
        [Display(Name = "Working Hours Per Day")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal WorkingHoursPerDay { get; set; } = 8;

        [Required]
        [Display(Name = "Default Work Start Time")]
        public TimeSpan DefaultWorkStartTime { get; set; } = TimeSpan.FromHours(8);

        [Required]
        [Display(Name = "Default Work End Time")]
        public TimeSpan DefaultWorkEndTime { get; set; } = TimeSpan.FromHours(17);

        [Required]
        [Range(0, 120)]
        [Display(Name = "Attendance Grace Period (minutes)")]
        public int DefaultGracePeriodMinutes { get; set; } = 15;

        [Required]
        [Range(0, 180)]
        [Display(Name = "Default Break Duration (minutes)")]
        public int DefaultBreakDurationMinutes { get; set; } = 60;

        [Required]
        [Range(1, 8)]
        [Display(Name = "Half Day Threshold (hours)")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DefaultHalfDayHours { get; set; } = 4;

        [Required]
        [Range(1, 120)]
        [Display(Name = "Auto Pairing Window (minutes)")]
        public int DefaultAutomaticPairingWindowMinutes { get; set; } = 15;

        [Display(Name = "Weekend Is Day Off")]
        public bool UseWeekendAsDayOff { get; set; } = true;

        [Display(Name = "Night Differential Start Time")]
        public TimeSpan NightDifferentialStartTime { get; set; } = TimeSpan.FromHours(22);

        [Display(Name = "Night Differential End Time")]
        public TimeSpan NightDifferentialEndTime { get; set; } = TimeSpan.FromHours(6);

        [Range(0, 480)]
        [Display(Name = "Minimum Overtime Minutes")]
        public int MinimumOvertimeMinutes { get; set; } = 30;

        [Display(Name = "Overtime Rounding Rule")]
        public OvertimeRoundingRule OvertimeRoundingRule { get; set; } = OvertimeRoundingRule.None;

        [Display(Name = "Require Overtime Approval")]
        public bool RequireOvertimeApproval { get; set; } = true;

        // ========== TABLE LOOKUPS ==========
        [Required]
        [Range(2000, 2100)]
        [Display(Name = "Tax Table Year")]
        public int TaxTableYear { get; set; } = DateTime.Now.Year;

        [Required]
        [Range(2000, 2100)]
        [Display(Name = "SSS Table Year")]
        public int SSSTableYear { get; set; } = DateTime.Now.Year;

        [Required]
        [Range(2000, 2100)]
        [Display(Name = "PhilHealth Table Year")]
        public int PhilHealthTableYear { get; set; } = DateTime.Now.Year;

        [Required]
        [Range(2000, 2100)]
        [Display(Name = "Pag-IBIG Table Year")]
        public int PagIbigTableYear { get; set; } = DateTime.Now.Year;

        // ========== COMPANY PROFILE ==========
        [MaxLength(200)]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = "";

        [MaxLength(50)]
        [Display(Name = "Short Name / Trade Name")]
        public string? CompanyShortName { get; set; }

        [MaxLength(500)]
        [Display(Name = "Company Description")]
        public string? CompanyDescription { get; set; }

        // Existing logo path stored in DB (read-only display)
        [Display(Name = "Current Logo")]
        public string? CompanyLogoPath { get; set; }

        // New logo file uploaded by the user
        [Display(Name = "Company Logo")]
        public IFormFile? CompanyLogoFile { get; set; }

        [MaxLength(200)]
        [Display(Name = "Address Line 1")]
        public string? CompanyAddress1 { get; set; }

        [MaxLength(200)]
        [Display(Name = "Address Line 2")]
        public string? CompanyAddress2 { get; set; }

        [MaxLength(100)]
        [Display(Name = "City")]
        public string? CompanyCity { get; set; }

        [MaxLength(100)]
        [Display(Name = "Province / State")]
        public string? CompanyProvince { get; set; }

        [MaxLength(20)]
        [Display(Name = "Zip Code")]
        public string? CompanyZipCode { get; set; }

        [MaxLength(50)]
        [Display(Name = "Contact Number")]
        public string? CompanyContactNumber { get; set; }

        [MaxLength(150)]
        [EmailAddress]
        [Display(Name = "Company Email")]
        public string? CompanyEmail { get; set; }

        [MaxLength(200)]
        [Display(Name = "Website")]
        public string? CompanyWebsite { get; set; }

        // ========== GOVERNMENT NUMBERS ==========
        [MaxLength(100)]
        [Display(Name = "Company TIN")]
        public string? CompanyTIN { get; set; }

        [MaxLength(50)]
        [Display(Name = "SSS Number")]
        public string? CompanySSSNumber { get; set; }

        [MaxLength(50)]
        [Display(Name = "PhilHealth Number")]
        public string? CompanyPhilHealthNumber { get; set; }

        [MaxLength(50)]
        [Display(Name = "Pag-IBIG Number")]
        public string? CompanyPagIbigNumber { get; set; }

        // ========== AUDIT ==========
        [Required]
        [Display(Name = "Effective Date")]
        public DateTime EffectiveDate { get; set; } = DateTime.Now;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;
    }
}