using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Models.BuildUp;

namespace PayrollSystem.Utility
{
    public static class EmployeeOrganizationHelper
    {
        public static bool IsAssignmentActive(DateTime? effectiveTo) =>
            effectiveTo == null;

        public static IQueryable<Employee> FilterByActiveOrganizationUnit(
            this IQueryable<Employee> query,
            ApplicationDbContext db,
            int organizationStructureId)
        {
            return query.Where(e => db.EmployeeOrganizationAssignments.Any(a =>
                a.EmployeeId == e.Id &&
                a.OrganizationStructureId == organizationStructureId &&
                a.EffectiveTo == null));
        }

        public static IQueryable<Employee> FilterByActiveOrganizationUnits(
            this IQueryable<Employee> query,
            ApplicationDbContext db,
            IEnumerable<int> organizationStructureIds)
        {
            var ids = organizationStructureIds.Distinct().ToList();
            if (ids.Count == 0)
                return query;

            return query.Where(e => db.EmployeeOrganizationAssignments.Any(a =>
                a.EmployeeId == e.Id &&
                ids.Contains(a.OrganizationStructureId) &&
                a.EffectiveTo == null));
        }

        public static IQueryable<Employee> FilterByActiveDesignation(
            this IQueryable<Employee> query,
            int designationId)
        {
            return query.Where(e => e.Designations.Any(d =>
                d.DesignationId == designationId && d.EffectiveTo == null));
        }

        public static async Task<Dictionary<Guid, string>> GetActiveUnitDisplayNamesAsync(
            ApplicationDbContext db,
            IEnumerable<Guid> employeeIds)
        {
            var idList = employeeIds.Distinct().ToList();
            if (idList.Count == 0)
                return new Dictionary<Guid, string>();

            var rows = await db.EmployeeOrganizationAssignments
                .AsNoTracking()
                .Where(a => idList.Contains(a.EmployeeId) && a.EffectiveTo == null)
                .Include(a => a.OrganizationStructure)
                .OrderByDescending(a => a.IsPrimary)
                .ThenBy(a => a.OrganizationStructure!.Name)
                .Select(a => new
                {
                    a.EmployeeId,
                    UnitName = a.OrganizationStructure!.Name,
                    a.IsPrimary
                })
                .ToListAsync();

            return rows
                .GroupBy(r => r.EmployeeId)
                .ToDictionary(
                    g => g.Key,
                    g => string.Join(", ", g.Select(x => x.IsPrimary ? $"{x.UnitName} (Primary)" : x.UnitName).Distinct()));
        }

        public static async Task<Dictionary<Guid, (int? FirstUnitId, string Names)>> GetActiveUnitInfoAsync(
            ApplicationDbContext db,
            IEnumerable<Guid> employeeIds)
        {
            var idList = employeeIds.Distinct().ToList();
            if (idList.Count == 0)
                return new Dictionary<Guid, (int?, string)>();

            var rows = await db.EmployeeOrganizationAssignments
                .AsNoTracking()
                .Where(a => idList.Contains(a.EmployeeId) && a.EffectiveTo == null)
                .Include(a => a.OrganizationStructure)
                .OrderByDescending(a => a.IsPrimary)
                .ThenBy(a => a.OrganizationStructure!.Name)
                .Select(a => new
                {
                    a.EmployeeId,
                    a.OrganizationStructureId,
                    UnitName = a.OrganizationStructure!.Name,
                    a.IsPrimary
                })
                .ToListAsync();

            return rows
                .GroupBy(r => r.EmployeeId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var primary = g.FirstOrDefault(x => x.IsPrimary) ?? g.First();
                        var names = g.Select(x => x.IsPrimary ? $"{x.UnitName} (Primary)" : x.UnitName);
                        return ((int?)primary.OrganizationStructureId, string.Join(", ", names.Distinct()));
                    });
        }

        public static async Task<Dictionary<Guid, string>> GetActiveDesignationNamesAsync(
            ApplicationDbContext db,
            IEnumerable<Guid> employeeIds)
        {
            var idList = employeeIds.Distinct().ToList();
            if (idList.Count == 0)
                return new Dictionary<Guid, string>();

            var rows = await db.EmployeeDesignations
                .AsNoTracking()
                .Where(d => idList.Contains(d.EmployeeId) && d.EffectiveTo == null)
                .Include(d => d.Designation)
                .Select(d => new { d.EmployeeId, Name = d.Designation!.DesignationName })
                .ToListAsync();

            return rows.ToDictionary(r => r.EmployeeId, r => r.Name);
        }

        public static async Task<int?> GetActiveDesignationIdAsync(
            ApplicationDbContext db,
            Guid employeeId)
        {
            return await db.EmployeeDesignations
                .AsNoTracking()
                .Where(d => d.EmployeeId == employeeId && d.EffectiveTo == null)
                .Select(d => (int?)d.DesignationId)
                .FirstOrDefaultAsync();
        }

        public static async Task<List<int>> GetActiveOrganizationStructureIdsAsync(
            ApplicationDbContext db,
            Guid employeeId)
        {
            return await db.EmployeeOrganizationAssignments
                .AsNoTracking()
                .Where(a => a.EmployeeId == employeeId && a.EffectiveTo == null)
                .OrderByDescending(a => a.IsPrimary)
                .ThenBy(a => a.OrganizationStructureId)
                .Select(a => a.OrganizationStructureId)
                .ToListAsync();
        }

        public static async Task<int?> GetPrimaryOrganizationStructureIdAsync(
            ApplicationDbContext db,
            Guid employeeId)
        {
            var primary = await db.EmployeeOrganizationAssignments
                .AsNoTracking()
                .Where(a => a.EmployeeId == employeeId && a.EffectiveTo == null && a.IsPrimary)
                .Select(a => (int?)a.OrganizationStructureId)
                .FirstOrDefaultAsync();

            if (primary.HasValue)
                return primary;

            return await db.EmployeeOrganizationAssignments
                .AsNoTracking()
                .Where(a => a.EmployeeId == employeeId && a.EffectiveTo == null)
                .OrderBy(a => a.EffectiveFrom)
                .ThenBy(a => a.OrganizationStructureId)
                .Select(a => (int?)a.OrganizationStructureId)
                .FirstOrDefaultAsync();
        }

        public static async Task SetPrimaryOrganizationStructureAsync(
            ApplicationDbContext db,
            Guid employeeId,
            int organizationStructureId)
        {
            var active = await db.EmployeeOrganizationAssignments
                .Where(a => a.EmployeeId == employeeId && a.EffectiveTo == null)
                .ToListAsync();

            foreach (var assignment in active)
                assignment.IsPrimary = assignment.OrganizationStructureId == organizationStructureId;
        }

        public static async Task EnsurePrimaryAssignmentAsync(
            ApplicationDbContext db,
            Guid employeeId)
        {
            var active = await db.EmployeeOrganizationAssignments
                .Where(a => a.EmployeeId == employeeId && a.EffectiveTo == null)
                .OrderBy(a => a.EffectiveFrom)
                .ThenBy(a => a.OrganizationStructureId)
                .ToListAsync();

            if (active.Count == 0 || active.Any(a => a.IsPrimary))
                return;

            active[0].IsPrimary = true;
        }

        public static async Task<bool> IsPrimaryOrganizationAssignmentAsync(
            ApplicationDbContext db,
            Guid employeeId,
            int organizationStructureId)
        {
            var primaryId = await GetPrimaryOrganizationStructureIdAsync(db, employeeId);
            return primaryId.HasValue && primaryId.Value == organizationStructureId;
        }

        public static async Task<List<int>> GetActiveFunctionalRoleOrgUnitIdsAsync(
            ApplicationDbContext db,
            Guid employeeId,
            OrganizationFunctionalRole functionalRole)
        {
            return await db.EmployeeRoleAssignments
                .AsNoTracking()
                .Where(a =>
                    a.EmployeeId == employeeId &&
                    a.EffectiveTo == null &&
                    a.FunctionalRole == functionalRole)
                .Select(a => a.OrganizationStructureId)
                .Distinct()
                .ToListAsync();
        }

        public static async Task<List<int>> GetActiveLeadershipRoleOrgUnitIdsAsync(
            ApplicationDbContext db,
            Guid employeeId,
            params OrganizationLeadershipRole[] leadershipRoles)
        {
            if (leadershipRoles.Length == 0)
                leadershipRoles = [OrganizationLeadershipRole.Head, OrganizationLeadershipRole.OIC];

            return await db.EmployeeRoleAssignments
                .AsNoTracking()
                .Where(a =>
                    a.EmployeeId == employeeId &&
                    a.EffectiveTo == null &&
                    a.LeadershipRole != null &&
                    leadershipRoles.Contains(a.LeadershipRole.Value))
                .Select(a => a.OrganizationStructureId)
                .Distinct()
                .ToListAsync();
        }

        public static async Task<bool> HasActiveFunctionalRoleAsync(
            ApplicationDbContext db,
            Guid employeeId,
            int organizationStructureId,
            OrganizationFunctionalRole functionalRole)
        {
            return await db.EmployeeRoleAssignments
                .AsNoTracking()
                .AnyAsync(a =>
                    a.EmployeeId == employeeId &&
                    a.OrganizationStructureId == organizationStructureId &&
                    a.EffectiveTo == null &&
                    a.FunctionalRole == functionalRole);
        }

        public static async Task<WorkSchedule?> FindWorkScheduleAsync(
            ApplicationDbContext db,
            int month,
            int year,
            int organizationStructureId,
            bool approvedOnly = false)
        {
            var query = db.WorkSchedules.Where(s =>
                s.Month == month &&
                s.Year == year &&
                s.OrganizationStructureId == organizationStructureId);

            if (approvedOnly)
                query = query.Where(s => s.Status == WorkScheduleStatus.Approved);

            return await query
                .OrderByDescending(s => s.Status == WorkScheduleStatus.Approved)
                .ThenByDescending(s => s.LastModifiedAt ?? s.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public static async Task<WorkSchedule?> FindWorkScheduleForEmployeeAsync(
            ApplicationDbContext db,
            Guid employeeId,
            int month,
            int year,
            bool approvedOnly = false)
        {
            return await ResolveEmployeeMonthWorkScheduleAsync(
                db, employeeId, month, year, organizationStructureId: null, approvedOnly);
        }

        // Resolves the work schedule that owns an employee's entries for a month,
        // preferring an explicit org unit, then existing schedule entries, then primary org.
        public static async Task<WorkSchedule?> ResolveEmployeeMonthWorkScheduleAsync(
            ApplicationDbContext db,
            Guid employeeId,
            int month,
            int year,
            int? organizationStructureId = null,
            bool approvedOnly = false)
        {
            if (organizationStructureId is int orgId and > 0)
            {
                var orgSchedule = await FindWorkScheduleAsync(db, month, year, orgId, approvedOnly);
                if (orgSchedule != null)
                    return orgSchedule;
            }

            var entryScheduleQuery = db.ScheduleEntries
                .AsNoTracking()
                .Where(e => e.EmployeeId == employeeId)
                .Where(e => e.WorkSchedule.Month == month && e.WorkSchedule.Year == year);

            if (approvedOnly)
                entryScheduleQuery = entryScheduleQuery.Where(e => e.WorkSchedule.Status == WorkScheduleStatus.Approved);

            var scheduleCounts = await entryScheduleQuery
                .GroupBy(e => e.WorkScheduleId)
                .Select(g => new { WorkScheduleId = g.Key, Count = g.Count() })
                .ToListAsync();

            if (scheduleCounts.Count > 0)
            {
                var scheduleIds = scheduleCounts.Select(x => x.WorkScheduleId).ToList();
                var schedules = await db.WorkSchedules
                    .AsNoTracking()
                    .Where(s => scheduleIds.Contains(s.Id))
                    .Select(s => new { s.Id, s.Status, s.LastModifiedAt, s.CreatedAt })
                    .ToListAsync();

                var bestScheduleId = scheduleCounts
                    .Join(
                        schedules,
                        count => count.WorkScheduleId,
                        schedule => schedule.Id,
                        (count, schedule) => new
                        {
                            count.WorkScheduleId,
                            count.Count,
                            schedule.Status,
                            schedule.LastModifiedAt,
                            schedule.CreatedAt
                        })
                    .OrderByDescending(x => x.Status == WorkScheduleStatus.Approved)
                    .ThenByDescending(x => x.Count)
                    .ThenByDescending(x => x.LastModifiedAt ?? x.CreatedAt)
                    .Select(x => x.WorkScheduleId)
                    .First();

                return await db.WorkSchedules
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == bestScheduleId);
            }

            var orgUnitId = await GetPrimaryOrganizationStructureIdAsync(db, employeeId);
            if (!orgUnitId.HasValue)
                return null;

            return await FindWorkScheduleAsync(db, month, year, orgUnitId.Value, approvedOnly);
        }
    }
}
