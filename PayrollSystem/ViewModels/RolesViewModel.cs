using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PayrollSystem.ViewModels
{
    public class RolesViewModel
    {

        public string? Id { get; set; }

        [Required(ErrorMessage = "Role name is required.")]
        [StringLength(100, ErrorMessage = "Role name cannot exceed 100 characters.")]
        public string? RoleName { get; set; }

        public bool IsActive { get; set; } = true;
        public string? Description { get; set; }
        public string? Remarks { get; set; }

        public List<string> AssignedTasks { get; set; } = new List<string>();

    }
}
