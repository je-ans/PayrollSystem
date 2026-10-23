using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PayrollSystem.Data;
using PayrollSystem.Interfaces;

namespace PayrollSystem.Services
{
    public class PermissionCacheService : IPermissionCacheService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PermissionCacheService> _logger;

        public PermissionCacheService(
            ApplicationDbContext dbContext,
            IMemoryCache cache,
            ILogger<PermissionCacheService> logger)
        {
            _dbContext = dbContext;
            _cache = cache;
            _logger = logger;
        }

        public async Task<IReadOnlyList<string>> GetRolePermissionsAsync(string roleId)
        {
            var cacheKey = $"Permissions__{roleId}";
            if (_cache.TryGetValue(cacheKey, out IReadOnlyList<string> cachedPermissions))
            {
                return cachedPermissions;
            }

            // (Adjust this query structure to fit your AssignRolePermissions/Task/Parent model)
            var permissions = await _dbContext.AssignRolePermissions
                .AsNoTracking()
                .Where(arp => arp.RoleId == roleId && arp.Task != null && arp.Task.IsDeleted == false)
                .Include(arp => arp.Task)
                    .ThenInclude(task => task.Parent)
                .Select(arp => arp.Task.ParentId == null
                    ? (arp.Task.Code ?? string.Empty)
                    : ((arp.Task.Parent.Code ?? string.Empty) + ":" + (arp.Task.Code ?? string.Empty)))
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToListAsync();

            _cache.Set(cacheKey, permissions, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                Priority = CacheItemPriority.Normal,
                Size = 1
            });
            _logger.LogInformation("[PermissionCache] Cached {Count} permissions for role {RoleId}", permissions.Count, roleId);

            return permissions;
        }

        public void InvalidateRolePermissions(string roleId)
        {
            var cacheKey = $"Permissions__{roleId}";
            _cache.Remove(cacheKey);
            _logger.LogInformation("[PermissionCache] Invalidated cache for role {RoleId}", roleId);
        }

        public void InvalidateAllPermissions()
        {
            _logger.LogWarning("[PermissionCache] InvalidateAllPermissions called, but no global eviction implemented.");
        }
    }
}