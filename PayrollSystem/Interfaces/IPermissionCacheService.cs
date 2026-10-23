using System.Collections.Generic;
using System.Threading.Tasks;

namespace PayrollSystem.Interfaces
{
    public interface IPermissionCacheService
    {
        Task<IReadOnlyList<string>> GetRolePermissionsAsync(string roleId);
        void InvalidateRolePermissions(string roleId);
        void InvalidateAllPermissions();
    }
}