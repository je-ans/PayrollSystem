using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;

namespace PayrollSystem.Utility
{
    public static class OrganizationRoleAssignmentHelper
    {
        public static async Task PopulateUnitEmployeeSelectListAsync(
            Controller controller,
            ApplicationDbContext db,
            int organizationStructureId,
            Guid? includeEmployeeId = null)
        {
            var assignedEmployeeIds = await db.EmployeeOrganizationAssignments
                .AsNoTracking()
                .Where(a =>
                    a.OrganizationStructureId == organizationStructureId &&
                    a.EffectiveTo == null)
                .Select(a => a.EmployeeId)
                .ToListAsync();

            if (includeEmployeeId.HasValue && !assignedEmployeeIds.Contains(includeEmployeeId.Value))
                assignedEmployeeIds.Add(includeEmployeeId.Value);

            controller.ViewBag.Employees = await db.Employees
                .Where(e => !e.IsDeleted && e.IsActive && assignedEmployeeIds.Contains(e.Id))
                .OrderBy(e => e.Surname).ThenBy(e => e.FirstName)
                .Select(e => new
                {
                    e.Id,
                    Name = $"{e.FirstName} {e.Surname} ({e.EmployeeIdNumber})"
                })
                .AsNoTracking()
                .ToListAsync();

            controller.ViewBag.HasUnitEmployees = assignedEmployeeIds.Count > 0;
        }
    }
}
