using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class GovernmentContributionTable : UserActivity, IValidatableObject
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int GovernmentContributionTypeId { get; set; }
        public virtual GovernmentContributionType GovernmentContributionType { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Minimum Salary")]
        public decimal MinSalary { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Maximum Salary")]
        public decimal? MaxSalary { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Employee Share (Fixed)")]
        public decimal? EmployeeShare { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Employer Share (Fixed)")]
        public decimal? EmployerShare { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Contribution (Fixed)")]
        public decimal? TotalContribution { get; set; }

        // Percentage Fields (PhilHealth, Pag-IBIG)
        // Stored as a decimal fraction: 0.02 = 2% 
        [Column(TypeName = "decimal(8,2)")]
        [Display(Name = "Employee Share % (e.g. 0.02 = 2%)")]
        public decimal? EmployeeSharePercentage { get; set; }

        // Stored as a decimal fraction: 0.02 = 2%
        [Column(TypeName = "decimal(8,2)")]
        [Display(Name = "Employer Share % (e.g. 0.02 = 2%)")]
        public decimal? EmployerSharePercentage { get; set; }

        // Caps / Floor 
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Maximum Employee Contribution")]
        public decimal? MaxEmployeeContribution { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Maximum Employer Contribution")]
        public decimal? MaxEmployerContribution { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Minimum Employee Contribution")]
        public decimal? MinEmployeeContribution { get; set; }

        // Effectivity (replaces Year) 
        [Required]
        [Display(Name = "Effective Start Date")]
        public DateTime EffectiveStartDate { get; set; }

        [Display(Name = "Effective End Date")]
        public DateTime? EffectiveEndDate { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        // Computed Helpers 
        [NotMapped]
        public string SalaryRange => MaxSalary.HasValue
            ? $"₱{MinSalary:N2} – ₱{MaxSalary:N2}"
            : $"₱{MinSalary:N2} and above";

        [NotMapped]
        public bool IsCurrent => IsActive
            && EffectiveStartDate <= DateTime.Now
            && (!EffectiveEndDate.HasValue || EffectiveEndDate.Value >= DateTime.Now);

        // Computation Helper 
        public (decimal EmployeeAmount, decimal EmployerAmount) ComputeContribution(
            decimal monthlySalary, ContributionCalculationMethod method)
        {
            switch (method)
            {
                case ContributionCalculationMethod.Fixed:
                case ContributionCalculationMethod.Tiered:
                    return (EmployeeShare ?? 0, EmployerShare ?? 0);

                case ContributionCalculationMethod.Percentage:
                    decimal empAmt = monthlySalary * (EmployeeSharePercentage ?? 0);
                    decimal erAmt = monthlySalary * (EmployerSharePercentage ?? 0);

                    if (MinEmployeeContribution.HasValue)
                        empAmt = Math.Max(empAmt, MinEmployeeContribution.Value);
                    if (MaxEmployeeContribution.HasValue)
                        empAmt = Math.Min(empAmt, MaxEmployeeContribution.Value);
                    if (MaxEmployerContribution.HasValue)
                        erAmt = Math.Min(erAmt, MaxEmployerContribution.Value);

                    return (empAmt, erAmt);

                default:
                    return (0, 0);
            }
        }

        // ── Validation ────────────────────────────────
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (MaxSalary.HasValue && MaxSalary.Value <= MinSalary)
                yield return new ValidationResult(
                    "Maximum Salary must be greater than Minimum Salary.",
                    new[] { nameof(MaxSalary) });

            if (EffectiveEndDate.HasValue && EffectiveEndDate.Value < EffectiveStartDate)
                yield return new ValidationResult(
                    "Effective End Date cannot be earlier than Effective Start Date.",
                    new[] { nameof(EffectiveEndDate) });

            bool hasFixed = EmployeeShare.HasValue || EmployerShare.HasValue;
            bool hasPercentage = EmployeeSharePercentage.HasValue || EmployerSharePercentage.HasValue;

            if (!hasFixed && !hasPercentage)
                yield return new ValidationResult(
                    "At least one of Fixed Share or Percentage Share must be provided.",
                    new[] { nameof(EmployeeShare), nameof(EmployeeSharePercentage) });

            if (EmployeeSharePercentage.HasValue
                && (EmployeeSharePercentage.Value < 0 || EmployeeSharePercentage.Value > 1))
                yield return new ValidationResult(
                    "Employee Share % must be between 0 and 1 (e.g., 0.02 for 2%).",
                    new[] { nameof(EmployeeSharePercentage) });

            if (EmployerSharePercentage.HasValue
                && (EmployerSharePercentage.Value < 0 || EmployerSharePercentage.Value > 1))
                yield return new ValidationResult(
                    "Employer Share % must be between 0 and 1 (e.g., 0.02 for 2%).",
                    new[] { nameof(EmployerSharePercentage) });
        }
    }
}