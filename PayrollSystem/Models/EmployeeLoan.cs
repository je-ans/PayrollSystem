using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class EmployeeLoan : UserActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public Guid EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        [Required]
        [Display(Name = "Deduction Type (Loan)")]
        public int DeductionTypeId { get; set; }
        public virtual DeductionType DeductionType { get; set; }

        [MaxLength(100)]
        [Display(Name = "Loan Name")]
        public string? LoanName { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Principal Amount")]
        public decimal PrincipalAmount { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Interest Rate (%)")]
        public decimal? InterestRate { get; set; }

        [Required]
        [Display(Name = "Total Installments")]
        public int TotalInstallments { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Installment Amount")]
        public decimal InstallmentAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Amount To Repay")]
        public decimal TotalAmountToRepay { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Remaining Balance")]
        public decimal RemainingBalance { get; set; }

        [Display(Name = "Installments Paid")]
        public int InstallmentsPaid { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateOnly StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateOnly? EndDate { get; set; }

        [Display(Name = "Status")]
        public EmployeeLoanStatus Status { get; set; } = EmployeeLoanStatus.Active;

        [MaxLength(100)]
        [Display(Name = "Reference Number")]
        public string? ReferenceNumber { get; set; }

        [MaxLength(500)]
        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        [NotMapped]
        public bool IsActive => Status == EmployeeLoanStatus.Active && RemainingBalance > 0;
    }
}
