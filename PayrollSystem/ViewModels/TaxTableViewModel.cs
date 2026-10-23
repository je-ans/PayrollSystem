using System;
using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class TaxTableViewModel
    {
        public int Id { get; set; }

        // ── Identification ────────────────────────────
        [Required(ErrorMessage = "Tax Table Name is required.")]
        [MaxLength(100)]
        [Display(Name = "Tax Table Name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [MaxLength(10)]
        [Display(Name = "Tax Year")]
        public string? TaxYear { get; set; }

        [Required(ErrorMessage = "Bracket Type is required.")]
        [Display(Name = "Bracket Type")]
        public TaxBracketType BracketType { get; set; }

        [Required(ErrorMessage = "Income Basis is required.")]
        [Display(Name = "Income Basis")]
        public IncomeBasis IncomeBasis { get; set; } = IncomeBasis.SemiMonthly;

        [Display(Name = "Bracket Order")]
        public int BracketOrder { get; set; }

        // ── Income Range ──────────────────────────────
        [Display(Name = "Minimum Income")]
        public decimal MinIncome { get; set; } = 0;

        [Display(Name = "Maximum Income")]
        public decimal? MaxIncome { get; set; }

        // ── Rate / Amount Fields ──────────────────────
        //Stored as a fraction: 0.20 = 20%
        [Display(Name = "Tax Rate (e.g. 0.20 = 20%)")]
        public decimal? TaxRate { get; set; }

        [Display(Name = "Fixed Amount")]
        public decimal? FixedAmount { get; set; }

        [Display(Name = "Base Tax Amount")]
        public decimal BaseTaxAmount { get; set; } = 0;

        // ── Effectivity ───────────────────────────────
        [Required(ErrorMessage = "Effective Start Date is required.")]
        [Display(Name = "Effective Start Date")]
        public DateTime EffectiveStartDate { get; set; }

        [Display(Name = "Effective End Date")]
        public DateTime? EffectiveEndDate { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [MaxLength(100)]
        [Display(Name = "Legal Reference")]
        public string? Reference { get; set; }
    }
}