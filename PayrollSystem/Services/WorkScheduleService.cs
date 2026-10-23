using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Models.Workflow;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels.WorkSchedule;

namespace PayrollSystem.Services
{
    public interface IWorkScheduleService
    {
        Task<WorkScheduleOverviewViewModel> GetOverviewViewModelAsync(int year, int? organizationStructureId = null);
        Task<WorkScheduleViewModel> GetViewModelAsync(int month, int year, int? organizationStructureId = null);
        Task<SaveEntryResult> SaveEntryAsync(SaveEntryDto dto, string userName);
        Task<(bool success, string error)> SubmitAsync(ScheduleWorkflowDto dto, string userName, Guid? submitterEmployeeId = null);
        Task<(bool success, string error)> ApproveAsync(ScheduleWorkflowDto dto, string userName, Guid? actorEmployeeId = null, bool allowAdminOverride = false);
        Task<(bool success, string error)> RejectAsync(ScheduleRejectDto dto, string userName, Guid? actorEmployeeId = null, bool allowAdminOverride = false);
        Task<(bool success, string error)> ReopenAsync(ScheduleWorkflowDto dto, string userName);
        Task SeedShiftTypesAsync();
    }

    public class WorkScheduleService : IWorkScheduleService
    {
        private readonly ApplicationDbContext _db;
        private readonly IApprovalWorkflowService _workflowService;

        public WorkScheduleService(ApplicationDbContext db, IApprovalWorkflowService workflowService)
        {
            _db = db;
            _workflowService = workflowService;
        }

        public async Task<WorkScheduleOverviewViewModel> GetOverviewViewModelAsync(int year, int? organizationStructureId = null)
        {
            var assignedOrgUnitIds = await _db.EmployeeOrganizationAssignments
                .AsNoTracking()
                .Where(a => a.EffectiveTo == null)
                .Select(a => a.OrganizationStructureId)
                .Distinct()
                .ToListAsync();

            var organizationUnits = await _db.OrganizationStructures
                .AsNoTracking()
                .Where(o => o.IsActive || assignedOrgUnitIds.Contains(o.Id))
                .OrderBy(o => o.Level)
                .ThenBy(o => o.Name)
                .Select(o => new OrganizationUnitOptionViewModel
                {
                    Id = o.Id,
                    DisplayText = o.Name
                })
                .ToListAsync();

            var resolvedOrgUnitId = organizationStructureId is > 0
                ? organizationStructureId.Value
                : organizationUnits.FirstOrDefault()?.Id ?? 0;
            var selectedUnit = organizationUnits.FirstOrDefault(u => u.Id == resolvedOrgUnitId);

            var employeeCount = 0;
            if (resolvedOrgUnitId > 0)
            {
                employeeCount = await _db.EmployeeOrganizationAssignments
                    .AsNoTracking()
                    .CountAsync(a =>
                        a.OrganizationStructureId == resolvedOrgUnitId &&
                        a.EffectiveTo == null &&
                        a.IsPrimary &&
                        a.Employee!.IsActive);
            }

            var schedules = resolvedOrgUnitId > 0
                ? await _db.WorkSchedules
                    .AsNoTracking()
                    .Where(s => s.Year == year && s.OrganizationStructureId == resolvedOrgUnitId)
                    .Select(s => new
                    {
                        s.Id,
                        s.Month,
                        s.Status,
                        s.LastModifiedAt,
                        s.SubmittedAt,
                        s.ApprovedAt,
                        EntryCount = s.Entries.Count
                    })
                    .ToListAsync()
                : [];

            var now = DateTime.Now;
            var months = Enumerable.Range(1, 12)
                .Select(month =>
                {
                    var schedule = schedules.FirstOrDefault(s => s.Month == month);
                    return new WorkScheduleMonthSummary
                    {
                        Month = month,
                        Year = year,
                        ScheduleId = schedule?.Id,
                        Status = schedule?.Status,
                        EntryCount = schedule?.EntryCount ?? 0,
                        EmployeeCount = employeeCount,
                        LastModifiedAt = schedule?.LastModifiedAt,
                        SubmittedAt = schedule?.SubmittedAt,
                        ApprovedAt = schedule?.ApprovedAt,
                        IsCurrentMonth = year == now.Year && month == now.Month
                    };
                })
                .ToList();

            return new WorkScheduleOverviewViewModel
            {
                Year = year,
                OrganizationStructureId = resolvedOrgUnitId,
                OrganizationUnitDisplay = selectedUnit?.DisplayText ?? string.Empty,
                EmployeeCount = employeeCount,
                OrganizationUnits = organizationUnits,
                Months = months
            };
        }

        public async Task<WorkScheduleViewModel> GetViewModelAsync(int month, int year, int? organizationStructureId = null)
        {
            var assignedOrgUnitIds = await _db.EmployeeOrganizationAssignments
                .AsNoTracking()
                .Where(a => a.EffectiveTo == null)
                .Select(a => a.OrganizationStructureId)
                .Distinct()
                .ToListAsync();

            var organizationUnits = await _db.OrganizationStructures
                .AsNoTracking()
                .Where(o => o.IsActive || assignedOrgUnitIds.Contains(o.Id))
                .OrderBy(o => o.Level)
                .ThenBy(o => o.Name)
                .Select(o => new OrganizationUnitOptionViewModel
                {
                    Id = o.Id,
                    DisplayText = o.Name
                })
                .ToListAsync();

            var resolvedOrgUnitId = organizationStructureId is > 0
                ? organizationStructureId.Value
                : organizationUnits.FirstOrDefault()?.Id ?? 0;
            var selectedUnit = organizationUnits.FirstOrDefault(u => u.Id == resolvedOrgUnitId);

            WorkSchedule? schedule = null;
            if (resolvedOrgUnitId > 0)
            {
                schedule = await EmployeeOrganizationHelper.FindWorkScheduleAsync(
                    _db, month, year, resolvedOrgUnitId, approvedOnly: false);
            }

            var assignmentsQuery = _db.EmployeeOrganizationAssignments
                .Include(a => a.Employee)
                    .ThenInclude(e => e!.Designations.Where(d => d.EffectiveTo == null))
                    .ThenInclude(d => d.Designation)
                .Include(a => a.OrganizationStructure)
                .Where(a => a.Employee!.IsActive && a.EffectiveTo == null && a.IsPrimary);

            if (resolvedOrgUnitId > 0)
                assignmentsQuery = assignmentsQuery.Where(a => a.OrganizationStructureId == resolvedOrgUnitId);

            var assignments = await assignmentsQuery.ToListAsync();

            if (resolvedOrgUnitId <= 0 && assignments.Count > 0)
                resolvedOrgUnitId = assignments[0].OrganizationStructureId;

            if (resolvedOrgUnitId > 0 && schedule == null)
            {
                schedule = await EmployeeOrganizationHelper.FindWorkScheduleAsync(
                    _db, month, year, resolvedOrgUnitId, approvedOnly: false);
            }

            if (schedule != null)
            {
                schedule = await _db.WorkSchedules
                    .Include(s => s.OrganizationStructure)
                    .Include(s => s.Entries)
                        .ThenInclude(e => e.ShiftType)
                    .FirstOrDefaultAsync(s => s.Id == schedule.Id);
            }

            if (resolvedOrgUnitId > 0)
            {
                selectedUnit ??= organizationUnits.FirstOrDefault(u => u.Id == resolvedOrgUnitId);
                assignments = assignments
                    .Where(a => a.OrganizationStructureId == resolvedOrgUnitId)
                    .ToList();
            }

            var shiftTypes = await _db.ShiftTypes
                .Where(s => s.IsActive)
                .OrderBy(s => s.SortOrder)
                .ToListAsync();

            var map = new Dictionary<Guid, Dictionary<int, ShiftType>>();
            if (schedule != null)
            {
                foreach (var entry in schedule.Entries)
                {
                    if (!map.ContainsKey(entry.EmployeeId))
                        map[entry.EmployeeId] = new Dictionary<int, ShiftType>();
                    map[entry.EmployeeId][entry.Day] = entry.ShiftType;
                }

                var employeeIds = assignments.Select(a => a.EmployeeId).Distinct().ToList();
                var approvedChanges = await _db.ScheduleChangeRequests
                    .AsNoTracking()
                    .Include(r => r.RequestedShiftType)
                    .Include(r => r.ApprovalRequest)
                    .Where(r => employeeIds.Contains(r.EmployeeId))
                    .Where(r => r.WorkSchedule.Month == month && r.WorkSchedule.Year == year)
                    .Where(r =>
                        (r.ApprovalRequest != null &&
                         r.ApprovalRequest.Status == ApprovalRequestStatus.Approved) ||
                        r.Status == JustificationStatus.Approved)
                    .OrderBy(r => r.SubmittedOn)
                    .ToListAsync();

                foreach (var change in approvedChanges
                    .GroupBy(c => new { c.EmployeeId, c.Day })
                    .Select(g => g.OrderByDescending(c => c.SubmittedOn).ThenByDescending(c => c.Id).First()))
                {
                    if (!map.ContainsKey(change.EmployeeId))
                        map[change.EmployeeId] = new Dictionary<int, ShiftType>();
                    map[change.EmployeeId][change.Day] = change.RequestedShiftType;
                }
            }

            var sections = assignments
                .GroupBy(a => new { a.OrganizationStructureId, a.OrganizationStructure?.Name })
                .Select(g => new SectionScheduleGroup
                {
                    OrganizationStructureId = g.Key.OrganizationStructureId,
                    Name = g.Key.Name ?? "Unknown Organization Unit",
                    Employees = g.Select(a => a.Employee!).DistinctBy(e => e.Id).ToList()
                })
                .OrderBy(g => g.Name)
                .ToList();

            var status = schedule?.Status ?? WorkScheduleStatus.Draft;

            return new WorkScheduleViewModel
            {
                OrganizationStructureId = resolvedOrgUnitId,
                OrganizationUnitDisplay = selectedUnit?.DisplayText
                    ?? schedule?.OrganizationStructure?.Name
                    ?? string.Empty,
                Month = month,
                Year = year,
                Status = status,
                IsEditable = status is WorkScheduleStatus.Draft or WorkScheduleStatus.Rejected,
                CanSubmit = status is WorkScheduleStatus.Draft or WorkScheduleStatus.Rejected,
                CanApprove = status == WorkScheduleStatus.Submitted,
                CanReject = status == WorkScheduleStatus.Submitted,
                CanReopen = status == WorkScheduleStatus.Approved,
                RejectionReason = schedule?.RejectionReason,
                SubmittedBy = schedule?.SubmittedBy,
                SubmittedAt = schedule?.SubmittedAt,
                ApprovedBy = schedule?.ApprovedBy,
                ApprovedAt = schedule?.ApprovedAt,
                ScheduleId = schedule?.Id ?? 0,
                ApprovalRequestId = schedule?.ApprovalRequestId,
                OrganizationUnits = organizationUnits,
                ShiftTypes = shiftTypes,
                ScheduleMap = map,
                Sections = sections
            };
        }

        public async Task<SaveEntryResult> SaveEntryAsync(SaveEntryDto dto, string userName)
        {
            try
            {
                var orgUnitId = await ResolveOrganizationStructureIdAsync(
                    dto.OrganizationStructureId, dto.EmployeeId);
                if (orgUnitId <= 0)
                    return new SaveEntryResult { Success = false, Error = "Organization unit is required." };

                var primaryOrgUnitId = await EmployeeOrganizationHelper
                    .GetPrimaryOrganizationStructureIdAsync(_db, dto.EmployeeId);
                if (!primaryOrgUnitId.HasValue)
                    return new SaveEntryResult { Success = false, Error = "Employee has no active organization assignment." };

                if (primaryOrgUnitId.Value != orgUnitId)
                    return new SaveEntryResult
                    {
                        Success = false,
                        Error = "This employee belongs to another unit's work schedule. Update their primary organization assignment to move them."
                    };

                var schedule = await GetOrCreateScheduleForUpdateAsync(
                    dto.Month, dto.Year, orgUnitId, userName);

                if (!schedule.IsEditable)
                    return new SaveEntryResult { Success = false, Error = "Schedule is not editable in its current status." };

                var entry = await _db.ScheduleEntries
                    .AsTracking()
                    .FirstOrDefaultAsync(e =>
                        e.WorkScheduleId == schedule.Id &&
                        e.EmployeeId == dto.EmployeeId &&
                        e.Day == dto.Day);

                if (entry == null)
                {
                    entry = new ScheduleEntry
                    {
                        WorkScheduleId = schedule.Id,
                        EmployeeId = dto.EmployeeId,
                        Day = dto.Day,
                        ShiftTypeId = dto.ShiftTypeId,
                        Remarks = dto.Remarks,
                        CreatedBy = userName
                    };
                    _db.ScheduleEntries.Add(entry);
                }
                else
                {
                    entry.ShiftTypeId = dto.ShiftTypeId;
                    entry.Remarks = dto.Remarks;
                }

                schedule.LastModifiedBy = userName;
                schedule.LastModifiedAt = DateTime.Now;

                await _db.SaveChangesAsync();

                var summary = await ComputeSummaryAsync(schedule.Id, dto.EmployeeId);
                return new SaveEntryResult { Success = true, UpdatedSummary = summary };
            }
            catch (Exception ex)
            {
                return new SaveEntryResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<(bool success, string error)> SubmitAsync(
            ScheduleWorkflowDto dto,
            string userName,
            Guid? submitterEmployeeId = null)
        {
            var orgUnitId = await ResolveOrganizationStructureIdAsync(dto.OrganizationStructureId);
            if (orgUnitId <= 0)
                return (false, "Organization unit is required.");

            var schedule = await FindScheduleForUpdateAsync(dto.Month, dto.Year, orgUnitId);

            if (schedule == null)
                return (false, "Schedule not found. Add at least one entry before submitting.");

            if (schedule.Status != WorkScheduleStatus.Draft && schedule.Status != WorkScheduleStatus.Rejected)
                return (false, "Only draft or rejected schedules can be submitted.");

            var (created, createError, request) = await _workflowService.CreateRequestAsync(
                WorkflowProcessType.WorkSchedule,
                schedule.Id.ToString(),
                submitterEmployeeId,
                schedule.OrganizationStructureId,
                userName);

            if (!created || request == null)
                return (false, createError);

            if (request.Status == ApprovalRequestStatus.Approved)
            {
                schedule.Status = WorkScheduleStatus.Approved;
                schedule.ApprovedBy = userName;
                schedule.ApprovedAt = DateTime.Now;
                schedule.IsLocked = true;
                schedule.LockedBy = userName;
                schedule.LockedAt = DateTime.Now;
            }
            else
            {
                schedule.Status = WorkScheduleStatus.Submitted;
            }

            schedule.SubmittedBy = userName;
            schedule.SubmittedAt = DateTime.Now;
            schedule.SubmittedByEmployeeId = submitterEmployeeId;
            schedule.ApprovalRequestId = request.Id;
            schedule.RejectedBy = null;
            schedule.RejectedAt = null;
            schedule.RejectionReason = null;
            schedule.LastModifiedBy = userName;
            schedule.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            return (true, string.Empty);
        }

        public async Task<(bool success, string error)> ApproveAsync(
            ScheduleWorkflowDto dto,
            string userName,
            Guid? actorEmployeeId = null,
            bool allowAdminOverride = false)
        {
            var orgUnitId = await ResolveOrganizationStructureIdAsync(dto.OrganizationStructureId);
            if (orgUnitId <= 0)
                return (false, "Organization unit is required.");

            var schedule = await FindScheduleForUpdateAsync(dto.Month, dto.Year, orgUnitId);

            if (schedule == null)
                return (false, "Schedule not found.");

            if (schedule.Status != WorkScheduleStatus.Submitted)
                return (false, "Only submitted schedules can be approved.");

            if (!schedule.ApprovalRequestId.HasValue)
                return (false, "No approval request found for this schedule.");

            var (approved, approveError) = await _workflowService.ApproveStepAsync(
                schedule.ApprovalRequestId.Value,
                actorEmployeeId,
                userName,
                allowAdminOverride: allowAdminOverride);

            if (!approved)
                return (false, approveError);

            var request = await _db.ApprovalRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == schedule.ApprovalRequestId.Value);

            if (request?.Status == ApprovalRequestStatus.Approved)
            {
                schedule.Status = WorkScheduleStatus.Approved;
                schedule.ApprovedBy = userName;
                schedule.ApprovedAt = DateTime.Now;
                schedule.IsLocked = true;
                schedule.LockedBy = userName;
                schedule.LockedAt = DateTime.Now;
            }

            schedule.LastModifiedBy = userName;
            schedule.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            return (true, string.Empty);
        }

        public async Task<(bool success, string error)> RejectAsync(
            ScheduleRejectDto dto,
            string userName,
            Guid? actorEmployeeId = null,
            bool allowAdminOverride = false)
        {
            if (string.IsNullOrWhiteSpace(dto.Reason))
                return (false, "Rejection reason is required.");

            var orgUnitId = await ResolveOrganizationStructureIdAsync(dto.OrganizationStructureId);
            if (orgUnitId <= 0)
                return (false, "Organization unit is required.");

            var schedule = await FindScheduleForUpdateAsync(dto.Month, dto.Year, orgUnitId);

            if (schedule == null)
                return (false, "Schedule not found.");

            if (schedule.Status != WorkScheduleStatus.Submitted)
                return (false, "Only submitted schedules can be rejected.");

            if (schedule.ApprovalRequestId.HasValue)
            {
                var (rejected, rejectError) = await _workflowService.RejectStepAsync(
                    schedule.ApprovalRequestId.Value,
                    actorEmployeeId,
                    userName,
                    dto.Reason.Trim(),
                    allowAdminOverride);

                if (!rejected)
                    return (false, rejectError);
            }

            schedule.Status = WorkScheduleStatus.Rejected;
            schedule.RejectedBy = userName;
            schedule.RejectedAt = DateTime.Now;
            schedule.RejectionReason = dto.Reason.Trim();
            schedule.IsLocked = false;
            schedule.LastModifiedBy = userName;
            schedule.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            return (true, string.Empty);
        }

        public async Task<(bool success, string error)> ReopenAsync(ScheduleWorkflowDto dto, string userName)
        {
            var orgUnitId = await ResolveOrganizationStructureIdAsync(dto.OrganizationStructureId);
            if (orgUnitId <= 0)
                return (false, "Organization unit is required.");

            var schedule = await FindScheduleForUpdateAsync(dto.Month, dto.Year, orgUnitId);

            if (schedule == null)
                return (false, "Schedule not found.");

            if (schedule.Status != WorkScheduleStatus.Approved)
                return (false, "Only approved schedules can be reopened to draft.");

            schedule.Status = WorkScheduleStatus.Draft;
            schedule.IsLocked = false;
            schedule.LockedBy = null;
            schedule.LockedAt = null;
            schedule.ApprovedBy = null;
            schedule.ApprovedAt = null;
            schedule.ApprovalRequestId = null;
            schedule.SubmittedByEmployeeId = null;
            schedule.LastModifiedBy = userName;
            schedule.LastModifiedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            return (true, string.Empty);
        }

        public async Task SeedShiftTypesAsync()
        {
            if (await _db.ShiftTypes.AnyAsync()) return;

            _db.ShiftTypes.AddRange(new[]
            {
                new ShiftType { Code = "REG-8-5", Label = "Regular Day (8am–5pm)", Hours = 8m, IsLeave = false, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17), HasLunchBreak = true, LunchStart = TimeSpan.FromHours(12), LunchEnd = TimeSpan.FromHours(13), GracePeriodMinutes = 15, ColorHex = "#2563eb", SortOrder = 1, IsActive = true },
                new ShiftType { Code = "7AM-3PM", Label = "Early Shift (7am–3pm)", Hours = 8m, IsLeave = false, StartTime = TimeSpan.FromHours(7), EndTime = TimeSpan.FromHours(15), HasLunchBreak = false, GracePeriodMinutes = 15, ColorHex = "#0891b2", SortOrder = 2, IsActive = true },
                new ShiftType { Code = "7PM-7AM", Label = "Night Shift (7pm–7am)", Hours = 12m, IsLeave = false, StartTime = TimeSpan.FromHours(19), EndTime = TimeSpan.FromHours(7), SpansNextDay = true, HasLunchBreak = false, GracePeriodMinutes = 15, ColorHex = "#4f46e5", SortOrder = 3, IsActive = true },
                new ShiftType { Code = "VL", Label = "Vacation Leave", Hours = 8m, IsLeave = true, ColorHex = "#f59e0b", SortOrder = 10, IsActive = true },
                new ShiftType { Code = "SL", Label = "Sick Leave", Hours = 8m, IsLeave = true, ColorHex = "#ef4444", SortOrder = 11, IsActive = true },
                new ShiftType { Code = "DAY OFF", Label = "Day Off", Hours = 0m, IsLeave = true, ColorHex = "#9ca3af", SortOrder = 20, IsActive = true },
                new ShiftType { Code = "HOL", Label = "Holiday", Hours = 0m, IsLeave = true, ColorHex = "#10b981", SortOrder = 21, IsActive = true },
            });
            await _db.SaveChangesAsync();
        }

        private static Task<WorkSchedule?> FindScheduleAsync(
            ApplicationDbContext db,
            int month,
            int year,
            int organizationStructureId) =>
            db.WorkSchedules.FirstOrDefaultAsync(s =>
                s.Month == month &&
                s.Year == year &&
                s.OrganizationStructureId == organizationStructureId);

        private Task<WorkSchedule?> FindScheduleAsync(int month, int year, int organizationStructureId) =>
            FindScheduleAsync(_db, month, year, organizationStructureId);

        private Task<WorkSchedule?> FindScheduleForUpdateAsync(int month, int year, int organizationStructureId) =>
            _db.WorkSchedules
                .AsTracking()
                .FirstOrDefaultAsync(s =>
                    s.Month == month &&
                    s.Year == year &&
                    s.OrganizationStructureId == organizationStructureId);

        private async Task<int> ResolveOrganizationStructureIdAsync(
            int organizationStructureId,
            Guid? employeeId = null)
        {
            if (organizationStructureId > 0)
                return organizationStructureId;

            if (employeeId.HasValue)
            {
                var fromPrimary = await EmployeeOrganizationHelper
                    .GetPrimaryOrganizationStructureIdAsync(_db, employeeId.Value);
                if (fromPrimary.HasValue)
                    return fromPrimary.Value;

                var fromEmployee = await _db.EmployeeOrganizationAssignments
                    .AsNoTracking()
                    .Where(a => a.EmployeeId == employeeId.Value && a.EffectiveTo == null)
                    .OrderBy(a => a.OrganizationStructureId)
                    .Select(a => a.OrganizationStructureId)
                    .FirstOrDefaultAsync();

                if (fromEmployee > 0)
                    return fromEmployee;
            }

            return await _db.OrganizationStructures
                .AsNoTracking()
                .Where(o => o.IsActive)
                .OrderBy(o => o.Level)
                .ThenBy(o => o.Name)
                .Select(o => o.Id)
                .FirstOrDefaultAsync();
        }

        private async Task<WorkSchedule> GetOrCreateScheduleForUpdateAsync(
            int month,
            int year,
            int organizationStructureId,
            string userName)
        {
            var schedule = await FindScheduleForUpdateAsync(month, year, organizationStructureId);

            if (schedule == null)
            {
                schedule = new WorkSchedule
                {
                    Month = month,
                    Year = year,
                    OrganizationStructureId = organizationStructureId,
                    Status = WorkScheduleStatus.Draft,
                    CreatedBy = userName,
                    CreatedAt = DateTime.Now,
                    IsLocked = false
                };
                _db.WorkSchedules.Add(schedule);
                await _db.SaveChangesAsync();
            }

            return schedule;
        }

        private async Task<EmployeeSummaryDto> ComputeSummaryAsync(int scheduleId, Guid employeeId)
        {
            var entries = await _db.ScheduleEntries
                .Include(e => e.ShiftType)
                .Where(e => e.WorkScheduleId == scheduleId && e.EmployeeId == employeeId)
                .ToListAsync();

            return new EmployeeSummaryDto
            {
                EmployeeId = employeeId,
                DutyDays = entries.Count(e => !e.ShiftType.IsLeave),
                DutyHours = entries.Where(e => !e.ShiftType.IsLeave).Sum(e => e.ShiftType.Hours),
                LeaveOffCount = entries.Count(e => e.ShiftType.IsLeave)
            };
        }
    }
}
