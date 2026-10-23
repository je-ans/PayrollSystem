using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.Services
{
    public interface IDutyExchangeRequestService
    {
        Task<(bool success, string message)> SubmitAsync(
            DutyExchangeSubmitDto dto,
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
        Task<(bool success, string message)> ApplyApprovedExchangeByIdAsync(
            Guid requestId,
            string reviewerName,
            string? reviewerId = null,
            string? reviewNotes = null);
    }

    public class DutyExchangeSubmitDto
    {
        public int? OrganizationStructureId { get; set; }
        public DateOnly DateFiled { get; set; }
        public Guid FirstEmployeeId { get; set; }
        public DateOnly FirstOriginalDate { get; set; }
        public int FirstOriginalShiftTypeId { get; set; }
        public DateOnly FirstExchangedDate { get; set; }
        public int FirstExchangedShiftTypeId { get; set; }
        public Guid SecondEmployeeId { get; set; }
        public DateOnly SecondOriginalDate { get; set; }
        public int SecondOriginalShiftTypeId { get; set; }
        public DateOnly SecondExchangedDate { get; set; }
        public int SecondExchangedShiftTypeId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class DutyExchangeRequestService : IDutyExchangeRequestService
    {
        private readonly ApplicationDbContext _db;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly IScheduleChangeRequestService _scheduleChangeService;
        private readonly IScheduleTransactionConflictService _conflictService;
        private readonly DtrAttendanceCalculationService _attendanceCalculation;

        public DutyExchangeRequestService(
            ApplicationDbContext db,
            IApprovalWorkflowService workflowService,
            IScheduleChangeRequestService scheduleChangeService,
            IScheduleTransactionConflictService conflictService,
            DtrAttendanceCalculationService attendanceCalculation)
        {
            _db = db;
            _workflowService = workflowService;
            _scheduleChangeService = scheduleChangeService;
            _conflictService = conflictService;
            _attendanceCalculation = attendanceCalculation;
        }

        public async Task<(bool success, string message)> SubmitAsync(
            DutyExchangeSubmitDto dto,
            string submittedBy,
            Guid? submitterEmployeeId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Reason))
                return (false, "Reason is required.");

            if (dto.FirstEmployeeId == dto.SecondEmployeeId)
                return (false, "You cannot exchange duty with yourself.");

            var swapError = ValidateSwap(dto);
            if (swapError != null)
                return (false, swapError);

            var scheduleError = await ValidateSchedulesMatchAsync(dto);
            if (scheduleError != null)
                return (false, scheduleError);

            var monthScheduleError = await ValidateApprovedSchedulesExistAsync(dto);
            if (monthScheduleError != null)
                return (false, monthScheduleError);

            var (hasConflict, conflictMessage) = await _conflictService.FindConflictsAsync(
                BuildConflictKeys(dto));
            if (hasConflict)
                return (false, conflictMessage);

            var request = new DutyExchangeRequest
            {
                OrganizationStructureId = dto.OrganizationStructureId,
                DateFiled = dto.DateFiled,
                FirstEmployeeId = dto.FirstEmployeeId,
                FirstOriginalDate = dto.FirstOriginalDate,
                FirstOriginalShiftTypeId = dto.FirstOriginalShiftTypeId,
                FirstExchangedDate = dto.FirstExchangedDate,
                FirstExchangedShiftTypeId = dto.FirstExchangedShiftTypeId,
                SecondEmployeeId = dto.SecondEmployeeId,
                SecondOriginalDate = dto.SecondOriginalDate,
                SecondOriginalShiftTypeId = dto.SecondOriginalShiftTypeId,
                SecondExchangedDate = dto.SecondExchangedDate,
                SecondExchangedShiftTypeId = dto.SecondExchangedShiftTypeId,
                Reason = dto.Reason.Trim(),
                Status = JustificationStatus.Pending,
                SubmittedBy = submittedBy,
                SubmittedOn = DateTime.Now
            };

            _db.DutyExchangeRequests.Add(request);
            await _db.SaveChangesAsync();

            var orgAnchorId = dto.OrganizationStructureId
                ?? await EmployeeOrganizationHelper.GetPrimaryOrganizationStructureIdAsync(_db, dto.FirstEmployeeId);

            var (created, createError, approvalRequest) = await _workflowService.CreateRequestAsync(
                WorkflowProcessType.DutyExchange,
                request.Id.ToString(),
                submitterEmployeeId ?? dto.FirstEmployeeId,
                orgAnchorId,
                submittedBy);

            if (!created || approvalRequest == null)
            {
                _db.DutyExchangeRequests.Remove(request);
                await _db.SaveChangesAsync();
                return (false, createError);
            }

            request.ApprovalRequestId = approvalRequest.Id;

            if (approvalRequest.Status == ApprovalRequestStatus.Approved)
            {
                var applyResult = await ApplyApprovedExchangeByIdAsync(request.Id, submittedBy);
                if (!applyResult.success)
                    return applyResult;
            }
            else
            {
                await _db.SaveChangesAsync();
            }

            return (true, "Exchange of duty request submitted for approval.");
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

            var request = await _db.DutyExchangeRequests
                .Include(r => r.ApprovalRequest)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

            if (request == null)
                return (false, "Request not found.");

            var workflowStatus = request.ApprovalRequest?.Status;
            if (!ApprovalRequestStatusHelper.IsPending(request.Status, workflowStatus))
            {
                if (workflowStatus == ApprovalRequestStatus.Approved)
                    return await ApplyApprovedExchangeByIdAsync(id, reviewerName, reviewerId, reviewNotes);

                return (false, "Request has already been processed.");
            }

            if (action == JustificationStatus.Approved)
            {
                if (!request.ApprovalRequestId.HasValue)
                    return (false, "No approval request found for this exchange of duty.");

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
                    var applyResult = await ApplyApprovedExchangeByIdAsync(id, reviewerName, reviewerId, reviewNotes);
                    if (!applyResult.success)
                        return applyResult;

                    return (true, "Exchange of duty approved and schedules updated.");
                }

                request.ReviewNotes = reviewNotes?.Trim();
                await _db.SaveChangesAsync();
                return (true, "Step approved. The request moved to the next approver.");
            }

            if (!request.ApprovalRequestId.HasValue)
                return (false, "No approval request found for this exchange of duty.");

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

            return (true, "Exchange of duty request rejected.");
        }

        public async Task<(bool success, string message)> ApplyApprovedExchangeByIdAsync(
            Guid requestId,
            string reviewerName,
            string? reviewerId = null,
            string? reviewNotes = null)
        {
            var request = await _db.DutyExchangeRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted);

            if (request == null)
                return (false, "Request not found.");

            var updates = await BuildScheduleUpdatesAsync(request);
            var alreadyApplied = await AreAllUpdatesAppliedAsync(updates);

            if (request.Status == JustificationStatus.Approved)
            {
                if (alreadyApplied)
                    return (true, "Exchange of duty is already applied.");

                // Approved but schedule drifted — re-apply targets only (skip original-shift check).
            }
            else if (alreadyApplied)
            {
                await MarkRequestApprovedAsync(requestId, reviewerId, reviewNotes);
                return (true, "Exchange of duty is already applied.");
            }
            else
            {
                var dto = ToSubmitDto(request);
                var scheduleError = await ValidateSchedulesMatchAsync(dto);
                if (scheduleError != null)
                    return (false, "Cannot apply exchange: " + scheduleError);

                var monthScheduleError = await ValidateApprovedSchedulesExistAsync(dto);
                if (monthScheduleError != null)
                    return (false, monthScheduleError);

                var (hasConflict, conflictMessage) = await _conflictService.FindConflictsAsync(
                    BuildConflictKeys(dto),
                    new ScheduleTransactionExclusion { DutyExchangeRequestId = request.Id });
                if (hasConflict)
                    return (false, conflictMessage);
            }

            if (request.Status == JustificationStatus.Approved)
            {
                var monthScheduleError = await ValidateApprovedSchedulesExistAsync(ToSubmitDto(request));
                if (monthScheduleError != null)
                    return (false, monthScheduleError);
            }

            // Single transaction: all schedule writes + domain Approved status, or none.
            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var applyResult = await _scheduleChangeService.ApplyEmployeeDayShiftsAsync(
                    updates.Select(u => (u.EmployeeId, u.Date, u.ShiftTypeId)).ToList(),
                    reviewerName);

                if (!applyResult.success)
                {
                    await transaction.RollbackAsync();
                    return applyResult;
                }

                await MarkRequestApprovedAsync(requestId, reviewerId, reviewNotes);
                await transaction.CommitAsync();
                return (true, "Exchange of duty approved and schedules updated.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<bool> AreAllUpdatesAppliedAsync(List<ScheduleUpdate> updates)
        {
            foreach (var update in updates)
            {
                var applied = await IsShiftAppliedAsync(
                    update.EmployeeId, update.Date, update.ShiftTypeId);
                if (!applied)
                    return false;
            }

            return updates.Count > 0;
        }

        private async Task<bool> IsShiftAppliedAsync(Guid employeeId, DateOnly date, int shiftTypeId)
        {
            var approvedShifts = await _db.ScheduleEntries
                .AsNoTracking()
                .Where(e => e.EmployeeId == employeeId && e.Day == date.Day)
                .Where(e => e.WorkSchedule.Month == date.Month && e.WorkSchedule.Year == date.Year)
                .Where(e => e.WorkSchedule.Status == WorkScheduleStatus.Approved)
                .Select(e => e.ShiftTypeId)
                .ToListAsync();

            if (approvedShifts.Count == 0)
                return false;

            return approvedShifts.All(id => id == shiftTypeId);
        }

        private async Task<List<ScheduleUpdate>> BuildScheduleUpdatesAsync(DutyExchangeRequest request)
        {
            var updates = new List<ScheduleUpdate>
            {
                new(request.FirstEmployeeId, request.FirstExchangedDate, request.FirstExchangedShiftTypeId),
                new(request.SecondEmployeeId, request.SecondExchangedDate, request.SecondExchangedShiftTypeId)
            };

            if (request.FirstOriginalDate == request.SecondOriginalDate)
                return updates;

            var dayOffShiftId = await ResolveDayOffShiftTypeIdAsync();
            if (!dayOffShiftId.HasValue)
                return updates;

            updates.Add(new ScheduleUpdate(request.FirstEmployeeId, request.FirstOriginalDate, dayOffShiftId.Value));
            updates.Add(new ScheduleUpdate(request.SecondEmployeeId, request.SecondOriginalDate, dayOffShiftId.Value));
            return updates;
        }

        private async Task<int?> ResolveDayOffShiftTypeIdAsync()
        {
            return await _db.ShiftTypes
                .AsNoTracking()
                .Where(s => s.IsActive && s.IsLeave)
                .OrderBy(s => s.Code == "OFF" ? 0 : s.Code == "DAY OFF" ? 1 : 2)
                .ThenBy(s => s.SortOrder)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();
        }

        private async Task MarkRequestApprovedAsync(Guid requestId, string? reviewerId, string? reviewNotes)
        {
            var trimmedNotes = string.IsNullOrWhiteSpace(reviewNotes) ? null : reviewNotes.Trim();

            await _db.DutyExchangeRequests
                .Where(r => r.Id == requestId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, JustificationStatus.Approved)
                    .SetProperty(r => r.ReviewedOn, DateTime.Now)
                    .SetProperty(r => r.ReviewNotes, trimmedNotes)
                    .SetProperty(r => r.ReviewedById, reviewerId));
        }

        private static IEnumerable<(Guid EmployeeId, DateOnly Date)> BuildConflictKeys(DutyExchangeSubmitDto dto) =>
            new[]
            {
                (dto.FirstEmployeeId, dto.FirstOriginalDate),
                (dto.FirstEmployeeId, dto.FirstExchangedDate),
                (dto.SecondEmployeeId, dto.SecondOriginalDate),
                (dto.SecondEmployeeId, dto.SecondExchangedDate)
            };

        private static DutyExchangeSubmitDto ToSubmitDto(DutyExchangeRequest request) => new()
        {
            OrganizationStructureId = request.OrganizationStructureId,
            DateFiled = request.DateFiled,
            FirstEmployeeId = request.FirstEmployeeId,
            FirstOriginalDate = request.FirstOriginalDate,
            FirstOriginalShiftTypeId = request.FirstOriginalShiftTypeId,
            FirstExchangedDate = request.FirstExchangedDate,
            FirstExchangedShiftTypeId = request.FirstExchangedShiftTypeId,
            SecondEmployeeId = request.SecondEmployeeId,
            SecondOriginalDate = request.SecondOriginalDate,
            SecondOriginalShiftTypeId = request.SecondOriginalShiftTypeId,
            SecondExchangedDate = request.SecondExchangedDate,
            SecondExchangedShiftTypeId = request.SecondExchangedShiftTypeId,
            Reason = request.Reason
        };

        private async Task<string?> ValidateApprovedSchedulesExistAsync(DutyExchangeSubmitDto dto)
        {
            var dates = new[]
            {
                (dto.FirstEmployeeId, dto.FirstOriginalDate),
                (dto.FirstEmployeeId, dto.FirstExchangedDate),
                (dto.SecondEmployeeId, dto.SecondOriginalDate),
                (dto.SecondEmployeeId, dto.SecondExchangedDate)
            };

            foreach (var (employeeId, date) in dates.Distinct())
            {
                var schedule = await EmployeeOrganizationHelper.ResolveEmployeeMonthWorkScheduleAsync(
                    _db, employeeId, date.Month, date.Year, approvedOnly: true);

                if (schedule == null)
                    return $"No approved work schedule exists for {date:MMMM yyyy}.";

                if (schedule.Status != WorkScheduleStatus.Approved)
                    return "Exchange of duty requires approved monthly schedules for both employees.";
            }

            return null;
        }

        private async Task<string?> ValidateSchedulesMatchAsync(DutyExchangeSubmitDto dto)
        {
            var firstOriginal = await ResolveShiftAsync(dto.FirstEmployeeId, dto.FirstOriginalDate);
            if (firstOriginal == null)
                return "No schedule found for the first employee on the original date.";

            if (firstOriginal.Id != dto.FirstOriginalShiftTypeId)
                return "The first employee's original shift no longer matches the schedule.";

            var secondOriginal = await ResolveShiftAsync(dto.SecondEmployeeId, dto.SecondOriginalDate);
            if (secondOriginal == null)
                return "No schedule found for the second employee on the original date.";

            if (secondOriginal.Id != dto.SecondOriginalShiftTypeId)
                return "The second employee's original shift no longer matches the schedule.";

            return null;
        }

        private static string? ValidateSwap(DutyExchangeSubmitDto dto)
        {
            if (dto.FirstExchangedDate != dto.SecondOriginalDate ||
                dto.FirstExchangedShiftTypeId != dto.SecondOriginalShiftTypeId)
                return "Employee 1 exchanged schedule must match Employee 2 original schedule.";

            if (dto.SecondExchangedDate != dto.FirstOriginalDate ||
                dto.SecondExchangedShiftTypeId != dto.FirstOriginalShiftTypeId)
                return "Employee 2 exchanged schedule must match Employee 1 original schedule.";

            return null;
        }

        private async Task<ShiftType?> ResolveShiftAsync(Guid employeeId, DateOnly date)
        {
            return await _attendanceCalculation.GetEmployeeShiftTypeForDayAsync(employeeId, date, approvedOnly: true)
                   ?? await _attendanceCalculation.GetEmployeeShiftTypeForDayAsync(employeeId, date, approvedOnly: false);
        }

        private sealed record ScheduleUpdate(Guid EmployeeId, DateOnly Date, int ShiftTypeId);
    }
}
