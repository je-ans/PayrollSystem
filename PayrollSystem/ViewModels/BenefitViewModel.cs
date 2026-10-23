using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.ViewModels
{
    public class BenefitViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Payroll Entry")]
        public int PayrollEntryId { get; set; }

        [Display(Name = "Benefit Type")]
        public BenefitType BenefitType { get; set; }

        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [MaxLength(200)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Is Recurring")]
        public bool IsRecurring { get; set; } = true;

        [Display(Name = "Is Taxable")]
        public bool IsTaxable { get; set; } = true;

        [MaxLength(100)]
        [Display(Name = "Reference Number")]
        public string? ReferenceNumber { get; set; }

        [Display(Name = "Effective Date")]
        public DateTime? EffectiveDate { get; set; }

        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }

        [MaxLength(500)]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        // Calculation-related fields
        [Display(Name = "Base Amount")]
        public decimal? BaseAmount { get; set; }

        [Display(Name = "Multiplier")]
        public decimal? Multiplier { get; set; }

        [Display(Name = "Hours/Units")]
        public decimal? HoursOrUnits { get; set; }

        // Overtime-specific fields
        [Display(Name = "Overtime Hours")]
        public decimal? OvertimeHours { get; set; }

        [Display(Name = "Overtime Rate Multiplier")]
        public decimal? OvertimeRateMultiplier { get; set; }

        // Allowance-specific fields
        [MaxLength(100)]
        [Display(Name = "Allowance Type")]
        public string? AllowanceType { get; set; }

        // Bonus-specific fields
        [Display(Name = "Bonus Date")]
        public DateTime? BonusDate { get; set; }

        [MaxLength(200)]
        [Display(Name = "Bonus Reason")]
        public string? BonusReason { get; set; }

        // Computed properties
        public string DisplayName => $"{BenefitType.GetDisplayName()} - {Description ?? "N/A"}";
        public bool IsActive => !EndDate.HasValue || EndDate.Value > DateTime.Now;
        public decimal CalculatedAmount
        {
            get
            {
                if (BaseAmount.HasValue && Multiplier.HasValue)
                    return BaseAmount.Value * Multiplier.Value;
                if (BaseAmount.HasValue && HoursOrUnits.HasValue)
                    return BaseAmount.Value * HoursOrUnits.Value;
                return Amount;
            }
        }
    }
}