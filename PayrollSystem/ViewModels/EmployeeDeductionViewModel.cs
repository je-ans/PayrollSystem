using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PayrollSystem.ViewModels
{
    public class DeductionListItemViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Deduction Name")]
        public string DeductionName { get; set; } = string.Empty;

        [Display(Name = "Deduction Type")]
        public string DeductionTypeName { get; set; } = string.Empty;

        [Display(Name = "Code")]
        public string? Code { get; set; }

        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Display(Name = "Effective Start Date")]
        public DateOnly EffectiveStartDate { get; set; }

        [Display(Name = "Effective End Date")]
        public DateOnly? EffectiveEndDate { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }

        [Display(Name = "Employees")]
        public int EmployeeCount { get; set; }

        public string? Remarks { get; set; }
    }

    public class DeductionEditViewModel : IValidatableObject
    {
        public int Id { get; set; }

        [Display(Name = "Apply to all active employees")]
        public bool ApplyAllActiveEmployees { get; set; }

        [Display(Name = "Employees")]
        [ValidateNever]
        public List<Guid> EmployeeIds { get; set; } = new();

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

        [MaxLength(500)]
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

    public class DeductionEmployeeListItemViewModel
    {
        public int AssignmentId { get; set; }

        [Display(Name = "Employee ID")]
        public string EmployeeIdNumber { get; set; } = string.Empty;

        [Display(Name = "Employee Name")]
        public string EmployeeName { get; set; } = string.Empty;

        [Display(Name = "Organization Unit")]
        public string? OrganizationUnitName { get; set; }

        [Display(Name = "Designation")]
        public string? DesignationName { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }
    }

    public class DeductionDetailsViewModel
    {
        public DeductionListItemViewModel Deduction { get; set; } = new();
        public List<DeductionEmployeeListItemViewModel> Employees { get; set; } = new();
    }

    public class EmployeeDeductionListItemViewModel
    {
        public int Id { get; set; }
        public int DeductionId { get; set; }
        public string EmployeeIdNumber { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string DeductionName { get; set; } = string.Empty;
        public string DeductionTypeName { get; set; } = string.Empty;
        public string? Code { get; set; }
        public decimal Amount { get; set; }
        public DateOnly EffectiveStartDate { get; set; }
        public DateOnly? EffectiveEndDate { get; set; }
        public bool IsActive { get; set; }
        public string? Remarks { get; set; }
    }
}
