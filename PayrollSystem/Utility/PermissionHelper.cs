using System.Security.Claims;

namespace PayrollSystem.Utility
{
    public static class PermissionHelper
    {
        public const string UserPermissionClaimType = "UserPermission";

        private static readonly string[] PrivilegedRoles =
        [
            "Admin",
            "SuperAdmin"
        ];

        public static bool IsPrivilegedUser(ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true)
                return false;

            return user.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Any(c => PrivilegedRoles.Contains(c.Value, StringComparer.OrdinalIgnoreCase));
        }

        public static HashSet<string> GetPermissionTokens(ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var raw = user.FindFirstValue(UserPermissionClaimType) ?? string.Empty;
            return new HashSet<string>(
                raw.Split('|', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim().ToUpperInvariant()),
                StringComparer.OrdinalIgnoreCase);
        }

        public static bool HasPermission(ClaimsPrincipal? user, string permission)
        {
            if (user?.Identity?.IsAuthenticated != true)
                return false;

            if (IsPrivilegedUser(user))
                return true;

            return HasPermissionToken(GetPermissionTokens(user), permission);
        }

        public static bool HasAnyPermission(ClaimsPrincipal? user, params string[] permissions)
        {
            if (permissions.Length == 0)
                return false;

            return permissions.Any(permission => HasPermission(user, permission));
        }

        public static bool HasPermissionToken(IEnumerable<string> tokens, string permission)
        {
            var userPermissionTokens = tokens as HashSet<string> ?? tokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var requiredPermission = (permission ?? string.Empty).Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(requiredPermission))
                return false;

            if (userPermissionTokens.Contains(requiredPermission))
                return true;

            if (userPermissionTokens.Any(token => token.StartsWith(requiredPermission + ":", StringComparison.OrdinalIgnoreCase)))
                return true;

            if (!requiredPermission.Contains(':'))
            {
                return userPermissionTokens.Any(token =>
                    token.EndsWith(":" + requiredPermission, StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }
    }
}
