using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Services;
using PayrollSystem.ViewModels.WorkSchedule;

namespace PayrollSystem.Utility
{
    public class WorkScheduleAccess
    {
        public Guid? EmployeeId { get; set; }

        public bool IsAdmin { get; set; }

        public bool HasGlobalView { get; set; }
        public bool HasGlobalEdit { get; set; }
        public bool HasGlobalSubmit { get; set; }
        public bool HasGlobalApprove { get; set; }
        public bool HasGlobalChangeProcess { get; set; }
        public bool HasGlobalChangeView { get; set; }

        public HashSet<int> SchedulerOrgUnitIds { get; set; } = new();
        public HashSet<int> HrApproverOrgUnitIds { get; set; } = new();
        public HashSet<int> TimekeeperOrgUnitIds { get; set; } = new();
        public HashSet<int> LeadershipHeadOrgUnitIds { get; set; } = new();
        public HashSet<int> FunctionalRoleManagerOrgUnitIds { get; set; } = new();

        public HashSet<int> ViewOrgUnitIds { get; set; } = new();
        public HashSet<int> EditOrgUnitIds { get; set; } = new();
        public HashSet<int> SubmitOrgUnitIds { get; set; } = new();
        public HashSet<int> ApproveOrgUnitIds { get; set; } = new();
        public HashSet<int> ChangeProcessOrgUnitIds { get; set; } = new();

        public bool CanManageSchedule =>
            IsAdmin ||
            HasGlobalEdit ||
            HasGlobalSubmit ||
            HasGlobalApprove ||
            SchedulerOrgUnitIds.Count > 0 ||
            SubmitOrgUnitIds.Count > 0 ||
            HrApproverOrgUnitIds.Count > 0 ||
            LeadershipHeadOrgUnitIds.Count > 0;

        public bool CanAccessWorkScheduleModule =>
            IsAdmin ||
            HasGlobalView ||
            HasGlobalEdit ||
            HasGlobalSubmit ||
            HasGlobalApprove ||
            SchedulerOrgUnitIds.Count > 0 ||
            SubmitOrgUnitIds.Count > 0 ||
            ApproveOrgUnitIds.Count > 0 ||
            HrApproverOrgUnitIds.Count > 0 ||
            TimekeeperOrgUnitIds.Count > 0 ||
            ViewOrgUnitIds.Count > 0;

        public bool CanViewOrgUnit(int organizationStructureId) =>
            IsAdmin || HasGlobalView || ViewOrgUnitIds.Contains(organizationStructureId);

        public bool CanEditOrgUnit(int organizationStructureId) =>
            IsAdmin || HasGlobalEdit || EditOrgUnitIds.Contains(organizationStructureId);

        public bool CanSubmitOrgUnit(int organizationStructureId) =>
            IsAdmin || HasGlobalSubmit || SubmitOrgUnitIds.Contains(organizationStructureId);

        public bool CanApproveOrgUnit(int organizationStructureId) =>
            IsAdmin || HasGlobalApprove || ApproveOrgUnitIds.Contains(organizationStructureId);

        public bool CanProcessChangeRequestsForOrgUnit(int organizationStructureId) =>
            IsAdmin ||
            HasGlobalChangeProcess ||
            ChangeProcessOrgUnitIds.Contains(organizationStructureId);

        public bool CanViewChangeRequestQueue =>
            IsAdmin ||
            HasGlobalChangeProcess ||
            HasGlobalChangeView ||
            ChangeProcessOrgUnitIds.Count > 0;

        public bool CanAssignFunctionalRolesForOrg(int organizationStructureId) =>
            IsAdmin || FunctionalRoleManagerOrgUnitIds.Contains(organizationStructureId);
    }

    public static class WorkScheduleAccessHelper
    {
        public static bool HasSystemPermission(ClaimsPrincipal user, string permission)
        {
            return PermissionHelper.HasPermission(user, permission);
        }

        public static async Task<WorkScheduleAccess> GetAccessAsync(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ClaimsPrincipal user)
        {
            if (user.IsInRole("Admin"))
            {
                return new WorkScheduleAccess
                {
                    IsAdmin = true,
                    HasGlobalView = true,
                    HasGlobalEdit = true,
                    HasGlobalSubmit = true,
                    HasGlobalApprove = true,
                    HasGlobalChangeProcess = true,
                    HasGlobalChangeView = true
                };
            }

            var hasScheduleAccess = HasSystemPermission(user, "WORK_SCHEDULE_VIEW");
            var hasChangeAccess = HasSystemPermission(user, "SCHEDULE_CHANGE_VIEW");

            // General model: *_VIEW unlocks the module. Org functional/leadership roles
            // still scope edit/submit/approve to units when the user has no global VIEW.
            var access = new WorkScheduleAccess
            {
                HasGlobalView = hasScheduleAccess,
                HasGlobalEdit = hasScheduleAccess,
                HasGlobalSubmit = hasScheduleAccess,
                HasGlobalApprove = hasScheduleAccess,
                HasGlobalChangeView = hasChangeAccess,
                HasGlobalChangeProcess = hasChangeAccess
            };

            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(userManager, db, user);
            if (employee == null)
                return access;

            access.EmployeeId = employee.Id;

            var schedulerIds = await EmployeeOrganizationHelper
                .GetActiveFunctionalRoleOrgUnitIdsAsync(db, employee.Id, OrganizationFunctionalRole.Scheduler);
            var scheduleApproverIds = await EmployeeOrganizationHelper
                .GetActiveFunctionalRoleOrgUnitIdsAsync(db, employee.Id, OrganizationFunctionalRole.ScheduleApprover);
            var hrIds = await EmployeeOrganizationHelper
                .GetActiveFunctionalRoleOrgUnitIdsAsync(db, employee.Id, OrganizationFunctionalRole.HRApprover);
            var timekeeperIds = await EmployeeOrganizationHelper
                .GetActiveFunctionalRoleOrgUnitIdsAsync(db, employee.Id, OrganizationFunctionalRole.Timekeeper);
            var headOrgIds = await EmployeeOrganizationHelper.GetActiveLeadershipRoleOrgUnitIdsAsync(
                db,
                employee.Id,
                OrganizationLeadershipRole.Head,
                OrganizationLeadershipRole.OIC);

            access.SchedulerOrgUnitIds = schedulerIds.ToHashSet();
            access.HrApproverOrgUnitIds = hrIds.ToHashSet();
            access.TimekeeperOrgUnitIds = timekeeperIds.ToHashSet();
            access.LeadershipHeadOrgUnitIds = headOrgIds.ToHashSet();
            access.FunctionalRoleManagerOrgUnitIds = await OrganizationScopeHelper
                .GetFunctionalRoleManagerOrgUnitIdsAsync(db, employee.Id);

            access.ViewOrgUnitIds = new HashSet<int>(schedulerIds);
            access.ViewOrgUnitIds.UnionWith(scheduleApproverIds);
            access.ViewOrgUnitIds.UnionWith(hrIds);
            access.ViewOrgUnitIds.UnionWith(timekeeperIds);
            access.ViewOrgUnitIds.UnionWith(headOrgIds);

            access.EditOrgUnitIds = schedulerIds.ToHashSet();
            access.SubmitOrgUnitIds = schedulerIds.ToHashSet();
            access.ApproveOrgUnitIds = new HashSet<int>(scheduleApproverIds);
            access.ApproveOrgUnitIds.UnionWith(hrIds);

            if (schedulerIds.Count > 0)
            {
                var orgCache = await OrganizationScopeHelper.LoadOrgCacheAsync(db);
                foreach (var schedulerOrgId in schedulerIds)
                {
                    access.ViewOrgUnitIds.Add(schedulerOrgId);
                    access.EditOrgUnitIds.Add(schedulerOrgId);
                    access.SubmitOrgUnitIds.Add(schedulerOrgId);

                    foreach (var orgId in OrganizationScopeHelper.GetSubtreeOrgIds(schedulerOrgId, orgCache))
                    {
                        if (!orgCache.TryGetValue(orgId, out var node))
                            continue;

                        if (node.Level is OrganizationLevel.Unit
                            or OrganizationLevel.Section
                            or OrganizationLevel.Department)
                        {
                            access.ViewOrgUnitIds.Add(orgId);
                            access.EditOrgUnitIds.Add(orgId);
                            access.SubmitOrgUnitIds.Add(orgId);
                        }
                    }
                }
            }

            if (headOrgIds.Count > 0)
            {
                var orgCache = await OrganizationScopeHelper.LoadOrgCacheAsync(db);
                var scheduleOrgIds = new HashSet<int>();

                foreach (var headOrgId in headOrgIds)
                {
                    foreach (var orgId in OrganizationScopeHelper.GetSubtreeOrgIds(headOrgId, orgCache))
                    {
                        if (orgCache.TryGetValue(orgId, out var node) &&
                            node.Level is OrganizationLevel.Unit
                                or OrganizationLevel.Section
                                or OrganizationLevel.Department)
                        {
                            scheduleOrgIds.Add(orgId);
                        }
                    }
                }

                access.ViewOrgUnitIds.UnionWith(scheduleOrgIds);

                var orgsWithoutScheduler = await OrganizationScopeHelper.GetOrgIdsWithoutFunctionalRoleAsync(
                    db,
                    scheduleOrgIds,
                    OrganizationFunctionalRole.Scheduler);

                foreach (var orgId in orgsWithoutScheduler)
                {
                    access.EditOrgUnitIds.Add(orgId);
                    access.SubmitOrgUnitIds.Add(orgId);
                }

                var orgsWithoutApprover = await OrganizationScopeHelper.GetOrgIdsWithoutFunctionalRoleAsync(
                    db,
                    scheduleOrgIds,
                    OrganizationFunctionalRole.ScheduleApprover);

                foreach (var orgId in orgsWithoutApprover)
                {
                    if (!scheduleApproverIds.Contains(orgId) && !hrIds.Contains(orgId))
                        access.ApproveOrgUnitIds.Add(orgId);
                }
            }

            access.ChangeProcessOrgUnitIds = new HashSet<int>(schedulerIds);
            access.ChangeProcessOrgUnitIds.UnionWith(hrIds);

            return access;
        }

        public static void ApplyToViewModel(WorkScheduleViewModel vm, WorkScheduleAccess access)
        {
            var hasUnscopedAccess = access.IsAdmin
                || access.HasGlobalView
                || access.HasGlobalEdit
                || access.HasGlobalSubmit
                || access.HasGlobalApprove;

            if (!hasUnscopedAccess)
            {
                vm.OrganizationUnits = vm.OrganizationUnits
                    .Where(u => access.ViewOrgUnitIds.Contains(u.Id))
                    .ToList();

                if (vm.OrganizationStructureId > 0 && !access.CanViewOrgUnit(vm.OrganizationStructureId))
                {
                    vm.OrganizationStructureId = vm.OrganizationUnits.FirstOrDefault()?.Id ?? 0;
                    vm.OrganizationUnitDisplay = vm.OrganizationUnits.FirstOrDefault()?.DisplayText ?? string.Empty;
                }
            }

            var orgUnitId = vm.OrganizationStructureId;
            vm.IsEditable = access.CanEditOrgUnit(orgUnitId) &&
                vm.Status is WorkScheduleStatus.Draft or WorkScheduleStatus.Rejected;
            vm.CanSubmit = access.CanSubmitOrgUnit(orgUnitId) &&
                vm.Status is WorkScheduleStatus.Draft or WorkScheduleStatus.Rejected;
            vm.CanReopen = access.CanEditOrgUnit(orgUnitId) &&
                vm.Status == WorkScheduleStatus.Approved;
        }

        public static void ForceReadOnly(WorkScheduleViewModel vm)
        {
            vm.IsEditable = false;
            vm.CanSubmit = false;
            vm.CanApprove = false;
            vm.CanReject = false;
            vm.CanReopen = false;
        }

        public static void PreserveOrganizationContext(
            WorkScheduleViewModel vm,
            int organizationStructureId,
            string? organizationUnitDisplay)
        {
            if (organizationStructureId <= 0)
                return;

            vm.OrganizationStructureId = organizationStructureId;

            if (!string.IsNullOrWhiteSpace(organizationUnitDisplay))
                vm.OrganizationUnitDisplay = organizationUnitDisplay;
        }

        public static async Task ApplyWorkflowApproverFlagsAsync(
            WorkScheduleViewModel vm,
            WorkScheduleAccess access,
            IApprovalWorkflowService workflowService)
        {
            vm.CanApprove = false;
            vm.CanReject = false;

            if (vm.Status != WorkScheduleStatus.Submitted)
                return;

            if (vm.ApprovalRequestId.HasValue)
            {
                if (access.EmployeeId.HasValue)
                {
                    var canAct = await workflowService.CanActorActOnCurrentStepAsync(
                        vm.ApprovalRequestId.Value,
                        access.EmployeeId);

                    vm.CanApprove = canAct;
                    vm.CanReject = canAct;
                }

                if (!vm.CanApprove && (access.IsAdmin || access.HasGlobalApprove))
                {
                    var canOverride = await workflowService.CanActorActOnCurrentStepAsync(
                        vm.ApprovalRequestId.Value,
                        access.EmployeeId,
                        allowAdminOverride: true);

                    vm.CanApprove = canOverride;
                    vm.CanReject = canOverride;
                }

                return;
            }

            if (access.CanApproveOrgUnit(vm.OrganizationStructureId))
            {
                vm.CanApprove = true;
                vm.CanReject = true;
            }
        }

        public static async Task<bool> HasActiveOrgAssignmentAsync(
            ApplicationDbContext db,
            Guid employeeId,
            int organizationStructureId)
        {
            if (organizationStructureId <= 0)
                return false;

            var orgIds = await EmployeeOrganizationHelper
                .GetActiveOrganizationStructureIdsAsync(db, employeeId);

            return orgIds.Contains(organizationStructureId);
        }

        public static void ApplyReadOnlyViewerFlags(WorkScheduleViewModel vm)
        {
            vm.IsEditable = false;
            vm.CanSubmit = false;
            vm.CanApprove = false;
            vm.CanReject = false;
            vm.CanReopen = false;
        }

        public static void ApplyToOverviewViewModel(WorkScheduleOverviewViewModel vm, WorkScheduleAccess access)
        {
            var hasUnscopedAccess = access.IsAdmin
                || access.HasGlobalView
                || access.HasGlobalEdit
                || access.HasGlobalSubmit
                || access.HasGlobalApprove;

            if (!hasUnscopedAccess)
            {
                vm.OrganizationUnits = vm.OrganizationUnits
                    .Where(u => access.ViewOrgUnitIds.Contains(u.Id))
                    .ToList();

                if (vm.OrganizationStructureId > 0 && !access.CanViewOrgUnit(vm.OrganizationStructureId))
                {
                    vm.OrganizationStructureId = vm.OrganizationUnits.FirstOrDefault()?.Id ?? 0;
                    vm.OrganizationUnitDisplay = vm.OrganizationUnits.FirstOrDefault()?.DisplayText ?? string.Empty;
                }
            }
        }
    }
}
