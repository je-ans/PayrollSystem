using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;

namespace PayrollSystem.Utility
{
    public static class OrganizationScopeHelper
    {
        public sealed record OrgNode(int Id, int? ParentId, OrganizationLevel Level);

        public static async Task<Dictionary<int, OrgNode>> LoadOrgCacheAsync(ApplicationDbContext db)
        {
            var nodes = await db.OrganizationStructures
                .AsNoTracking()
                .Select(o => new OrgNode(o.Id, o.ParentId, o.Level))
                .ToListAsync();

            return nodes.ToDictionary(n => n.Id);
        }

        public static HashSet<int> GetSubtreeOrgIds(int rootOrgId, IReadOnlyDictionary<int, OrgNode> cache)
        {
            var result = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(rootOrgId);

            while (stack.Count > 0)
            {
                var id = stack.Pop();
                if (!result.Add(id))
                    continue;

                foreach (var child in cache.Values.Where(n => n.ParentId == id))
                    stack.Push(child.Id);
            }

            return result;
        }

        public static bool IsOrgWithinScope(
            int targetOrgId,
            int scopeRootOrgId,
            IReadOnlyDictionary<int, OrgNode> cache)
        {
            var current = targetOrgId;
            var visited = new HashSet<int>();

            while (current > 0 && visited.Add(current))
            {
                if (current == scopeRootOrgId)
                    return true;

                if (!cache.TryGetValue(current, out var node) || !node.ParentId.HasValue)
                    return false;

                current = node.ParentId.Value;
            }

            return false;
        }

        public static HashSet<int> GetDescendantOrgIdsAtLevel(
            IEnumerable<int> ancestorIds,
            OrganizationLevel level,
            IReadOnlyDictionary<int, OrgNode> cache)
        {
            var result = new HashSet<int>();

            foreach (var ancestorId in ancestorIds.Distinct())
            {
                foreach (var orgId in GetSubtreeOrgIds(ancestorId, cache))
                {
                    if (cache.TryGetValue(orgId, out var node) && node.Level == level)
                        result.Add(orgId);
                }
            }

            return result;
        }

        public static async Task<HashSet<int>> GetOrgIdsWithoutFunctionalRoleAsync(
            ApplicationDbContext db,
            IEnumerable<int> orgIds,
            OrganizationFunctionalRole functionalRole)
        {
            var idList = orgIds.Distinct().ToList();
            if (idList.Count == 0)
                return new HashSet<int>();

            var withRole = await db.EmployeeRoleAssignments
                .AsNoTracking()
                .Where(a =>
                    idList.Contains(a.OrganizationStructureId) &&
                    a.EffectiveTo == null &&
                    a.FunctionalRole == functionalRole)
                .Select(a => a.OrganizationStructureId)
                .Distinct()
                .ToListAsync();

            return idList.Where(id => !withRole.Contains(id)).ToHashSet();
        }

        public static async Task<bool> HasActiveFunctionalRoleHolderAsync(
            ApplicationDbContext db,
            int organizationStructureId,
            OrganizationFunctionalRole functionalRole)
        {
            return await db.EmployeeRoleAssignments
                .AsNoTracking()
                .AnyAsync(a =>
                    a.OrganizationStructureId == organizationStructureId &&
                    a.EffectiveTo == null &&
                    a.FunctionalRole == functionalRole);
        }

        public static async Task<bool> IsLeadershipManagerForOrgAsync(
            ApplicationDbContext db,
            Guid employeeId,
            int organizationStructureId,
            params OrganizationLeadershipRole[] leadershipRoles)
        {
            if (leadershipRoles.Length == 0)
            {
                leadershipRoles =
                [
                    OrganizationLeadershipRole.Head,
                    OrganizationLeadershipRole.OIC
                ];
            }

            var leaderOrgIds = await EmployeeOrganizationHelper.GetActiveLeadershipRoleOrgUnitIdsAsync(
                db,
                employeeId,
                leadershipRoles);

            if (leaderOrgIds.Count == 0)
                return false;

            var cache = await LoadOrgCacheAsync(db);
            return leaderOrgIds.Any(leaderOrgId =>
                IsOrgWithinScope(organizationStructureId, leaderOrgId, cache));
        }

        public static async Task<HashSet<int>> GetFunctionalRoleManagerOrgUnitIdsAsync(
            ApplicationDbContext db,
            Guid employeeId)
        {
            var leaderOrgIds = await EmployeeOrganizationHelper.GetActiveLeadershipRoleOrgUnitIdsAsync(
                db,
                employeeId,
                OrganizationLeadershipRole.Head,
                OrganizationLeadershipRole.OIC,
                OrganizationLeadershipRole.AssistantHead);

            if (leaderOrgIds.Count == 0)
                return new HashSet<int>();

            var cache = await LoadOrgCacheAsync(db);
            var result = new HashSet<int>();

            foreach (var leaderOrgId in leaderOrgIds)
                result.UnionWith(GetSubtreeOrgIds(leaderOrgId, cache));

            return result;
        }
    }
}
