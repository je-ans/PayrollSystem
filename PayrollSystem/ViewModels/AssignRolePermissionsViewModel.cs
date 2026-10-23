using Microsoft.AspNetCore.Identity;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class AssignRolePermissionsViewModel
    {
        public ICollection<SystemTask> SystemTasks { get; set; }

        public ICollection<IdentityRole> SystemRoles { get; set; }

        public ICollection<int> RightsIdsAssigned { get; set; }

        public int[] Ids { get; set; }

        public string RoleId { get; set; }

        public int TaskId { get; set; }
    }
}
