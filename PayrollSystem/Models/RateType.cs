using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class RateType : UserActivity, IValidatableObject
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Display(Name = "Rate Type Name")]
        public string Name { get; set; }

        [MaxLength(10)]
        [Display(Name = "Code")]
        public string? Code { get; set; }

        [MaxLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }


        [Required]
        [Display(Name = "Rate Basis")]
        public RateBasis RateBasis { get; set; }


        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Monthly Salary")]
        public decimal? MonthlySalary { get; set; }


        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Daily Rate")]
        public decimal? DailyRate { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Hourly Rate")]
        public decimal? HourlyRate { get; set; }


        [Display(Name = "Effective Start Date")]
        public DateTime? EffectiveStartDate { get; set; }

        [Display(Name = "Effective End Date")]
        public DateTime? EffectiveEndDate { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;


        [NotMapped]
        public decimal EffectiveMonthlyRate => RateBasis switch
        {
            RateBasis.Monthly when MonthlySalary.HasValue => MonthlySalary.Value,
            RateBasis.Monthly when DailyRate.HasValue => DailyRate.Value * 22M,
            RateBasis.Daily when DailyRate.HasValue => DailyRate.Value * 22M,
            RateBasis.Hourly when HourlyRate.HasValue => HourlyRate.Value * 8M * 22M,
            _ => 0
        };

        [NotMapped]
        public decimal EffectiveDailyRate => RateBasis switch
        {
            RateBasis.Daily when DailyRate.HasValue => DailyRate.Value,
            RateBasis.Hourly when HourlyRate.HasValue => HourlyRate.Value * 8M,
            _ => EffectiveMonthlyRate / 22M
        };

        [NotMapped]
        public decimal EffectiveHourlyRate => RateBasis == RateBasis.Hourly && HourlyRate.HasValue
            ? HourlyRate.Value
            : EffectiveDailyRate / 8M;

        [NotMapped]
        public bool IsCurrent => IsActive
            && (!EffectiveStartDate.HasValue || EffectiveStartDate.Value <= DateTime.Now)
            && (!EffectiveEndDate.HasValue || EffectiveEndDate.Value >= DateTime.Now);

        public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();


        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            switch (RateBasis)
            {
                case RateBasis.Monthly:
                    if (!MonthlySalary.HasValue && !DailyRate.HasValue)
                        yield return new ValidationResult(
                            "Monthly Salary or Daily Rate is required when Rate Basis is Monthly.",
                            new[] { nameof(MonthlySalary), nameof(DailyRate) });
                    break;

                case RateBasis.Daily:
                    if (!DailyRate.HasValue)
                        yield return new ValidationResult(
                            "Daily Rate is required when Rate Basis is Daily.",
                            new[] { nameof(DailyRate) });
                    break;

                case RateBasis.Hourly:
                    if (!HourlyRate.HasValue)
                        yield return new ValidationResult(
                            "Hourly Rate is required when Rate Basis is Hourly.",
                            new[] { nameof(HourlyRate) });
                    break;
            }

            if (MonthlySalary.HasValue && MonthlySalary.Value < 0)
                yield return new ValidationResult(
                    "Monthly Salary cannot be negative.",
                    new[] { nameof(MonthlySalary) });

            if (DailyRate.HasValue && DailyRate.Value < 0)
                yield return new ValidationResult(
                    "Daily Rate cannot be negative.",
                    new[] { nameof(DailyRate) });

            if (HourlyRate.HasValue && HourlyRate.Value < 0)
                yield return new ValidationResult(
                    "Hourly Rate cannot be negative.",
                    new[] { nameof(HourlyRate) });

            if (EffectiveStartDate.HasValue && EffectiveEndDate.HasValue
                && EffectiveEndDate.Value < EffectiveStartDate.Value)
                yield return new ValidationResult(
                    "Effective End Date cannot be earlier than Effective Start Date.",
                    new[] { nameof(EffectiveEndDate) });
        }
    }
}