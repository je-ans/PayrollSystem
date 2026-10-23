using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.Services
{
    public interface IExchangeRestDayRequestService
    {
        Task<(bool success, string message)> SubmitAsync(
            ExchangeRestDaySubmitDto dto,
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

    public class ExchangeRestDaySubmitDto
    {
        public int? OrganizationStructureId { get; set; }
        public DateOnly DateFiled { get; set; }
        public Guid FirstEmployeeId { get; set; }
        public Guid SecondEmployeeId { get; set; }
        public DateOnly FirstRestDate { get; set; }
        public DateOnly SecondRestDate { get; set; }
        public int FirstRestShiftTypeId { get; set; }
        public int SecondRestShiftTypeId { get; set; }
        public int FirstWorkShiftTypeId { get; set; }
        public int SecondWorkShiftTypeId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ExchangeRestDayRequestService : IExchangeRestDayRequestService
    {
        private readonly ApplicationDbContext _db;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly IScheduleChangeRequestService _scheduleChangeService;
        private readonly IScheduleTransactionConflictService _conflictService;
        private readonly DtrAttendanceCalculationService _attendanceCalculation;

        public ExchangeRestDayRequestService(
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
            ExchangeRestDaySubmitDto dto,
            string submittedBy,
            Guid? submitterEmployeeId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Reason))
                return (false, "Reason is required.");

            if (dto.FirstEmployeeId == dto.SecondEmployeeId)
                return (false, "You cannot exchange rest days with yourself.");

            if (dto.FirstRestDate == dto.SecondRestDate)
                return (false, "The two rest days must be different dates.");

            var firstExists = await _db.Employees.AsNoTracking()
                .AnyAsync(e => e.Id == dto.FirstEmployeeId && !e.IsDeleted && e.IsActive);
            var secondExists = await _db.Employees.AsNoTracking()
                .AnyAsync(e => e.Id == dto.SecondEmployeeId && !e.IsDeleted && e.IsActive);
            if (!firstExists || !secondExists)
                return (false, "One or both employees were not found or are inactive.");

            var patternError = await ValidateRestWorkPatternAsync(dto);
            if (patternError != null)
                return (false, patternError);

            var monthScheduleError = await ValidateApprovedSchedulesExistAsync(dto);
            if (monthScheduleError != null)
                return (false, monthScheduleError);

            var (hasConflict, conflictMessage) = await _conflictService.FindConflictsAsync(BuildConflictKeys(dto));
            if (hasConflict)
                return (false, conflictMessage);

            var request = new ExchangeRestDayRequest
            {
                OrganizationStructureId = dto.OrganizationStructureId,
                DateFiled = dto.DateFiled,
                FirstEmployeeId = dto.FirstEmployeeId,
                SecondEmployeeId = dto.SecondEmployeeId,
                FirstRestDate = dto.FirstRestDate,
                SecondRestDate = dto.SecondRestDate,
                FirstRestShiftTypeId = dto.FirstRestShiftTypeId,
                SecondRestShiftTypeId = dto.SecondRestShiftTypeId,
                FirstWorkShiftTypeId = dto.FirstWorkShiftTypeId,
                SecondWorkShiftTypeId = dto.SecondWorkShiftTypeId,
                Reason = dto.Reason.Trim(),
                Status = JustificationStatus.Pending,
                SubmittedBy = submittedBy,
                SubmittedOn = DateTime.Now
            };

            _db.ExchangeRestDayRequests.Add(request);
            await _db.SaveChangesAsync();

            var orgAnchorId = dto.OrganizationStructureId
                ?? await EmployeeOrganizationHelper.GetPrimaryOrganizationStructureIdAsync(_db, dto.FirstEmployeeId);

            var (created, createError, approvalRequest) = await _workflowService.CreateRequestAsync(
                WorkflowProcessType.ExchangeRestDay,
                request.Id.ToString(),
                submitterEmployeeId ?? dto.FirstEmployeeId,
                orgAnchorId,
                submittedBy);

            if (!created || approvalRequest == null)
            {
                _db.ExchangeRestDayRequests.Remove(request);
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

            return (true, "Exchange rest day request submitted for approval.");
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

            var request = await _db.ExchangeRestDayRequests
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
                    return (false, "No approval request found for this exchange rest day.");

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

                    return (true, "Exchange rest day approved and schedules updated.");
                }

                request.ReviewNotes = reviewNotes?.Trim();
                await _db.SaveChangesAsync();
                return (true, "Step approved. The request moved to the next approver.");
            }

            if (!request.ApprovalRequestId.HasValue)
                return (false, "No approval request found for this exchange rest day.");

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

            return (true, "Exchange rest day request rejected.");
        }

        public async Task<(bool success, string message)> ApplyApprovedExchangeByIdAsync(
            Guid requestId,
            string reviewerName,
            string? reviewerId = null,
            string? reviewNotes = null)
        {
            var request = await _db.ExchangeRestDayRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted);

            if (request == null)
                return (false, "Request not found.");

            var updates = BuildScheduleUpdates(request);
            var alreadyApplied = await AreAllUpdatesAppliedAsync(updates);

            if (request.Status == JustificationStatus.Approved)
            {
                if (alreadyApplied)
                    return (true, "Exchange rest day is already applied.");

                return (false, "Request is marked approved but the schedule does not match the intended exchange. Revalidate and correct manually if needed.");
            }

            if (alreadyApplied)
            {
                await MarkRequestApprovedAsync(requestId, reviewerId, reviewNotes);
                return (true, "Exchange rest day is already applied.");
            }

            var dto = ToSubmitDto(request);

            var firstExists = await _db.Employees.AsNoTracking()
                .AnyAsync(e => e.Id == dto.FirstEmployeeId && !e.IsDeleted);
            var secondExists = await _db.Employees.AsNoTracking()
                .AnyAsync(e => e.Id == dto.SecondEmployeeId && !e.IsDeleted);
            if (!firstExists || !secondExists)
                return (false, "One or both employees no longer exist.");

            var patternError = await ValidateRestWorkPatternAsync(dto);
            if (patternError != null)
                return (false, "Cannot apply exchange: " + patternError);

            var monthScheduleError = await ValidateApprovedSchedulesExistAsync(dto);
            if (monthScheduleError != null)
                return (false, monthScheduleError);

            var (hasConflict, conflictMessage) = await _conflictService.FindConflictsAsync(
                BuildConflictKeys(dto),
                new ScheduleTransactionExclusion { ExchangeRestDayRequestId = request.Id });
            if (hasConflict)
                return (false, conflictMessage);

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
                return (true, "Exchange rest day approved and schedules updated.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// Target state after exchange (swap each date's duty/rest between employees):
        /// Emp1 @ FirstRestDate  → Emp2's prior work (SecondWorkShiftTypeId)
        /// Emp1 @ SecondRestDate → Emp2's prior rest (SecondRestShiftTypeId)
        /// Emp2 @ FirstRestDate  → Emp1's prior rest (FirstRestShiftTypeId)
        /// Emp2 @ SecondRestDate → Emp1's prior work (FirstWorkShiftTypeId)

        private static List<ScheduleUpdate> BuildScheduleUpdates(ExchangeRestDayRequest request) =>
        [
            new(request.FirstEmployeeId, request.FirstRestDate, request.SecondWorkShiftTypeId),
            new(request.FirstEmployeeId, request.SecondRestDate, request.SecondRestShiftTypeId),
            new(request.SecondEmployeeId, request.FirstRestDate, request.FirstRestShiftTypeId),
            new(request.SecondEmployeeId, request.SecondRestDate, request.FirstWorkShiftTypeId)
        ];

        private async Task<bool> AreAllUpdatesAppliedAsync(List<ScheduleUpdate> updates)
        {
            foreach (var update in updates)
            {
                if (!await IsShiftAppliedAsync(update.EmployeeId, update.Date, update.ShiftTypeId))
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

        private async Task MarkRequestApprovedAsync(Guid requestId, string? reviewerId, string? reviewNotes)
        {
            var trimmedNotes = string.IsNullOrWhiteSpace(reviewNotes) ? null : reviewNotes.Trim();

            await _db.ExchangeRestDayRequests
                .Where(r => r.Id == requestId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, JustificationStatus.Approved)
                    .SetProperty(r => r.ReviewedOn, DateTime.Now)
                    .SetProperty(r => r.ReviewNotes, trimmedNotes)
                    .SetProperty(r => r.ReviewedById, reviewerId));
        }

        private static IEnumerable<(Guid EmployeeId, DateOnly Date)> BuildConflictKeys(ExchangeRestDaySubmitDto dto) =>
            new[]
            {
                (dto.FirstEmployeeId, dto.FirstRestDate),
                (dto.FirstEmployeeId, dto.SecondRestDate),
                (dto.SecondEmployeeId, dto.FirstRestDate),
                (dto.SecondEmployeeId, dto.SecondRestDate)
            };

        private static ExchangeRestDaySubmitDto ToSubmitDto(ExchangeRestDayRequest request) => new()
        {
            OrganizationStructureId = request.OrganizationStructureId,
            DateFiled = request.DateFiled,
            FirstEmployeeId = request.FirstEmployeeId,
            SecondEmployeeId = request.SecondEmployeeId,
            FirstRestDate = request.FirstRestDate,
            SecondRestDate = request.SecondRestDate,
            FirstRestShiftTypeId = request.FirstRestShiftTypeId,
            SecondRestShiftTypeId = request.SecondRestShiftTypeId,
            FirstWorkShiftTypeId = request.FirstWorkShiftTypeId,
            SecondWorkShiftTypeId = request.SecondWorkShiftTypeId,
            Reason = request.Reason
        };

        private async Task<string?> ValidateApprovedSchedulesExistAsync(ExchangeRestDaySubmitDto dto)
        {
            foreach (var (employeeId, date) in BuildConflictKeys(dto).Distinct())
            {
                var schedule = await EmployeeOrganizationHelper.ResolveEmployeeMonthWorkScheduleAsync(
                    _db, employeeId, date.Month, date.Year, approvedOnly: true);

                if (schedule == null)
                    return $"No approved work schedule exists for {date:MMMM yyyy}.";

                if (schedule.Status != WorkScheduleStatus.Approved)
                    return "Exchange rest day requires approved monthly schedules for both employees.";
            }

            return null;
        }

        private async Task<string?> ValidateRestWorkPatternAsync(ExchangeRestDaySubmitDto dto)
        {
            var emp1Rest = await ResolveShiftAsync(dto.FirstEmployeeId, dto.FirstRestDate);
            if (emp1Rest == null)
                return "No schedule found for the first employee on their rest date.";
            if (!emp1Rest.IsLeave)
                return "The first employee's selected rest date must be a rest/off (leave) shift.";
            if (emp1Rest.Id != dto.FirstRestShiftTypeId)
                return "The first employee's rest shift no longer matches the schedule.";

            var emp2Rest = await ResolveShiftAsync(dto.SecondEmployeeId, dto.SecondRestDate);
            if (emp2Rest == null)
                return "No schedule found for the second employee on their rest date.";
            if (!emp2Rest.IsLeave)
                return "The second employee's selected rest date must be a rest/off (leave) shift.";
            if (emp2Rest.Id != dto.SecondRestShiftTypeId)
                return "The second employee's rest shift no longer matches the schedule.";

            var emp1Work = await ResolveShiftAsync(dto.FirstEmployeeId, dto.SecondRestDate);
            if (emp1Work == null)
                return "No schedule found for the first employee on the second rest date.";
            if (emp1Work.IsLeave)
                return "The first employee must be scheduled to work on the second employee's rest date.";
            if (emp1Work.Id != dto.FirstWorkShiftTypeId)
                return "The first employee's work shift no longer matches the schedule.";

            var emp2Work = await ResolveShiftAsync(dto.SecondEmployeeId, dto.FirstRestDate);
            if (emp2Work == null)
                return "No schedule found for the second employee on the first rest date.";
            if (emp2Work.IsLeave)
                return "The second employee must be scheduled to work on the first employee's rest date.";
            if (emp2Work.Id != dto.SecondWorkShiftTypeId)
                return "The second employee's work shift no longer matches the schedule.";

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
