using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class TaxTable : UserActivity, IValidatableObject
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Display(Name = "Tax Table Name")]
        public string Name { get; set; }

        [MaxLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [MaxLength(10)]
        [Display(Name = "Tax Year")]
        public string? TaxYear { get; set; }

        // How the tax is calculated for this bracket
        [Required]
        [Display(Name = "Bracket Type")]
        public TaxBracketType BracketType { get; set; }

        // What period MinIncome/MaxIncome values represent.
        [Required]
        [Display(Name = "Income Basis")]
        public IncomeBasis IncomeBasis { get; set; } = IncomeBasis.SemiMonthly;

        [Display(Name = "Bracket Order")]
        public int BracketOrder { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Minimum Income")]
        public decimal MinIncome { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Maximum Income")]
        public decimal? MaxIncome { get; set; }

        // Marginal rate stored as a decimal fraction: 0.20 = 20%.
        // Required for Progressive and Percentage bracket types.
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Tax Rate (e.g. 0.20 = 20%)")]
        public decimal? TaxRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Fixed Amount")]
        public decimal? FixedAmount { get; set; }

        // Base tax applied before the marginal rate.
        // Used with Progressive brackets (BIR: "₱X + Y% of excess over ₱Z").
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Base Tax Amount")]
        public decimal BaseTaxAmount { get; set; } = 0;

        [Required]
        [Display(Name = "Effective Start Date")]
        public DateTime EffectiveStartDate { get; set; }

        [Display(Name = "Effective End Date")]
        public DateTime? EffectiveEndDate { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [MaxLength(100)]
        [Display(Name = "Legal Reference")]
        public string? Reference { get; set; }

        [NotMapped]
        public string IncomeRange => MaxIncome.HasValue
            ? $"₱{MinIncome:N2} – ₱{MaxIncome:N2}"
            : $"₱{MinIncome:N2} and above";

        [NotMapped]
        public string RateDisplay => BracketType switch
        {
            TaxBracketType.Progressive => $"₱{BaseTaxAmount:N2} + {TaxRate * 100:N2}% of excess over ₱{MinIncome:N2}",
            TaxBracketType.Percentage => $"{TaxRate * 100:N4}%",
            TaxBracketType.FixedAmount => FixedAmount.HasValue ? $"₱{FixedAmount:N2}" : "N/A",
            _ => "N/A"
        };

        [NotMapped]
        public bool IsCurrent => IsActive
            && EffectiveStartDate <= DateTime.Now
            && (!EffectiveEndDate.HasValue || EffectiveEndDate.Value >= DateTime.Now);

        // ── Tax Computation Helper ────────────────────
        // Computes withholding tax for a given taxable income.
        // Call only after confirming the income falls within this bracket.
        // TaxRate must be stored as a fraction (0.20 = 20%).
        public decimal ComputeTax(decimal taxableIncome)
        {
            return BracketType switch
            {
                TaxBracketType.Progressive =>
                    BaseTaxAmount + ((taxableIncome - MinIncome) * (TaxRate ?? 0)),

                TaxBracketType.Percentage =>
                    taxableIncome * (TaxRate ?? 0),

                TaxBracketType.FixedAmount =>
                    FixedAmount ?? 0,

                _ => 0
            };
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (MaxIncome.HasValue && MaxIncome.Value <= MinIncome)
                yield return new ValidationResult(
                    "Maximum Income must be greater than Minimum Income.",
                    new[] { nameof(MaxIncome) });

            if (BracketType == TaxBracketType.Progressive || BracketType == TaxBracketType.Percentage)
            {
                if (!TaxRate.HasValue)
                    yield return new ValidationResult(
                        $"Tax Rate is required for {BracketType} bracket type.",
                        new[] { nameof(TaxRate) });
                else if (TaxRate.Value < 0 || TaxRate.Value > 1)
                    yield return new ValidationResult(
                        "Tax Rate must be between 0 and 1 (e.g., 0.20 for 20%).",
                        new[] { nameof(TaxRate) });
            }

            if (BracketType == TaxBracketType.FixedAmount && !FixedAmount.HasValue)
                yield return new ValidationResult(
                    "Fixed Amount is required for FixedAmount bracket type.",
                    new[] { nameof(FixedAmount) });

            if (EffectiveEndDate.HasValue && EffectiveEndDate.Value < EffectiveStartDate)
                yield return new ValidationResult(
                    "Effective End Date cannot be earlier than Effective Start Date.",
                    new[] { nameof(EffectiveEndDate) });
        }
    }
}