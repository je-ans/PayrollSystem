using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PayrollSystem.Models
{
    public class AssignRolePermission : UserActivity
    {
        public int Id { get; set; }
        public int? TaskId { get; set; }

        public SystemTask? Task { get; set; }

        public string? RoleId { get; set; }

        public ApplicationRole? Role { get; set; }

    }
}
