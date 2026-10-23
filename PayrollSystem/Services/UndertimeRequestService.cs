using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.Services
{
    public interface IUndertimeRequestService
    {
        Task<(bool success, string message)> SubmitAsync(
            UndertimeSubmitDto dto,
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

        Task<(bool success, string message)> MarkFinallyApprovedAsync(
            Guid requestId,
            string reviewerName,
            string? reviewerId = null,
            string? reviewNotes = null);
    }

    public class UndertimeSubmitDto
    {
        public Guid EmployeeId { get; set; }
        public DateOnly DateFiled { get; set; }
        public DateOnly DateOfUndertime { get; set; }
        public decimal DurationHours { get; set; }
        public decimal TotalHours { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public int? ComputedUndertimeMinutesAtSubmit { get; set; }
    }

    public class UndertimeRequestService : IUndertimeRequestService
    {
        private readonly ApplicationDbContext _db;
        private readonly IApprovalWorkflowService _workflowService;

        public UndertimeRequestService(
            ApplicationDbContext db,
            IApprovalWorkflowService workflowService)
        {
            _db = db;
            _workflowService = workflowService;
        }

        public async Task<(bool success, string message)> SubmitAsync(
            UndertimeSubmitDto dto,
            string submittedBy,
            Guid? submitterEmployeeId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Purpose))
                return (false, "Purpose is required.");

            if (dto.TotalHours < dto.DurationHours)
                return (false, "Total no. of hours cannot be less than duration.");

            var employeeExists = await _db.Employees.AsNoTracking()
                .AnyAsync(e => e.Id == dto.EmployeeId && !e.IsDeleted && e.IsActive);
            if (!employeeExists)
                return (false, "Employee not found or inactive.");

            var duplicateError = await ValidateNoBlockingDuplicateAsync(dto.EmployeeId, dto.DateOfUndertime);
            if (duplicateError != null)
                return (false, duplicateError);

            var request = new UndertimeRequest
            {
                EmployeeId = dto.EmployeeId,
                DateFiled = dto.DateFiled,
                DateOfUndertime = dto.DateOfUndertime,
                DurationHours = dto.DurationHours,
                TotalHours = dto.TotalHours,
                Purpose = dto.Purpose.Trim(),
                ComputedUndertimeMinutesAtSubmit = dto.ComputedUndertimeMinutesAtSubmit,
                Status = JustificationStatus.Pending,
                SubmittedBy = submittedBy,
                SubmittedOn = DateTime.Now
            };

            _db.UndertimeRequests.Add(request);
            await _db.SaveChangesAsync();

            var orgAnchorId = await EmployeeOrganizationHelper.GetPrimaryOrganizationStructureIdAsync(
                _db, dto.EmployeeId);

            var (created, createError, approvalRequest) = await _workflowService.CreateRequestAsync(
                WorkflowProcessType.Undertime,
                request.Id.ToString(),
                submitterEmployeeId ?? dto.EmployeeId,
                orgAnchorId,
                submittedBy);

            if (!created || approvalRequest == null)
            {
                _db.UndertimeRequests.Remove(request);
                await _db.SaveChangesAsync();
                return (false, createError);
            }

            request.ApprovalRequestId = approvalRequest.Id;

            if (approvalRequest.Status == ApprovalRequestStatus.Approved)
            {
                var applyResult = await MarkFinallyApprovedAsync(request.Id, submittedBy);
                if (!applyResult.success)
                    return applyResult;
            }
            else
            {
                await _db.SaveChangesAsync();
            }

            return (true, "Undertime request submitted for approval.");
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

            var request = await _db.UndertimeRequests
                .Include(r => r.ApprovalRequest)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

            if (request == null)
                return (false, "Request not found.");

            var workflowStatus = request.ApprovalRequest?.Status;
            if (!ApprovalRequestStatusHelper.IsPending(request.Status, workflowStatus))
            {
                if (workflowStatus == ApprovalRequestStatus.Approved)
                    return await MarkFinallyApprovedAsync(id, reviewerName, reviewerId, reviewNotes);

                return (false, "Request has already been processed.");
            }

            if (action == JustificationStatus.Approved)
            {
                if (!request.ApprovalRequestId.HasValue)
                    return (false, "No approval request found for this undertime request.");

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
                    var applyResult = await MarkFinallyApprovedAsync(id, reviewerName, reviewerId, reviewNotes);
                    if (!applyResult.success)
                        return applyResult;

                    return (true, "Undertime request approved.");
                }

                request.ReviewNotes = reviewNotes?.Trim();
                await _db.SaveChangesAsync();
                return (true, "Step approved. The request moved to the next approver.");
            }

            if (!request.ApprovalRequestId.HasValue)
                return (false, "No approval request found for this undertime request.");

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

            return (true, "Undertime request rejected.");
        }

        public async Task<(bool success, string message)> MarkFinallyApprovedAsync(
            Guid requestId,
            string reviewerName,
            string? reviewerId = null,
            string? reviewNotes = null)
        {
            var request = await _db.UndertimeRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted);

            if (request == null)
                return (false, "Request not found.");

            if (request.Status == JustificationStatus.Approved)
                return (true, "Undertime request is already approved.");

            if (request.Status == JustificationStatus.Rejected)
                return (false, "Cannot approve a rejected undertime request.");

            var trimmedNotes = string.IsNullOrWhiteSpace(reviewNotes) ? null : reviewNotes.Trim();

            await _db.UndertimeRequests
                .Where(r => r.Id == requestId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, JustificationStatus.Approved)
                    .SetProperty(r => r.ReviewedOn, DateTime.Now)
                    .SetProperty(r => r.ReviewNotes, trimmedNotes)
                    .SetProperty(r => r.ReviewedById, reviewerId));

            return (true, "Undertime request approved.");
        }

        private async Task<string?> ValidateNoBlockingDuplicateAsync(Guid employeeId, DateOnly dateOfUndertime)
        {
            var existing = await _db.UndertimeRequests
                .AsNoTracking()
                .Include(r => r.ApprovalRequest)
                .Where(r => r.EmployeeId == employeeId &&
                            r.DateOfUndertime == dateOfUndertime &&
                            !r.IsDeleted)
                .ToListAsync();

            if (existing.Any(r =>
                    ApprovalRequestStatusHelper.IsPending(r.Status, r.ApprovalRequest?.Status)))
                return "An undertime request for this date is already pending approval.";

            if (existing.Any(r =>
                    r.Status == JustificationStatus.Approved ||
                    r.ApprovalRequest?.Status == ApprovalRequestStatus.Approved))
                return "An approved undertime request already exists for this date.";

            return null;
        }
    }
}
