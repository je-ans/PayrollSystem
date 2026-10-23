using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.Services
{
    public interface ITimeLogJustificationService
    {
        Task<(bool success, string message)> SubmitAsync(
            DtrJustificationSubmitDto dto,
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
    }

    public class DtrJustificationSubmitDto
    {
        public Guid EmployeeId { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly? AmIn { get; set; }
        public TimeOnly? LunchOut { get; set; }
        public TimeOnly? LunchIn { get; set; }
        public TimeOnly? PmOut { get; set; }
        public TimeOnly? PmIn { get; set; }
        public TimeOnly? AmOut { get; set; }
        public DateOnly? RelatesToPreviousShiftDate { get; set; }
        public string Justification { get; set; } = string.Empty;
    }

    public class TimeLogJustificationService : ITimeLogJustificationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly TimeEntryProcessingService _processingService;
        private readonly DtrAttendanceCalculationService _attendanceCalculation;

        public TimeLogJustificationService(
            ApplicationDbContext db,
            IApprovalWorkflowService workflowService,
            TimeEntryProcessingService processingService,
            DtrAttendanceCalculationService attendanceCalculation)
        {
            _db = db;
            _workflowService = workflowService;
            _processingService = processingService;
            _attendanceCalculation = attendanceCalculation;
        }

        public async Task<(bool success, string message)> SubmitAsync(
            DtrJustificationSubmitDto dto,
            string submittedBy,
            Guid? submitterEmployeeId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Justification))
                return (false, "Justification is required.");

            var hasRequestedTime = dto.AmIn.HasValue || dto.LunchOut.HasValue || dto.LunchIn.HasValue ||
                dto.PmOut.HasValue || dto.PmIn.HasValue || dto.AmOut.HasValue;
            if (!hasRequestedTime)
                return (false, "Please provide at least one preferred time.");

            var existingRequests = await _db.TimeLogJustifications
                .Include(j => j.ApprovalRequest)
                .Where(j => j.EmployeeId == dto.EmployeeId && j.EntryDate == dto.Date)
                .ToListAsync();

            if (existingRequests.Any(j =>
                    ApprovalRequestStatusHelper.IsPending(j.Status, j.ApprovalRequest?.Status)))
                return (false, "A justification for this date is already pending approval.");

            var layout = await ResolveLayoutAsync(dto.EmployeeId, dto.Date);
            MapRequestedTimes(dto, layout, out var requestedAmIn, out var requestedLunchOut, out var requestedLunchIn, out var requestedPmOut);

            if (dto.RelatesToPreviousShiftDate.HasValue && dto.AmOut.HasValue)
            {
                requestedAmIn = dto.AmOut;
                requestedLunchOut = null;
                requestedLunchIn = null;
                requestedPmOut = null;
            }

            var request = new TimeLogJustification
            {
                EmployeeId = dto.EmployeeId,
                EntryDate = dto.Date,
                Justification = dto.Justification.Trim(),
                RequestedAmIn = requestedAmIn,
                RequestedLunchOut = requestedLunchOut,
                RequestedLunchIn = requestedLunchIn,
                RequestedPmOut = requestedPmOut,
                Status = JustificationStatus.Pending,
                SubmittedBy = submittedBy,
                SubmittedOn = DateTime.Now
            };

            _db.TimeLogJustifications.Add(request);
            await _db.SaveChangesAsync();

            var organizationAnchorId = await EmployeeOrganizationHelper
                .GetPrimaryOrganizationStructureIdAsync(_db, dto.EmployeeId);

            var (created, createError, approvalRequest) = await _workflowService.CreateRequestAsync(
                WorkflowProcessType.TimeLogJustification,
                request.Id.ToString(),
                submitterEmployeeId ?? dto.EmployeeId,
                organizationAnchorId,
                submittedBy);

            if (!created || approvalRequest == null)
            {
                _db.TimeLogJustifications.Remove(request);
                await _db.SaveChangesAsync();
                return (false, createError);
            }

            request.ApprovalRequestId = approvalRequest.Id;

            if (approvalRequest.Status == ApprovalRequestStatus.Approved)
            {
                var applyResult = await ApplyApprovedJustificationAsync(request, submittedBy);
                if (!applyResult.success)
                    return applyResult;
            }
            else
            {
                await _db.SaveChangesAsync();
            }

            return (true, "Justification submitted and is pending approval.");
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

            var request = await _db.TimeLogJustifications
                .FirstOrDefaultAsync(j => j.Id == id);

            if (request == null)
                return (false, "Justification request not found.");

            if (request.Status != JustificationStatus.Pending)
                return (false, "Only pending justifications can be reviewed.");

            if (action == JustificationStatus.Approved)
            {
                if (!request.ApprovalRequestId.HasValue)
                    return (false, "No approval request found for this justification.");

                var (approved, approveError) = await _workflowService.ApproveStepAsync(
                    request.ApprovalRequestId.Value,
                    actorEmployeeId,
                    reviewerName,
                    reviewNotes,
                    allowAdminOverride);

                if (!approved)
                    return (false, approveError);

                var approvalRequest = await _db.ApprovalRequests
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == request.ApprovalRequestId.Value);

                if (approvalRequest?.Status == ApprovalRequestStatus.Approved)
                {
                    var applyResult = await ApplyApprovedJustificationAsync(request, reviewerName, reviewerId, reviewNotes);
                    if (!applyResult.success)
                        return applyResult;

                    return (true, "Justification approved and time entries were applied.");
                }

                request.ReviewNotes = reviewNotes?.Trim();
                await _db.SaveChangesAsync();
                return (true, "Step approved. The request moved to the next approver.");
            }

            if (!request.ApprovalRequestId.HasValue)
                return (false, "No approval request found for this justification.");

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

            return (true, "Justification rejected.");
        }

        private async Task<(bool success, string message)> ApplyApprovedJustificationAsync(
            TimeLogJustification request,
            string reviewerName,
            string? reviewerId = null,
            string? reviewNotes = null)
        {
            var layout = await ResolveLayoutAsync(request.EmployeeId, request.EntryDate);
            var isCarryOverShiftEnd = await IsCarryOverShiftEndJustificationAsync(request);
            var createdBy = reviewerName;
            var newEntries = new List<TimeEntry>();

            if (isCarryOverShiftEnd && request.RequestedAmIn.HasValue)
            {
                newEntries.Add(BuildManualEntry(
                    request.EmployeeId, request.EntryDate, request.RequestedAmIn.Value,
                    EntryType.TimeOut, request.Justification, createdBy));
            }
            else
            {
                switch (layout)
                {
                    case DtrScheduleLayout.Overnight:
                        if (request.RequestedPmOut.HasValue)
                        {
                            newEntries.Add(BuildManualEntry(
                                request.EmployeeId, request.EntryDate, request.RequestedPmOut.Value,
                                EntryType.TimeIn, request.Justification, createdBy));
                        }

                        if (request.RequestedAmIn.HasValue)
                        {
                            newEntries.Add(BuildManualEntry(
                                request.EmployeeId, request.EntryDate.AddDays(1), request.RequestedAmIn.Value,
                                EntryType.TimeOut, request.Justification, createdBy));
                        }
                        break;

                    case DtrScheduleLayout.SimpleDay:
                        if (request.RequestedAmIn.HasValue)
                        {
                            newEntries.Add(BuildManualEntry(
                                request.EmployeeId, request.EntryDate, request.RequestedAmIn.Value,
                                EntryType.TimeIn, request.Justification, createdBy));
                        }

                        if (request.RequestedPmOut.HasValue)
                        {
                            newEntries.Add(BuildManualEntry(
                                request.EmployeeId, request.EntryDate, request.RequestedPmOut.Value,
                                EntryType.TimeOut, request.Justification, createdBy));
                        }
                        break;

                    default:
                        if (request.RequestedAmIn.HasValue)
                        {
                            newEntries.Add(BuildManualEntry(
                                request.EmployeeId, request.EntryDate, request.RequestedAmIn.Value,
                                EntryType.TimeIn, request.Justification, createdBy));
                        }

                        if (request.RequestedLunchOut.HasValue)
                        {
                            newEntries.Add(BuildManualEntry(
                                request.EmployeeId, request.EntryDate, request.RequestedLunchOut.Value,
                                EntryType.LunchStart, request.Justification, createdBy));
                        }

                        if (request.RequestedLunchIn.HasValue)
                        {
                            newEntries.Add(BuildManualEntry(
                                request.EmployeeId, request.EntryDate, request.RequestedLunchIn.Value,
                                EntryType.LunchEnd, request.Justification, createdBy));
                        }

                        if (request.RequestedPmOut.HasValue)
                        {
                            newEntries.Add(BuildManualEntry(
                                request.EmployeeId, request.EntryDate, request.RequestedPmOut.Value,
                                EntryType.TimeOut, request.Justification, createdBy));
                        }
                        break;
                }
            }

            if (!newEntries.Any())
                return (false, "No requested times were recorded for this justification.");

            request.Status = JustificationStatus.Approved;
            request.ReviewedById = reviewerId ?? request.ReviewedById;
            request.ReviewedOn = DateTime.Now;
            request.ReviewNotes = reviewNotes?.Trim();

            _db.TimeEntries.AddRange(newEntries);
            await _db.SaveChangesAsync();

            if (layout != DtrScheduleLayout.Overnight && !isCarryOverShiftEnd)
                await _processingService.ValidateAndPairEntries(request.EntryDate);

            return (true, "Justification approved and time entries were applied.");
        }

        private async Task<bool> IsCarryOverShiftEndJustificationAsync(TimeLogJustification request)
        {
            if (!request.RequestedAmIn.HasValue)
                return false;

            if (request.RequestedPmOut.HasValue || request.RequestedLunchOut.HasValue || request.RequestedLunchIn.HasValue)
                return false;

            var shift = await _attendanceCalculation.GetEmployeeShiftTypeForDayAsync(request.EmployeeId, request.EntryDate);
            if (shift?.IsLeave == true)
                return true;

            var prevShift = await _attendanceCalculation.GetEmployeeShiftTypeForDayAsync(
                request.EmployeeId, request.EntryDate.AddDays(-1));
            var config = await _attendanceCalculation.GetActiveConfigurationAsync();
            var prevSchedule = _attendanceCalculation.ResolveSchedule(config, prevShift);
            return DtrPunchResolutionService.GetLayout(prevSchedule) == DtrScheduleLayout.Overnight;
        }

        private async Task<DtrScheduleLayout> ResolveLayoutAsync(Guid employeeId, DateOnly date)
        {
            var shift = await _attendanceCalculation.GetEmployeeShiftTypeForDayAsync(employeeId, date);
            var config = await _attendanceCalculation.GetActiveConfigurationAsync();
            var schedule = _attendanceCalculation.ResolveSchedule(config, shift);
            return DtrPunchResolutionService.GetLayout(schedule);
        }

        private static void MapRequestedTimes(
            DtrJustificationSubmitDto dto,
            DtrScheduleLayout layout,
            out TimeOnly? requestedAmIn,
            out TimeOnly? requestedLunchOut,
            out TimeOnly? requestedLunchIn,
            out TimeOnly? requestedPmOut)
        {
            requestedAmIn = null;
            requestedLunchOut = null;
            requestedLunchIn = null;
            requestedPmOut = null;

            switch (layout)
            {
                case DtrScheduleLayout.Overnight:
                    requestedPmOut = dto.PmIn;
                    requestedAmIn = dto.AmOut;
                    break;
                case DtrScheduleLayout.SimpleDay:
                    requestedAmIn = dto.AmIn;
                    requestedPmOut = dto.PmOut;
                    break;
                default:
                    requestedAmIn = dto.AmIn;
                    requestedLunchOut = dto.LunchOut;
                    requestedLunchIn = dto.LunchIn;
                    requestedPmOut = dto.PmOut;
                    break;
            }
        }

        private static TimeEntry BuildManualEntry(
            Guid employeeId,
            DateOnly date,
            TimeOnly time,
            EntryType type,
            string justification,
            string createdBy)
        {
            return new TimeEntry
            {
                EmployeeId = employeeId,
                EntryDate = date,
                EntryTime = time,
                EntryType = type,
                Source = EntrySource.Manual,
                IsManualEntry = true,
                Status = EntryStatus.Valid,
                Notes = $"DTR Justification: {justification}",
                CreatedBy = createdBy,
                CreatedDate = DateTime.Now
            };
        }
    }
}
