using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;

namespace PayrollSystem.Utility
{
    public static class EmployeePortalHelper
    {
        public const string EmployeePortalViewPermission = "EMPLOYEE_PORTAL_VIEW";

        private static readonly string[] ManagementSystemRoles =
        [
            "Admin",
            "SuperAdmin"
        ];

        public static bool IsManagementSystemRole(string role) =>
            ManagementSystemRoles.Contains(role, StringComparer.OrdinalIgnoreCase);

        public static bool HasManagementSystemRole(IEnumerable<string> roles) =>
            roles.Any(IsManagementSystemRole);

        public static async Task<Employee?> GetCurrentEmployeeAsync(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            ClaimsPrincipal user)
        {
            var applicationUser = await userManager.GetUserAsync(user);
            if (applicationUser == null)
                return null;

            return await db.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.ApplicationUserId == applicationUser.Id &&
                    !e.IsDeleted &&
                    e.IsActive);
        }

        public static async Task<bool> CanAccessEmployeeAsync(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            ClaimsPrincipal user,
            Guid employeeId)
        {
            if (!await IsEmployeeOnlyUserAsync(userManager, user))
                return true;

            var current = await GetCurrentEmployeeAsync(userManager, db, user);
            return current != null && current.Id == employeeId;
        }

        public static async Task<bool> CanViewEmployeePortalAsync(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            ClaimsPrincipal user)
        {
            var applicationUser = await userManager.GetUserAsync(user);
            if (applicationUser == null)
                return false;

            var employee = await GetCurrentEmployeeAsync(userManager, db, user);
            if (employee == null)
                return false;

            var roles = await userManager.GetRolesAsync(applicationUser);

            if (HasManagementSystemRole(roles))
                return false;

            if (HasEmployeePortalPermission(user))
                return true;

            return roles.Contains("Employee", StringComparer.OrdinalIgnoreCase);
        }

        public static bool HasAdminConsoleAccess(ClaimsPrincipal user, IEnumerable<string> roles)
        {
            if (HasManagementSystemRole(roles))
                return true;

            var tokens = PermissionHelper.GetPermissionTokens(user);
            if (tokens.Count == 0)
                return false;

            return tokens.Any(token =>
                !string.Equals(token, EmployeePortalViewPermission, StringComparison.OrdinalIgnoreCase) &&
                !token.StartsWith("EMPLOYEE_PORTAL:", StringComparison.OrdinalIgnoreCase));
        }

        public static bool HasEmployeePortalPermission(ClaimsPrincipal user) =>
            PermissionHelper.HasPermission(user, EmployeePortalViewPermission);

        public static async Task<bool> IsEmployeeOnlyUserAsync(
            UserManager<ApplicationUser> userManager,
            ClaimsPrincipal user)
        {
            var applicationUser = await userManager.GetUserAsync(user);
            if (applicationUser == null)
                return false;

            var roles = await userManager.GetRolesAsync(applicationUser);
            var isEmployee = roles.Contains("Employee", StringComparer.OrdinalIgnoreCase);

            return isEmployee && !HasManagementSystemRole(roles);
        }
    }
}
