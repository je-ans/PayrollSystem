using System.Threading.Tasks;
using PayrollSystem.Models;

namespace PayrollSystem.Interfaces
{
    public interface IOptimizedUserService
    {
        Task<ApplicationUser> GetCachedUserDetailsAsync(string userId);
        void InvalidateUserCache(string userId);
    }
}