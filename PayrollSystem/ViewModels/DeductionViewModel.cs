using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.ViewModels
{
    public class DeductionViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Payroll Entry")]
        public int PayrollEntryId { get; set; }

        [Display(Name = "Deduction Type")]
        public int DeductionTypeId { get; set; }
        public string? DeductionTypeName { get; set; }

        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [MaxLength(200)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Is Mandatory")]
        public bool IsMandatory { get; set; } = false;

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

        // Tax-related fields
        [Display(Name = "Taxable Amount")]
        public decimal? TaxableAmount { get; set; }

        [Display(Name = "Tax Rate")]
        public decimal? TaxRate { get; set; }

        // Loan-related fields
        [Display(Name = "Principal Amount")]
        public decimal? PrincipalAmount { get; set; }

        [Display(Name = "Interest Amount")]
        public decimal? InterestAmount { get; set; }

        [Display(Name = "Balance Remaining")]
        public decimal? BalanceRemaining { get; set; }

        [Display(Name = "Installment Number")]
        public int? InstallmentNumber { get; set; }

        [Display(Name = "Total Installments")]
        public int? TotalInstallments { get; set; }

        // Computed properties
        public string DisplayName => $"{DeductionTypeName ?? "Deduction"} - {Description ?? "N/A"}";
        public bool IsActive => !EndDate.HasValue || EndDate.Value > DateTime.Now;
    }
}