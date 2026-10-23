using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.Services
{
    public interface IScheduleChangeRequestService
    {
        Task<(bool success, string message)> SubmitAsync(
            ScheduleChangeSubmitDto dto,
            string submittedBy,
            Guid? submitterEmployeeId = null);
        Task<(bool success, string message)> ProcessAsync(
            Guid id,
            JustificationStatus action,
            string? reviewNotes,
            string reviewerId,
            string reviewerName,
            Guid? actorEmployeeId = null,
            bool allowAdminOverride = false);
        Task SyncApprovedChangesForEmployeeMonthAsync(Guid employeeId, int month, int year);
        Task SyncApprovedChangesForWorkScheduleAsync(int workScheduleId);
        Task<(bool success, string message)> ApplyApprovedChangeByIdAsync(
            Guid requestId,
            string reviewerName,
            string? reviewerId = null,
            string? reviewNotes = null);
        Task<(bool success, string message)> ApplyEmployeeDayShiftAsync(
            Guid employeeId,
            DateOnly date,
            int shiftTypeId,
            string modifiedBy);

        // Applies multiple employee/day shift updates atomically (single DB transaction).
        Task<(bool success, string message)> ApplyEmployeeDayShiftsAsync(
            IReadOnlyList<(Guid EmployeeId, DateOnly Date, int ShiftTypeId)> updates,
            string modifiedBy);
    }

    public class ScheduleChangeSubmitDto
    {
        public Guid EmployeeId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public int Day { get; set; }
        public int RequestedShiftTypeId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ScheduleChangeRequestService : IScheduleChangeRequestService
    {
        private readonly ApplicationDbContext _db;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly IScheduleTransactionConflictService _conflictService;

        public ScheduleChangeRequestService(
            ApplicationDbContext db,
            IApprovalWorkflowService workflowService,
            IScheduleTransactionConflictService conflictService)
        {
            _db = db;
            _workflowService = workflowService;
            _conflictService = conflictService;
        }

        public async Task<(bool success, string message)> SubmitAsync(
            ScheduleChangeSubmitDto dto,
            string submittedBy,
            Guid? submitterEmployeeId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Reason))
                return (false, "Reason is required.");

            var schedule = await EmployeeOrganizationHelper.ResolveEmployeeMonthWorkScheduleAsync(
                _db, dto.EmployeeId, dto.Month, dto.Year, approvedOnly: true);

            if (schedule == null)
                return (false, "No work schedule exists for this month.");

            if (schedule.Status != WorkScheduleStatus.Approved)
                return (false, "Schedule change requests are only allowed when the monthly schedule is approved.");

            var requestDate = new DateOnly(dto.Year, dto.Month, dto.Day);
            var (hasConflict, conflictMessage) = await _conflictService.FindConflictAsync(
                dto.EmployeeId, requestDate);
            if (hasConflict)
                return (false, conflictMessage);

            var entry = await FindApprovedMonthEntryAsync(
                dto.EmployeeId, dto.Month, dto.Year, dto.Day, schedule.Id);

            var currentShiftId = await ResolveEffectiveShiftTypeIdAsync(
                dto.EmployeeId, dto.Month, dto.Year, dto.Day, schedule.Id, entry?.ShiftTypeId);

            if (currentShiftId.HasValue && currentShiftId.Value == dto.RequestedShiftTypeId)
                return (false, "Requested shift is the same as the current schedule.");

            var effectiveScheduleId = entry?.WorkScheduleId ?? schedule.Id;
            if (effectiveScheduleId != schedule.Id)
            {
                schedule = await _db.WorkSchedules
                    .AsNoTracking()
                    .FirstAsync(s => s.Id == effectiveScheduleId);
            }

            var request = new ScheduleChangeRequest
            {
                EmployeeId = dto.EmployeeId,
                WorkScheduleId = effectiveScheduleId,
                Day = dto.Day,
                CurrentShiftTypeId = currentShiftId ?? dto.RequestedShiftTypeId,
                RequestedShiftTypeId = dto.RequestedShiftTypeId,
                Reason = dto.Reason.Trim(),
                Status = JustificationStatus.Pending,
                SubmittedBy = submittedBy,
                SubmittedOn = DateTime.Now
            };

            _db.ScheduleChangeRequests.Add(request);
            await _db.SaveChangesAsync();

            var (created, createError, approvalRequest) = await _workflowService.CreateRequestAsync(
                WorkflowProcessType.ScheduleChange,
                request.Id.ToString(),
                submitterEmployeeId ?? dto.EmployeeId,
                schedule.OrganizationStructureId,
                submittedBy);

            if (!created || approvalRequest == null)
            {
                _db.ScheduleChangeRequests.Remove(request);
                await _db.SaveChangesAsync();
                return (false, createError);
            }

            request.ApprovalRequestId = approvalRequest.Id;

            if (approvalRequest.Status == ApprovalRequestStatus.Approved)
            {
                var applyResult = await ApplyApprovedRequestAsync(request.Id, submittedBy);
                if (!applyResult.success)
                    return applyResult;
            }
            else
            {
                await _db.SaveChangesAsync();
            }

            return (true, "Schedule change request submitted successfully.");
        }

        public async Task<(bool success, string message)> ProcessAsync(
            Guid id,
            JustificationStatus action,
            string? reviewNotes,
            string reviewerId,
            string reviewerName,
            Guid? actorEmployeeId = null,
            bool allowAdminOverride = false)
        {
            if (action != JustificationStatus.Approved && action != JustificationStatus.Rejected)
                return (false, "Invalid action.");

            if (action == JustificationStatus.Rejected && string.IsNullOrWhiteSpace(reviewNotes))
                return (false, "Review notes are required when rejecting.");

            var request = await _db.ScheduleChangeRequests
                .Include(r => r.WorkSchedule)
                .Include(r => r.ApprovalRequest)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return (false, "Request not found.");

            var workflowStatus = request.ApprovalRequest?.Status;
            if (!ApprovalRequestStatusHelper.IsPending(request.Status, workflowStatus))
            {
                if (workflowStatus == ApprovalRequestStatus.Approved)
                {
                    var alreadyApplied = await IsChangeAppliedAsync(
                        request.EmployeeId,
                        request.Day,
                        request.WorkSchedule.Month,
                        request.WorkSchedule.Year,
                        request.RequestedShiftTypeId);

                    if (request.Status != JustificationStatus.Approved || !alreadyApplied)
                        return await ApplyApprovedRequestAsync(id, reviewerName, reviewerId, reviewNotes);
                }

                return (false, "Request has already been processed.");
            }

            if (action == JustificationStatus.Approved)
            {
                if (!request.ApprovalRequestId.HasValue)
                    return (false, "No approval request found for this schedule change.");

                var (approved, approveError) = await _workflowService.ApproveStepAsync(
                    request.ApprovalRequestId.Value,
                    actorEmployeeId,
                    reviewerName,
                    reviewNotes,
                    allowAdminOverride);

                if (!approved)
                    return (false, approveError);

                var finalWorkflowStatus = await _db.ApprovalRequests
                    .AsNoTracking()
                    .Where(r => r.Id == request.ApprovalRequestId.Value)
                    .Select(r => r.Status)
                    .FirstOrDefaultAsync();

                if (finalWorkflowStatus == ApprovalRequestStatus.Approved)
                {
                    var applyResult = await ApplyApprovedRequestAsync(id, reviewerName, reviewerId, reviewNotes);
                    if (!applyResult.success)
                        return applyResult;

                    return (true, "Schedule change approved and applied.");
                }

                request.ReviewNotes = reviewNotes?.Trim();
                await _db.SaveChangesAsync();
                return (true, "Step approved. The request moved to the next approver.");
            }

            if (!request.ApprovalRequestId.HasValue)
                return (false, "No approval request found for this schedule change.");

            var (rejected, rejectError) = await _workflowService.RejectStepAsync(
                request.ApprovalRequestId.Value,
                actorEmployeeId,
                reviewerName,
                reviewNotes ?? string.Empty,
                allowAdminOverride);

            if (!rejected)
                return (false, rejectError);

            request.Status = JustificationStatus.Rejected;
            request.ReviewedById = reviewerId;
            request.ReviewedOn = DateTime.Now;
            request.ReviewNotes = reviewNotes?.Trim();
            await _db.SaveChangesAsync();

            return (true, "Schedule change request rejected.");
        }

        public Task<(bool success, string message)> ApplyApprovedChangeByIdAsync(
            Guid requestId,
            string reviewerName,
            string? reviewerId = null,
            string? reviewNotes = null) =>
            ApplyApprovedRequestAsync(requestId, reviewerName, reviewerId, reviewNotes);

        public Task<(bool success, string message)> ApplyEmployeeDayShiftAsync(
            Guid employeeId,
            DateOnly date,
            int shiftTypeId,
            string modifiedBy) =>
            ApplyEmployeeDayShiftCoreAsync(employeeId, date, shiftTypeId, modifiedBy);

        public async Task<(bool success, string message)> ApplyEmployeeDayShiftsAsync(
            IReadOnlyList<(Guid EmployeeId, DateOnly Date, int ShiftTypeId)> updates,
            string modifiedBy)
        {
            if (updates == null || updates.Count == 0)
                return (true, "No schedule updates to apply.");

            var ownsTransaction = _db.Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction
                ? await _db.Database.BeginTransactionAsync()
                : null;

            try
            {
                foreach (var update in updates)
                {
                    var result = await ApplyEmployeeDayShiftCoreAsync(
                        update.EmployeeId,
                        update.Date,
                        update.ShiftTypeId,
                        modifiedBy);

                    if (!result.success)
                    {
                        if (transaction != null)
                            await transaction.RollbackAsync();
                        return result;
                    }
                }

                if (transaction != null)
                    await transaction.CommitAsync();

                return (true, "Schedule updates applied.");
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<(bool success, string message)> ApplyEmployeeDayShiftCoreAsync(
            Guid employeeId,
            DateOnly date,
            int shiftTypeId,
            string modifiedBy)
        {
            var schedule = await EmployeeOrganizationHelper.ResolveEmployeeMonthWorkScheduleAsync(
                _db, employeeId, date.Month, date.Year, approvedOnly: true);

            if (schedule == null)
                return (false, $"No approved work schedule exists for {date:MMMM yyyy}.");

            if (schedule.Status != WorkScheduleStatus.Approved)
                return (false, "Schedule updates require an approved monthly schedule.");

            if (await IsChangeAppliedAsync(employeeId, date.Day, date.Month, date.Year, shiftTypeId))
                return (true, "Shift already applied.");

            _db.ChangeTracker.Clear();

            var approvedScheduleIds = await _db.WorkSchedules
                .AsNoTracking()
                .Where(s => s.Month == date.Month &&
                            s.Year == date.Year &&
                            s.Status == WorkScheduleStatus.Approved)
                .Select(s => s.Id)
                .ToListAsync();

            var rowsUpdated = 0;
            if (approvedScheduleIds.Count > 0)
            {
                rowsUpdated = await _db.ScheduleEntries
                    .Where(e => approvedScheduleIds.Contains(e.WorkScheduleId) &&
                                e.EmployeeId == employeeId &&
                                e.Day == date.Day)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.ShiftTypeId, shiftTypeId));
            }

            var touchedScheduleIds = new HashSet<int>();
            if (rowsUpdated > 0)
            {
                var touched = await _db.ScheduleEntries
                    .AsNoTracking()
                    .Where(e => approvedScheduleIds.Contains(e.WorkScheduleId) &&
                                e.EmployeeId == employeeId &&
                                e.Day == date.Day &&
                                e.ShiftTypeId == shiftTypeId)
                    .Select(e => e.WorkScheduleId)
                    .Distinct()
                    .ToListAsync();

                foreach (var scheduleId in touched)
                    touchedScheduleIds.Add(scheduleId);
            }
            else
            {
                var targetScheduleId = await ResolveApplyTargetScheduleIdAsync(
                    employeeId,
                    date.Month,
                    date.Year,
                    schedule.Id,
                    schedule.OrganizationStructureId);

                _db.ScheduleEntries.Add(new ScheduleEntry
                {
                    WorkScheduleId = targetScheduleId,
                    EmployeeId = employeeId,
                    Day = date.Day,
                    ShiftTypeId = shiftTypeId,
                    CreatedBy = modifiedBy,
                    CreatedAt = DateTime.Now
                });
                touchedScheduleIds.Add(targetScheduleId);
                await _db.SaveChangesAsync();
            }

            var now = DateTime.Now;
            foreach (var scheduleId in touchedScheduleIds)
            {
                await _db.WorkSchedules
                    .Where(s => s.Id == scheduleId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(w => w.LastModifiedBy, modifiedBy)
                        .SetProperty(w => w.LastModifiedAt, now));
            }

            return (true, "Schedule updated.");
        }

        private async Task<(bool success, string message)> ApplyApprovedRequestAsync(
            Guid requestId,
            string reviewerName,
            string? reviewerId = null,
            string? reviewNotes = null)
        {
            var request = await _db.ScheduleChangeRequests
                .AsNoTracking()
                .Where(r => r.Id == requestId)
                .Select(r => new
                {
                    r.Id,
                    r.WorkScheduleId,
                    r.EmployeeId,
                    r.Day,
                    r.RequestedShiftTypeId,
                    r.Status,
                    r.ReviewedById,
                    Month = r.WorkSchedule.Month,
                    Year = r.WorkSchedule.Year,
                    OrganizationStructureId = r.WorkSchedule.OrganizationStructureId
                })
                .FirstOrDefaultAsync();

            if (request == null)
                return (false, "Request not found.");

            if (await IsChangeAppliedAsync(
                    request.EmployeeId,
                    request.Day,
                    request.Month,
                    request.Year,
                    request.RequestedShiftTypeId))
            {
                if (request.Status != JustificationStatus.Approved)
                    await MarkRequestApprovedAsync(requestId, reviewerId ?? request.ReviewedById, reviewNotes);

                return (true, "Schedule change is already applied.");
            }

            var requestDate = new DateOnly(request.Year, request.Month, request.Day);
            var (hasConflict, conflictMessage) = await _conflictService.FindConflictAsync(
                request.EmployeeId,
                requestDate,
                new ScheduleTransactionExclusion { ScheduleChangeRequestId = request.Id });
            if (hasConflict)
                return (false, conflictMessage);

            _db.ChangeTracker.Clear();

            var approvedScheduleIds = await _db.WorkSchedules
                .AsNoTracking()
                .Where(s => s.Month == request.Month &&
                            s.Year == request.Year &&
                            s.Status == WorkScheduleStatus.Approved)
                .Select(s => s.Id)
                .ToListAsync();

            var rowsUpdated = 0;
            if (approvedScheduleIds.Count > 0)
            {
                rowsUpdated = await _db.ScheduleEntries
                    .Where(e => approvedScheduleIds.Contains(e.WorkScheduleId) &&
                                e.EmployeeId == request.EmployeeId &&
                                e.Day == request.Day)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.ShiftTypeId, request.RequestedShiftTypeId));
            }

            var touchedScheduleIds = new HashSet<int>();
            if (rowsUpdated > 0)
            {
                var touched = await _db.ScheduleEntries
                    .AsNoTracking()
                    .Where(e => approvedScheduleIds.Contains(e.WorkScheduleId) &&
                                e.EmployeeId == request.EmployeeId &&
                                e.Day == request.Day &&
                                e.ShiftTypeId == request.RequestedShiftTypeId)
                    .Select(e => e.WorkScheduleId)
                    .Distinct()
                    .ToListAsync();

                foreach (var scheduleId in touched)
                    touchedScheduleIds.Add(scheduleId);
            }
            else
            {
                var targetScheduleId = await ResolveApplyTargetScheduleIdAsync(
                    request.EmployeeId,
                    request.Month,
                    request.Year,
                    request.WorkScheduleId,
                    request.OrganizationStructureId);

                _db.ScheduleEntries.Add(new ScheduleEntry
                {
                    WorkScheduleId = targetScheduleId,
                    EmployeeId = request.EmployeeId,
                    Day = request.Day,
                    ShiftTypeId = request.RequestedShiftTypeId,
                    CreatedBy = reviewerName,
                    CreatedAt = DateTime.Now
                });
                touchedScheduleIds.Add(targetScheduleId);
                await _db.SaveChangesAsync();
            }

            var now = DateTime.Now;
            foreach (var scheduleId in touchedScheduleIds)
            {
                await _db.WorkSchedules
                    .Where(s => s.Id == scheduleId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(w => w.LastModifiedBy, reviewerName)
                        .SetProperty(w => w.LastModifiedAt, now));
            }

            await MarkRequestApprovedAsync(requestId, reviewerId ?? request.ReviewedById, reviewNotes);

            return (true, "Schedule change approved and applied.");
        }

        private async Task MarkRequestApprovedAsync(Guid requestId, string? reviewerId, string? reviewNotes)
        {
            var trimmedNotes = string.IsNullOrWhiteSpace(reviewNotes) ? null : reviewNotes.Trim();

            await _db.ScheduleChangeRequests
                .Where(r => r.Id == requestId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, JustificationStatus.Approved)
                    .SetProperty(r => r.ReviewedOn, DateTime.Now)
                    .SetProperty(r => r.ReviewNotes, trimmedNotes)
                    .SetProperty(r => r.ReviewedById, reviewerId));
        }

        private async Task<bool> IsChangeAppliedAsync(
            Guid employeeId,
            int day,
            int month,
            int year,
            int requestedShiftTypeId)
        {
            var approvedShifts = await _db.ScheduleEntries
                .AsNoTracking()
                .Where(e => e.EmployeeId == employeeId && e.Day == day)
                .Where(e => e.WorkSchedule.Month == month && e.WorkSchedule.Year == year)
                .Where(e => e.WorkSchedule.Status == WorkScheduleStatus.Approved)
                .Select(e => e.ShiftTypeId)
                .ToListAsync();

            if (approvedShifts.Count == 0)
                return false;

            return approvedShifts.All(shiftId => shiftId == requestedShiftTypeId);
        }

        private async Task<int?> ResolveEffectiveShiftTypeIdAsync(
            Guid employeeId,
            int month,
            int year,
            int day,
            int preferredWorkScheduleId,
            int? entryShiftTypeId)
        {
            var latestApprovedChange = await _db.ScheduleChangeRequests
                .AsNoTracking()
                .Include(r => r.ApprovalRequest)
                .Include(r => r.WorkSchedule)
                .Where(r => r.EmployeeId == employeeId &&
                            r.Day == day &&
                            r.WorkSchedule.Month == month &&
                            r.WorkSchedule.Year == year)
                .Where(r =>
                    (r.ApprovalRequest != null && r.ApprovalRequest.Status == ApprovalRequestStatus.Approved) ||
                    r.Status == JustificationStatus.Approved)
                .OrderByDescending(r => r.SubmittedOn)
                .ThenByDescending(r => r.Id)
                .Select(r => (int?)r.RequestedShiftTypeId)
                .FirstOrDefaultAsync();

            if (latestApprovedChange.HasValue)
                return latestApprovedChange;

            if (entryShiftTypeId.HasValue)
                return entryShiftTypeId;

            return await _db.ScheduleEntries
                .AsNoTracking()
                .Where(e => e.EmployeeId == employeeId && e.Day == day)
                .Where(e => e.WorkSchedule.Month == month && e.WorkSchedule.Year == year)
                .Where(e => e.WorkSchedule.Status == WorkScheduleStatus.Approved)
                .OrderByDescending(e => e.WorkScheduleId == preferredWorkScheduleId)
                .ThenByDescending(e => e.WorkSchedule.LastModifiedAt ?? e.WorkSchedule.CreatedAt)
                .Select(e => (int?)e.ShiftTypeId)
                .FirstOrDefaultAsync();
        }

        private async Task<ScheduleEntry?> FindApprovedMonthEntryAsync(
            Guid employeeId,
            int month,
            int year,
            int day,
            int preferredWorkScheduleId)
        {
            return await _db.ScheduleEntries
                .AsNoTracking()
                .Where(e => e.EmployeeId == employeeId && e.Day == day)
                .Where(e => e.WorkSchedule.Month == month && e.WorkSchedule.Year == year)
                .Where(e => e.WorkSchedule.Status == WorkScheduleStatus.Approved)
                .OrderByDescending(e => e.WorkScheduleId == preferredWorkScheduleId)
                .ThenByDescending(e => e.WorkSchedule.LastModifiedAt ?? e.WorkSchedule.CreatedAt)
                .FirstOrDefaultAsync();
        }

        private async Task<int> ResolveApplyTargetScheduleIdAsync(
            Guid employeeId,
            int month,
            int year,
            int preferredWorkScheduleId,
            int organizationStructureId)
        {
            var existingEntryScheduleId = await _db.ScheduleEntries
                .AsNoTracking()
                .Where(e => e.EmployeeId == employeeId)
                .Where(e => e.WorkSchedule.Month == month && e.WorkSchedule.Year == year)
                .Where(e => e.WorkSchedule.Status == WorkScheduleStatus.Approved)
                .OrderByDescending(e => e.WorkScheduleId == preferredWorkScheduleId)
                .ThenByDescending(e => e.WorkSchedule.LastModifiedAt ?? e.WorkSchedule.CreatedAt)
                .Select(e => (int?)e.WorkScheduleId)
                .FirstOrDefaultAsync();

            if (existingEntryScheduleId.HasValue)
                return existingEntryScheduleId.Value;

            var resolved = await EmployeeOrganizationHelper.ResolveEmployeeMonthWorkScheduleAsync(
                _db, employeeId, month, year, organizationStructureId, approvedOnly: true);

            return resolved?.Id ?? preferredWorkScheduleId;
        }

        public async Task SyncApprovedChangesForEmployeeMonthAsync(Guid employeeId, int month, int year)
        {
            var requests = await _db.ScheduleChangeRequests
                .AsNoTracking()
                .Include(r => r.ApprovalRequest)
                .Include(r => r.WorkSchedule)
                .Where(r => r.EmployeeId == employeeId &&
                            r.WorkSchedule.Month == month &&
                            r.WorkSchedule.Year == year)
                .ToListAsync();

            foreach (var request in requests)
                await TryApplyApprovedChangeAsync(request);
        }

        public async Task SyncApprovedChangesForWorkScheduleAsync(int workScheduleId)
        {
            var schedule = await _db.WorkSchedules
                .AsNoTracking()
                .Where(s => s.Id == workScheduleId)
                .Select(s => new { s.Month, s.Year })
                .FirstOrDefaultAsync();

            if (schedule == null)
                return;

            var employeeIds = await _db.ScheduleEntries
                .AsNoTracking()
                .Where(e => e.WorkScheduleId == workScheduleId)
                .Select(e => e.EmployeeId)
                .Distinct()
                .ToListAsync();

            var requests = await _db.ScheduleChangeRequests
                .AsNoTracking()
                .Include(r => r.ApprovalRequest)
                .Include(r => r.WorkSchedule)
                .Where(r => r.WorkSchedule.Month == schedule.Month && r.WorkSchedule.Year == schedule.Year)
                .Where(r => r.WorkScheduleId == workScheduleId || employeeIds.Contains(r.EmployeeId))
                .ToListAsync();

            foreach (var request in requests.DistinctBy(r => r.Id))
                await TryApplyApprovedChangeAsync(request);
        }

        private async Task TryApplyApprovedChangeAsync(ScheduleChangeRequest request)
        {
            var workflowApproved = request.ApprovalRequest?.Status == ApprovalRequestStatus.Approved;
            if (!workflowApproved && request.Status != JustificationStatus.Approved)
                return;

            if (await IsChangeAppliedAsync(
                    request.EmployeeId,
                    request.Day,
                    request.WorkSchedule.Month,
                    request.WorkSchedule.Year,
                    request.RequestedShiftTypeId))
            {
                return;
            }

            await ApplyApprovedRequestAsync(
                request.Id,
                request.ReviewedById ?? request.SubmittedBy ?? "system",
                request.ReviewedById,
                request.ReviewNotes);
        }
    }
}
