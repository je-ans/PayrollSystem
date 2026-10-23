using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.Services
{
    public interface IOvertimeRequestService
    {
        Task<(bool success, string message)> SubmitAsync(
            OvertimeSubmitDto dto,
            string submittedBy,
            string? createdById = null,
            Guid? submitterEmployeeId = null);

        Task<(bool success, string message)> ProcessAsync(
            Guid id,
            OvertimeStatus action,
            string? reviewNotes,
            string reviewerId,
            string reviewerName,
            TimeOnly? approvedStartTime = null,
            TimeOnly? approvedEndTime = null,
            bool? approvedHasLunchBreak = null,
            TimeOnly? approvedLunchStart = null,
            TimeOnly? approvedLunchEnd = null,
            Guid? actorEmployeeId = null,
            bool allowAdminOverride = false);

        // Marks an already workflow-approved request as Approved Pending OT (no time-card write).
        Task<(bool success, string message)> MarkFinallyApprovedAsync(
            Guid requestId,
            string reviewerName,
            string? reviewerId = null,
            string? reviewNotes = null,
            TimeOnly? approvedStartTime = null,
            TimeOnly? approvedEndTime = null,
            bool? approvedHasLunchBreak = null,
            TimeOnly? approvedLunchStart = null,
            TimeOnly? approvedLunchEnd = null);

        Task<(bool success, string message)> MarkProcessedToPayrollAsync(
            Guid requestId,
            int payrollPeriodId,
            string processedBy);

        // Approved OT not yet imported: employee → DayTypeId → OT / ND-OT hour split.
        // Expands multi-day requests into each day that falls inside the payroll period.
        // OT hours inside the ND window are paid as ND-OT (not double-counted as OT).
        Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<int, PendingOvertimeImportBucket>>> GetPendingImportHoursAsync(
            IEnumerable<Guid> employeeIds,
            DateOnly periodStart,
            DateOnly periodEnd);

        Task<(int marked, string message)> MarkImportedForPeriodAsync(
            int payrollPeriodId,
            IEnumerable<Guid> employeeIds,
            DateOnly periodStart,
            DateOnly periodEnd,
            string processedBy);
    }

    public class OvertimeSubmitDto
    {
        public Guid EmployeeId { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public TimeOnly RequestedStartTime { get; set; }
        public TimeOnly RequestedEndTime { get; set; }
        public bool HasLunchBreak { get; set; }
        public TimeOnly? LunchStart { get; set; }
        public TimeOnly? LunchEnd { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    //OT vs ND-OT hours for one DayType on payroll import
    public sealed class PendingOvertimeImportBucket
    {
        public decimal OvertimeHours { get; set; }
        public decimal NightDifferentialOvertimeHours { get; set; }
        public decimal TotalHours => OvertimeHours + NightDifferentialOvertimeHours;
    }

    public class OvertimeRequestService : IOvertimeRequestService
    {
        private const int MaxRequestDays = 31;

        private readonly ApplicationDbContext _db;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly IWorkingCalendarService _calendarService;
        private readonly IOvertimeEngineService _overtimeEngine;
        private readonly DtrAttendanceCalculationService _attendance;

        public OvertimeRequestService(
            ApplicationDbContext db,
            IApprovalWorkflowService workflowService,
            IWorkingCalendarService calendarService,
            IOvertimeEngineService overtimeEngine,
            DtrAttendanceCalculationService attendance)
        {
            _db = db;
            _workflowService = workflowService;
            _calendarService = calendarService;
            _overtimeEngine = overtimeEngine;
            _attendance = attendance;
        }

        public async Task<(bool success, string message)> SubmitAsync(
            OvertimeSubmitDto dto,
            string submittedBy,
            string? createdById = null,
            Guid? submitterEmployeeId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Reason))
                return (false, "Reason is required.");

            if (dto.EndDate < dto.StartDate)
                return (false, "End date must be on or after the start date.");

            var dayCount = dto.EndDate.DayNumber - dto.StartDate.DayNumber + 1;
            if (dayCount > MaxRequestDays)
                return (false, $"Overtime date range cannot exceed {MaxRequestDays} days.");

            var grossHours = OvertimeRequest.ComputeGrossHours(dto.RequestedStartTime, dto.RequestedEndTime);
            if (grossHours <= 0)
                return (false, "End time must be after start time (or continue past midnight).");

            var (lunchValid, lunchError) = OvertimeRequest.ValidateLunchBreak(
                dto.RequestedStartTime,
                dto.RequestedEndTime,
                dto.HasLunchBreak,
                dto.LunchStart,
                dto.LunchEnd);
            if (!lunchValid)
                return (false, lunchError!);

            var breakMinutes = OvertimeRequest.ResolveBreakMinutes(
                dto.HasLunchBreak, dto.LunchStart, dto.LunchEnd);
            var hoursPerDay = OvertimeRequest.ComputeHours(
                dto.RequestedStartTime,
                dto.RequestedEndTime,
                dto.HasLunchBreak,
                dto.LunchStart,
                dto.LunchEnd);
            if (hoursPerDay <= 0)
                return (false, "Net overtime hours must be greater than zero after lunch break.");

            var config = await _attendance.GetActiveConfigurationAsync();
            var policyHours = _overtimeEngine.ApplyPolicyToHours(hoursPerDay, config);
            if (policyHours <= 0)
            {
                var min = config?.MinimumOvertimeMinutes ?? 30;
                return (false,
                    $"Requested overtime ({hoursPerDay:0.##}h net per day) does not meet company minimum/rounding rules (min {min} minutes).");
            }

            var employeeExists = await _db.Employees.AnyAsync(e => e.Id == dto.EmployeeId);
            if (!employeeExists)
                return (false, "Employee not found.");

            var existingRequests = await _db.OvertimeRequests
                .Include(r => r.ApprovalRequest)
                .Where(r => r.EmployeeId == dto.EmployeeId &&
                            !r.IsDeleted &&
                            r.StartDate <= dto.EndDate &&
                            r.EndDate >= dto.StartDate)
                .ToListAsync();

            if (existingRequests.Any(r =>
                    ApprovalRequestStatusHelper.IsPending(r.Status, r.ApprovalRequest?.Status)))
                return (false, "A pending overtime request already overlaps this date range.");

            var (scheduleValid, scheduleError) = await ValidateOvertimeDoesNotOverlapDutyAsync(
                dto.EmployeeId,
                dto.StartDate,
                dto.EndDate,
                dto.RequestedStartTime,
                dto.RequestedEndTime);
            if (!scheduleValid)
                return (false, scheduleError!);

            var shift = await _attendance.GetEmployeeShiftTypeForDayAsync(dto.EmployeeId, dto.StartDate, approvedOnly: true)
                        ?? await _attendance.GetEmployeeShiftTypeForDayAsync(dto.EmployeeId, dto.StartDate, approvedOnly: false);
            var dayType = await ResolvePayrollDayTypeAsync(dto.StartDate, shift);

            var requiresApproval = config?.RequireOvertimeApproval ?? true;
            var now = DateTime.Now;
            var request = new OvertimeRequest
            {
                EmployeeId = dto.EmployeeId,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                RequestedStartTime = dto.RequestedStartTime,
                RequestedEndTime = dto.RequestedEndTime,
                HasLunchBreak = dto.HasLunchBreak,
                LunchStart = dto.HasLunchBreak ? dto.LunchStart : null,
                LunchEnd = dto.HasLunchBreak ? dto.LunchEnd : null,
                BreakMinutes = breakMinutes,
                Reason = dto.Reason.Trim(),
                Status = OvertimeStatus.Pending,
                SubmittedBy = submittedBy,
                SubmittedOn = now,
                DayTypeId = dayType?.Id,
                CreatedById = createdById,
                CreatedOn = now,
                IsProcessedToPayroll = false
            };

            _db.OvertimeRequests.Add(request);
            await _db.SaveChangesAsync();

            if (!requiresApproval)
            {
                var applyResult = await MarkFinallyApprovedAsync(
                    request.Id, submittedBy, createdById, "Auto-approved (approval not required by company policy).");
                if (!applyResult.success)
                    return applyResult;

                return (true, "Overtime recorded and queued as Pending OT for payroll import (approval not required).");
            }

            var organizationAnchorId = await EmployeeOrganizationHelper
                .GetPrimaryOrganizationStructureIdAsync(_db, dto.EmployeeId);

            var (created, createError, approvalRequest) = await _workflowService.CreateRequestAsync(
                WorkflowProcessType.Overtime,
                request.Id.ToString(),
                submitterEmployeeId ?? dto.EmployeeId,
                organizationAnchorId,
                submittedBy);

            if (!created || approvalRequest == null)
            {
                _db.OvertimeRequests.Remove(request);
                await _db.SaveChangesAsync();
                return (false, createError);
            }

            request.ApprovalRequestId = approvalRequest.Id;

            if (approvalRequest.Status == ApprovalRequestStatus.Approved)
            {
                var applyResult = await MarkFinallyApprovedAsync(request.Id, submittedBy, createdById);
                if (!applyResult.success)
                    return applyResult;
            }
            else
            {
                await _db.SaveChangesAsync();
            }

            var rangeLabel = OvertimeRequest.FormatDateRange(dto.StartDate, dto.EndDate);
            return (true, $"Overtime request for {rangeLabel} submitted and is pending approval.");
        }

        public async Task<(bool success, string message)> ProcessAsync(
            Guid id,
            OvertimeStatus action,
            string? reviewNotes,
            string reviewerId,
            string reviewerName,
            TimeOnly? approvedStartTime = null,
            TimeOnly? approvedEndTime = null,
            bool? approvedHasLunchBreak = null,
            TimeOnly? approvedLunchStart = null,
            TimeOnly? approvedLunchEnd = null,
            Guid? actorEmployeeId = null,
            bool allowAdminOverride = false)
        {
            if (action != OvertimeStatus.Approved && action != OvertimeStatus.Rejected)
                return (false, "Invalid action.");

            if (action == OvertimeStatus.Rejected && string.IsNullOrWhiteSpace(reviewNotes))
                return (false, "Review notes are required when rejecting.");

            var request = await _db.OvertimeRequests
                .Include(r => r.ApprovalRequest)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

            if (request == null)
                return (false, "Overtime request not found.");

            if (!ApprovalRequestStatusHelper.IsPending(request.Status, request.ApprovalRequest?.Status))
                return (false, "Only pending overtime requests can be reviewed.");

            if (action == OvertimeStatus.Approved)
            {
                if (!request.ApprovalRequestId.HasValue)
                    return (false, "No approval request found for this overtime request.");

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
                    var applyResult = await MarkFinallyApprovedAsync(
                        id, reviewerName, reviewerId, reviewNotes,
                        approvedStartTime, approvedEndTime,
                        approvedHasLunchBreak, approvedLunchStart, approvedLunchEnd);
                    if (!applyResult.success)
                        return applyResult;

                    return (true, "Overtime approved. It is now Pending OT for payroll import.");
                }

                request.ReviewNotes = reviewNotes?.Trim();
                request.UpdatedById = reviewerId;
                request.UpdatedOn = DateTime.Now;
                await _db.SaveChangesAsync();
                return (true, "Step approved. The request moved to the next approver.");
            }

            if (!request.ApprovalRequestId.HasValue)
                return (false, "No approval request found for this overtime request.");

            var (rejected, rejectError) = await _workflowService.RejectStepAsync(
                request.ApprovalRequestId.Value,
                actorEmployeeId,
                reviewerName,
                reviewNotes ?? string.Empty,
                allowAdminOverride);

            if (!rejected)
                return (false, rejectError);

            request.Status = OvertimeStatus.Rejected;
            request.ReviewedById = reviewerId;
            request.ReviewedOn = DateTime.Now;
            request.ReviewNotes = reviewNotes?.Trim();
            request.UpdatedById = reviewerId;
            request.UpdatedOn = DateTime.Now;
            await _db.SaveChangesAsync();

            return (true, "Overtime request rejected.");
        }

        public async Task<(bool success, string message)> MarkFinallyApprovedAsync(
            Guid requestId,
            string reviewerName,
            string? reviewerId = null,
            string? reviewNotes = null,
            TimeOnly? approvedStartTime = null,
            TimeOnly? approvedEndTime = null,
            bool? approvedHasLunchBreak = null,
            TimeOnly? approvedLunchStart = null,
            TimeOnly? approvedLunchEnd = null)
        {
            var request = await _db.OvertimeRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted);

            if (request == null)
                return (false, "Overtime request not found.");

            var start = approvedStartTime
                ?? request.ApprovedStartTime
                ?? request.RequestedStartTime;
            var end = approvedEndTime
                ?? request.ApprovedEndTime
                ?? request.RequestedEndTime;
            var hasLunch = approvedHasLunchBreak
                ?? request.ApprovedHasLunchBreak
                ?? request.HasLunchBreak;
            var lunchStart = approvedLunchStart
                ?? request.ApprovedLunchStart
                ?? request.LunchStart;
            var lunchEnd = approvedLunchEnd
                ?? request.ApprovedLunchEnd
                ?? request.LunchEnd;

            var gross = OvertimeRequest.ComputeGrossHours(start, end);
            if (gross <= 0)
                return (false, "Approved end time must be after approved start time.");

            var (lunchValid, lunchError) = OvertimeRequest.ValidateLunchBreak(
                start, end, hasLunch, lunchStart, lunchEnd);
            if (!lunchValid)
                return (false, lunchError!);

            var breakMinutes = OvertimeRequest.ResolveBreakMinutes(hasLunch, lunchStart, lunchEnd);
            if (OvertimeRequest.ComputeHours(start, end, hasLunch, lunchStart, lunchEnd) <= 0)
                return (false, "Approved net overtime hours must be greater than zero after lunch break.");

            var (scheduleValid, scheduleError) = await ValidateOvertimeDoesNotOverlapDutyAsync(
                request.EmployeeId,
                request.StartDate,
                request.EndDate,
                start,
                end);
            if (!scheduleValid)
                return (false, scheduleError!);

            // workflow side-effects may approve first; ProcessAsync may call again with null overrides.
            var alreadyFinal = request.Status == OvertimeStatus.Approved
                && request.ApprovedStartTime == start
                && request.ApprovedEndTime == end
                && request.ApprovedHasLunchBreak == hasLunch
                && request.ApprovedLunchStart == lunchStart
                && request.ApprovedLunchEnd == lunchEnd
                && request.ApprovedBreakMinutes == breakMinutes
                && !request.IsProcessedToPayroll;

            if (alreadyFinal && string.IsNullOrWhiteSpace(reviewNotes) && string.IsNullOrWhiteSpace(reviewerId))
                return (true, "Overtime request is already approved (Pending OT).");

            request.Status = OvertimeStatus.Approved;
            request.ApprovedStartTime = start;
            request.ApprovedEndTime = end;
            request.ApprovedHasLunchBreak = hasLunch;
            request.ApprovedLunchStart = hasLunch ? lunchStart : null;
            request.ApprovedLunchEnd = hasLunch ? lunchEnd : null;
            request.ApprovedBreakMinutes = breakMinutes;
            request.IsProcessedToPayroll = false;
            request.ReviewedById = reviewerId ?? request.ReviewedById;
            request.ReviewedOn = DateTime.Now;
            request.ReviewNotes = string.IsNullOrWhiteSpace(reviewNotes)
                ? request.ReviewNotes
                : reviewNotes.Trim();
            if (!string.IsNullOrWhiteSpace(reviewerId))
                request.UpdatedById = reviewerId;
            request.UpdatedOn = DateTime.Now;

            await _db.SaveChangesAsync();
            return (true, "Overtime approved and queued as Pending OT.");
        }

        public async Task<(bool success, string message)> MarkProcessedToPayrollAsync(
            Guid requestId,
            int payrollPeriodId,
            string processedBy)
        {
            var request = await _db.OvertimeRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && !r.IsDeleted);

            if (request == null)
                return (false, "Overtime request not found.");

            if (request.Status != OvertimeStatus.Approved)
                return (false, "Only approved overtime can be imported to payroll.");

            if (request.IsProcessedToPayroll)
                return (false, "This overtime request was already processed to payroll.");

            request.IsProcessedToPayroll = true;
            request.ProcessedToPayrollOn = DateTime.Now;
            request.ProcessedToPayrollBy = processedBy;
            request.PayrollPeriodId = payrollPeriodId;
            request.UpdatedOn = DateTime.Now;

            await _db.SaveChangesAsync();
            return (true, "Overtime marked as processed to payroll.");
        }

        public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<int, PendingOvertimeImportBucket>>> GetPendingImportHoursAsync(
            IEnumerable<Guid> employeeIds,
            DateOnly periodStart,
            DateOnly periodEnd)
        {
            var ids = employeeIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<Guid, IReadOnlyDictionary<int, PendingOvertimeImportBucket>>();

            var config = await _attendance.GetActiveConfigurationAsync();
            var ndStart = config?.NightDifferentialStartTime ?? TimeSpan.FromHours(22);
            var ndEnd = config?.NightDifferentialEndTime ?? TimeSpan.FromHours(6);

            // workflow already Approved but domain Status still Pending → Pending OT.
            var needsHeal = await _db.OvertimeRequests
                .Include(r => r.ApprovalRequest)
                .Where(r => !r.IsDeleted &&
                            ids.Contains(r.EmployeeId) &&
                            !r.IsProcessedToPayroll &&
                            r.StartDate <= periodEnd &&
                            r.EndDate >= periodStart &&
                            r.Status != OvertimeStatus.Approved &&
                            r.ApprovalRequest != null &&
                            r.ApprovalRequest.Status == ApprovalRequestStatus.Approved)
                .Select(r => r.Id)
                .ToListAsync();

            foreach (var id in needsHeal)
                await MarkFinallyApprovedAsync(id, "system-import-heal");

            var requests = await _db.OvertimeRequests
                .AsNoTracking()
                .Include(r => r.ApprovalRequest)
                .Where(r => !r.IsDeleted &&
                            ids.Contains(r.EmployeeId) &&
                            !r.IsProcessedToPayroll &&
                            r.StartDate <= periodEnd &&
                            r.EndDate >= periodStart)
                .ToListAsync();

            // Import when domain is Approved, or workflow completed (effective Approved).
            requests = requests
                .Where(r => ApprovalRequestStatusHelper.ResolveEffectiveStatus(
                                r.Status, r.ApprovalRequest?.Status) == OvertimeStatus.Approved)
                .ToList();

            var regularDayTypeId = await _db.DayTypes
                .AsNoTracking()
                .Where(d => d.IsActive && !d.IsDeleted &&
                            (d.Code == "REG-DAY" || d.Name == "Regular Day"))
                .Select(d => (int?)d.Id)
                .FirstOrDefaultAsync();

            var result = new Dictionary<Guid, Dictionary<int, PendingOvertimeImportBucket>>();

            foreach (var request in requests)
            {
                var start = request.ApprovedStartTime ?? request.RequestedStartTime;
                var end = request.ApprovedEndTime ?? request.RequestedEndTime;
                var rawHours = OvertimeRequest.ComputeHours(
                    start,
                    end,
                    request.EffectiveHasLunchBreak,
                    request.EffectiveLunchStart,
                    request.EffectiveLunchEnd,
                    request.EffectiveBreakMinutes);
                if (rawHours <= 0)
                    continue;

                // Same min/rounding policy used when the OT was requested.
                var hoursPerDay = _overtimeEngine.ApplyPolicyToHours(rawHours, config);
                if (hoursPerDay <= 0)
                    hoursPerDay = rawHours;

                foreach (var workDate in request.EnumerateDaysInPeriod(periodStart, periodEnd))
                {
                    var shift = await _attendance.GetEmployeeShiftTypeForDayAsync(
                                    request.EmployeeId, workDate, approvedOnly: true)
                                ?? await _attendance.GetEmployeeShiftTypeForDayAsync(
                                    request.EmployeeId, workDate, approvedOnly: false);
                    var resolved = await ResolvePayrollDayTypeAsync(workDate, shift);
                    var dayTypeId = resolved?.Id ?? request.DayTypeId ?? regularDayTypeId;
                    if (!dayTypeId.HasValue)
                        continue;

                    var otStart = workDate.ToDateTime(start);
                    var otEnd = workDate.ToDateTime(end);
                    if (otEnd <= otStart)
                        otEnd = otEnd.AddDays(1);

                    var ndOtMinutes = AttendanceComputationService.OverlapWithWindowMinutes(
                        otStart, otEnd, ndStart, ndEnd);
                    var ndOtHours = Math.Round(ndOtMinutes / 60m, 4, MidpointRounding.AwayFromZero);
                    if (ndOtHours > hoursPerDay)
                        ndOtHours = hoursPerDay;
                    var plainOtHours = Math.Round(hoursPerDay - ndOtHours, 4, MidpointRounding.AwayFromZero);

                    if (!result.TryGetValue(request.EmployeeId, out var byDayType))
                    {
                        byDayType = new Dictionary<int, PendingOvertimeImportBucket>();
                        result[request.EmployeeId] = byDayType;
                    }

                    if (!byDayType.TryGetValue(dayTypeId.Value, out var bucket))
                    {
                        bucket = new PendingOvertimeImportBucket();
                        byDayType[dayTypeId.Value] = bucket;
                    }

                    bucket.OvertimeHours += plainOtHours;
                    bucket.NightDifferentialOvertimeHours += ndOtHours;
                }
            }

            return result.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyDictionary<int, PendingOvertimeImportBucket>)kv.Value);
        }

        public async Task<(int marked, string message)> MarkImportedForPeriodAsync(
            int payrollPeriodId,
            IEnumerable<Guid> employeeIds,
            DateOnly periodStart,
            DateOnly periodEnd,
            string processedBy)
        {
            var ids = employeeIds.Distinct().ToList();
            if (ids.Count == 0)
                return (0, "No employees selected.");

            // Mark only when the full authorized range is within this period
            // (or ends in this period after earlier days were already importable).
            // Periods should be processed chronologically for multi-period ranges.
            var requests = await _db.OvertimeRequests
                .Include(r => r.ApprovalRequest)
                .Where(r => !r.IsDeleted &&
                            ids.Contains(r.EmployeeId) &&
                            !r.IsProcessedToPayroll &&
                            r.StartDate <= periodEnd &&
                            r.EndDate >= periodStart &&
                            r.EndDate <= periodEnd)
                .ToListAsync();

            requests = requests
                .Where(r => r.Status == OvertimeStatus.Approved ||
                            r.ApprovalRequest?.Status == ApprovalRequestStatus.Approved)
                .ToList();

            if (requests.Count == 0)
                return (0, "No pending approved overtime to mark as imported.");

            var now = DateTime.Now;
            foreach (var request in requests)
            {
                if (request.Status != OvertimeStatus.Approved)
                {
                    request.Status = OvertimeStatus.Approved;
                    request.ApprovedStartTime ??= request.RequestedStartTime;
                    request.ApprovedEndTime ??= request.RequestedEndTime;
                }

                request.IsProcessedToPayroll = true;
                request.ProcessedToPayrollOn = now;
                request.ProcessedToPayrollBy = processedBy;
                request.PayrollPeriodId = payrollPeriodId;
                request.UpdatedOn = now;
            }

            await _db.SaveChangesAsync();
            return (requests.Count, $"Marked {requests.Count} overtime request(s) as imported to payroll.");
        }

        // OT must fall outside fixed regular-duty windows (e.g. cannot request 8:00–5:00 when scheduled REG 8:00–5:00).
        // Day off / leave / flexible shifts are not checked.
        private async Task<(bool valid, string? error)> ValidateOvertimeDoesNotOverlapDutyAsync(
            Guid employeeId,
            DateOnly startDate,
            DateOnly endDate,
            TimeOnly otStart,
            TimeOnly otEnd)
        {
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var shift = await _attendance.GetEmployeeShiftTypeForDayAsync(employeeId, date, approvedOnly: true)
                            ?? await _attendance.GetEmployeeShiftTypeForDayAsync(employeeId, date, approvedOnly: false);

                if (!OvertimeScheduleHelper.HasFixedDutyWindow(shift))
                    continue;

                if (OvertimeScheduleHelper.GetDutyOverlapMinutes(date, otStart, otEnd, shift!) <= 0)
                    continue;

                return (false, OvertimeScheduleHelper.BuildDutyOverlapMessage(date, shift!, otStart, otEnd));
            }

            return (true, null);
        }

        // Holiday from Working Calendar; otherwise Regular Day for working shifts, Rest Day for Day Off.
        private async Task<DayType?> ResolvePayrollDayTypeAsync(DateOnly date, ShiftType? shift)
        {
            var calendarDayType = await _calendarService.GetDayTypeForDateAsync(date);
            var calendarEntry = await _calendarService.GetEntryAsync(date);
            var allDayTypes = await _db.DayTypes
                .AsNoTracking()
                .Where(d => d.IsActive && !d.IsDeleted)
                .ToListAsync();

            return DayTypeHelper.ResolveDayType(
                shift,
                calendarDayType,
                isHoliday: calendarEntry?.HolidayId != null || DayTypeHelper.IsHolidayDayType(calendarDayType),
                allDayTypes);
        }
    }
}
