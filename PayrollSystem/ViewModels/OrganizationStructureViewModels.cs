using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Rendering;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class OrganizationStructureViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Code is required")]
        [MaxLength(50)]
        [Display(Name = "Code")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Name is required")]
        [MaxLength(300)]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Parent Unit")]
        public int? ParentId { get; set; }

        [Display(Name = "Organization Level")]
        public OrganizationLevel Level { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        public string? ParentName { get; set; }

        public string LevelDisplayName { get; set; } = string.Empty;
    }

    public class OrganizationStructureTreeNodeViewModel
    {
        public OrganizationStructureViewModel Node { get; set; } = new();
        public List<OrganizationStructureTreeNodeViewModel> Children { get; set; } = new();
        public bool HasChildren { get; set; }
        public bool HasEmployeeAssignments { get; set; }
        public bool HasRoleAssignments { get; set; }
        public bool CanDelete => !HasChildren && !HasEmployeeAssignments && !HasRoleAssignments;
    }

    public class OrganizationStructureIndexViewModel
    {
        public List<OrganizationStructureTreeNodeViewModel> Roots { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class OrganizationBreadcrumbItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class OrganizationUnitPageViewModel
    {
        public OrganizationStaffViewModel Staff { get; set; } = new();
        public List<OrganizationBreadcrumbItem> Breadcrumb { get; set; } = new();
        public int ChildCount { get; set; }
        public bool HasChildren { get; set; }
        public bool CanDelete { get; set; }
    }

    public static class OrganizationStructureDisplayHelper
    {
        public static string GetLevelIcon(OrganizationLevel level) => level switch
        {
            OrganizationLevel.Institution => "fa-university",
            OrganizationLevel.Office => "fa-landmark",
            OrganizationLevel.Division => "fa-sitemap",
            OrganizationLevel.Department => "fa-building",
            OrganizationLevel.Section => "fa-layer-group",
            OrganizationLevel.Unit => "fa-cubes",
            OrganizationLevel.Team => "fa-users",
            _ => "fa-folder"
        };

        public static string GetLevelAccentClass(OrganizationLevel level) => level switch
        {
            OrganizationLevel.Institution => "org-accent-institution",
            OrganizationLevel.Office => "org-accent-office",
            OrganizationLevel.Division => "org-accent-division",
            OrganizationLevel.Department => "org-accent-department",
            OrganizationLevel.Section => "org-accent-section",
            OrganizationLevel.Unit => "org-accent-unit",
            OrganizationLevel.Team => "org-accent-team",
            _ => "org-accent-default"
        };

        public static string GetLevelDisplayName(OrganizationLevel level)
        {
            var member = typeof(OrganizationLevel).GetMember(level.ToString()).FirstOrDefault();
            var display = member?.GetCustomAttribute<DisplayAttribute>();
            return display?.Name ?? level.ToString();
        }
    }

    public class OrganizationStructureFormViewModel : OrganizationStructureViewModel
    {
        public List<ParentOptionViewModel> ParentOptions { get; set; } = new();
        public List<SelectListItem> LevelOptions { get; set; } = new();
    }

    public class ParentOptionViewModel
    {
        public int Id { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        public OrganizationLevel Level { get; set; }
    }

    public class OrganizationStaffViewModel
    {
        public OrganizationStructureViewModel Unit { get; set; } = new();
        public List<EmployeeAssignmentRowViewModel> Employees { get; set; } = new();
        public List<RoleAssignmentRowViewModel> Roles { get; set; } = new();
        public int ActiveEmployeeCount => Employees.Count(e => e.IsActive);
        public int ActiveRoleCount => Roles.Count(r => r.IsActive);
    }

    public class EmployeeAssignmentRowViewModel
    {
        public int Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeIdNumber { get; set; } = string.Empty;
        public string? DesignationName { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsActive { get; set; }
        public bool IsPrimary { get; set; }
    }

    public class RoleAssignmentRowViewModel
    {
        public int Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeIdNumber { get; set; } = string.Empty;
        public OrganizationLeadershipRole? LeadershipRole { get; set; }
        public OrganizationFunctionalRole? FunctionalRole { get; set; }
        public string RoleDisplayName { get; set; } = string.Empty;
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsActive { get; set; }
    }

    public class EmployeeOrganizationAssignmentFormViewModel
    {
        public int Id { get; set; }

        public int OrganizationStructureId { get; set; }

        public string OrganizationName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Employee is required")]
        [Display(Name = "Employee")]
        public Guid EmployeeId { get; set; }

        [Required(ErrorMessage = "Effective from date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Effective From")]
        public DateTime EffectiveFrom { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "Effective To")]
        public DateTime? EffectiveTo { get; set; }

        [Display(Name = "Primary schedule unit")]
        public bool IsPrimary { get; set; }
    }

    public class EmployeeRoleAssignmentFormViewModel
    {
        public int Id { get; set; }

        public int OrganizationStructureId { get; set; }

        public string OrganizationName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Employee is required")]
        [Display(Name = "Employee")]
        public Guid EmployeeId { get; set; }

        [Display(Name = "Leadership Role")]
        public OrganizationLeadershipRole? LeadershipRole { get; set; }

        [Display(Name = "Functional Role")]
        public OrganizationFunctionalRole? FunctionalRole { get; set; }

        [Required(ErrorMessage = "Effective from date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Effective From")]
        public DateTime EffectiveFrom { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "Effective To")]
        public DateTime? EffectiveTo { get; set; }

        public bool LockFunctionalRole { get; set; }
    }
}
