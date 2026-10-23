using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PayrollSystem.Data;
using PayrollSystem.Interfaces;
using PayrollSystem.Models;

namespace PayrollSystem.Services
{
    public class OptimizedUserService : IOptimizedUserService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IMemoryCache _cache;
        private readonly ILogger<OptimizedUserService> _logger;

        public OptimizedUserService(
            ApplicationDbContext dbContext,
            IMemoryCache cache,
            ILogger<OptimizedUserService> logger)
        {
            _dbContext = dbContext;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ApplicationUser> GetCachedUserDetailsAsync(string userId)
        {
            var cacheKey = $"User__{userId}";
            if (_cache.TryGetValue(cacheKey, out ApplicationUser cachedUser))
            {
                return cachedUser;
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .Include(u => u.Designation)
                .Include(u => u.Department)
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user != null)
            {
                _cache.Set(cacheKey, user, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                    Priority = CacheItemPriority.Normal,
                    Size = 1
                });
            }

            return user;
        }

        public void InvalidateUserCache(string userId)
        {
            var cacheKey = $"User__{userId}";
            _cache.Remove(cacheKey);
        }
    }
}