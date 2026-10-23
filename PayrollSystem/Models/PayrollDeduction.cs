using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class PayrollDeduction : UserActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Payroll Entry")]
        public int PayrollEntryId { get; set; }
        public virtual PayrollEntry PayrollEntry { get; set; }

        [Required]
        [Display(Name = "Deduction Type")]
        public int DeductionTypeId { get; set; }
        public virtual DeductionType DeductionType { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
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

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Taxable Amount")]
        public decimal? TaxableAmount { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Tax Rate")]
        public decimal? TaxRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Principal Amount")]
        public decimal? PrincipalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Interest Amount")]
        public decimal? InterestAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Balance Remaining")]
        public decimal? BalanceRemaining { get; set; }

        [Display(Name = "Installment Number")]
        public int? InstallmentNumber { get; set; }

        [Display(Name = "Total Installments")]
        public int? TotalInstallments { get; set; }

        [NotMapped]
        public string DisplayName => $"{DeductionType?.Name ?? "Deduction"} - {Description ?? "N/A"}";

        [NotMapped]
        public bool IsActive => !EndDate.HasValue || EndDate.Value > DateTime.Now;

        [NotMapped]
        public DateTime? DateCreated => CreatedOn;
    }
}
