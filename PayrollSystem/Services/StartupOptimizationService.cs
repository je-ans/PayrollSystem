using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PayrollSystem.Data;
using PayrollSystem.Interfaces;

namespace PayrollSystem.Services
{
    public class StartupOptimizationService : IStartupOptimizationService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IMemoryCache _cache;
        private readonly ILogger<StartupOptimizationService> _logger;

        public StartupOptimizationService(
            ApplicationDbContext dbContext,
            IMemoryCache cache,
            ILogger<StartupOptimizationService> logger)
        {
            _dbContext = dbContext;
            _cache = cache;
            _logger = logger;
        }

        public async Task WarmUpAsync()
        {
            var start = DateTime.UtcNow;
            try
            {
                _logger.LogInformation("[WarmUp] Starting background cache warm-up...");

                var systemInfo = await _dbContext.SystemInfos
                    .AsNoTracking()
                    .Where(s => !s.IsDeleted)
                    .FirstOrDefaultAsync();
                if (systemInfo != null)
                    _cache.Set("SystemInfo", systemInfo, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1), Size = 1 });

                _logger.LogInformation("[WarmUp] SystemInfo preloaded.");

                var activeRoles = await _dbContext.Roles
                    .AsNoTracking()
                    .Where(r => r.IsActive && !r.IsDeleted)
                    .ToListAsync();
                foreach (var role in activeRoles)
                {
                    var cacheKey = $"Role__{role.Name.ToUpperInvariant()}";
                    _cache.Set(cacheKey, role, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1), Size = 1 });
                }
                _logger.LogInformation("[WarmUp] {Count} roles preloaded.", activeRoles.Count);



                var duration = (DateTime.UtcNow - start).TotalMilliseconds;
                _logger.LogInformation("[WarmUp] Completed in {Duration} ms.", duration);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[WarmUp] One or more warm-up steps failed.");
            }
        }
    }
}