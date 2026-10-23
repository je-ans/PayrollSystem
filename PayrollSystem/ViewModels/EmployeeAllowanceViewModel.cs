using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class AllowanceListItemViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Allowance Name")]
        public string AllowanceName { get; set; } = string.Empty;

        [Display(Name = "Allowance Type")]
        public string AllowanceTypeName { get; set; } = string.Empty;

        [Display(Name = "Code")]
        public string? Code { get; set; }

        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Display(Name = "Frequency")]
        public AllowanceFrequency Frequency { get; set; }

        [Display(Name = "Is Taxable")]
        public bool IsTaxable { get; set; }

        [Display(Name = "Is Mandatory")]
        public bool IsMandatory { get; set; }

        [Display(Name = "Effective Date")]
        public DateTime EffectiveDate { get; set; }

        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }

        [Display(Name = "Employees")]
        public int EmployeeCount { get; set; }
    }

    public class AllowanceEditViewModel : IValidatableObject
    {
        public int Id { get; set; }

        [Display(Name = "Apply to all active employees")]
        public bool ApplyAllActiveEmployees { get; set; }

        [Display(Name = "Employees")]
        [ValidateNever]
        public List<Guid> EmployeeIds { get; set; } = new();

        [Required]
        [MaxLength(100)]
        [Display(Name = "Allowance Name")]
        public string AllowanceName { get; set; } = string.Empty;

        [MaxLength(10)]
        [Display(Name = "Code")]
        public string? Code { get; set; }

        [Required]
        [Display(Name = "Allowance Type")]
        public int AllowanceTypeId { get; set; }

        [Required]
        [Display(Name = "Amount")]
        [Range(0, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Frequency")]
        public AllowanceFrequency Frequency { get; set; }

        [Display(Name = "Taxable")]
        public bool IsTaxable { get; set; } = true;

        [Display(Name = "Mandatory")]
        public bool IsMandatory { get; set; }

        [Display(Name = "Effective Date")]
        public DateTime EffectiveDate { get; set; } = DateTime.Today;

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

    public class AllowanceEmployeeListItemViewModel
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

    public class AllowanceDetailsViewModel
    {
        public AllowanceListItemViewModel Allowance { get; set; } = new();
        public List<AllowanceEmployeeListItemViewModel> Employees { get; set; } = new();
    }

    public class EmployeeAllowanceListItemViewModel
    {
        public int Id { get; set; }
        public int AllowanceId { get; set; }
        public string EmployeeIdNumber { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string AllowanceName { get; set; } = string.Empty;
        public string AllowanceTypeName { get; set; } = string.Empty;
        public string? Code { get; set; }
        public decimal Amount { get; set; }
        public AllowanceFrequency Frequency { get; set; }
        public bool IsTaxable { get; set; }
        public bool IsMandatory { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
    }

    public class EmployeeAllowanceEditViewModel : AllowanceEditViewModel
    {
    }
}
