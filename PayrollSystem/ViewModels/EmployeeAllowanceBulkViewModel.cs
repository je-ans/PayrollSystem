using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class EmployeeAllowanceBulkViewModel : IValidatableObject
    {
        // When true, all active employees (not deleted) receive the allowance; <see cref="EmployeeIds"/> is ignored
        [Display(Name = "Apply to all active employees")]
        public bool ApplyAllActiveEmployees { get; set; }

        [Display(Name = "Employees")]
        [ValidateNever]
        public List<Guid> EmployeeIds { get; set; } = new List<Guid>();

        [Required]
        [Display(Name = "Allowance Type")]
        public int AllowanceTypeId { get; set; }

        [Required]
        [MaxLength(100)]
        [Display(Name = "Allowance Name")]
        public string AllowanceName { get; set; } = string.Empty;

        [MaxLength(10)]
        [Display(Name = "Code")]
        public string? Code { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Frequency")]
        public AllowanceFrequency Frequency { get; set; }

        [Display(Name = "Taxable")]
        public bool IsTaxable { get; set; } = true;

        [Display(Name = "Mandatory")]
        public bool IsMandatory { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Effective Date")]
        public DateTime EffectiveDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }

        [MaxLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

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
