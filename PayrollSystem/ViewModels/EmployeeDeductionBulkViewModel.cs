using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PayrollSystem.ViewModels
{
    public class EmployeeDeductionBulkViewModel : IValidatableObject
    {
        // When true, all active employees (not deleted) receive the deduction; 
        [Display(Name = "Apply to all active employees")]
        public bool ApplyAllActiveEmployees { get; set; }

        [Display(Name = "Employees")]
        [ValidateNever]
        public List<Guid> EmployeeIds { get; set; } = new List<Guid>();

        [Required]
        [Display(Name = "Deduction Type")]
        public int DeductionTypeId { get; set; }

        [Required]
        [MaxLength(100)]
        [Display(Name = "Deduction Name")]
        public string DeductionName { get; set; } = string.Empty;

        [MaxLength(10)]
        [Display(Name = "Code")]
        public string? Code { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Effective Start Date")]
        public DateOnly EffectiveStartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [DataType(DataType.Date)]
        [Display(Name = "Effective End Date")]
        public DateOnly? EffectiveEndDate { get; set; }

        [Display(Name = "Remarks")]
        public string? Remarks { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!ApplyAllActiveEmployees && (EmployeeIds == null || !EmployeeIds.Any()))
            {
                yield return new ValidationResult(
                    "Select at least one employee, or enable Apply to all active employees.",
                    new[] { nameof(EmployeeIds) });
            }
        }
    }
}
