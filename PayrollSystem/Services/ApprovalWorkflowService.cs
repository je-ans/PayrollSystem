using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Models.Workflow;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Services
{
    public interface IApprovalWorkflowService
    {
        Task<ApprovalWorkflow?> GetActiveWorkflowAsync(WorkflowProcessType processType);
        Task<(bool success, string error, ApprovalRequest? request)> CreateRequestAsync(
            WorkflowProcessType processType,
            string referenceId,
            Guid? submitterEmployeeId,
            int? organizationAnchorId,
            string submittedByUserName);
        Task<(bool success, string error)> ApproveStepAsync(
            int approvalRequestId,
            Guid? actorEmployeeId,
            string actorUserName,
            string? remarks = null,
            bool allowAdminOverride = false);
        Task<(bool success, string error)> RejectStepAsync(
            int approvalRequestId,
            Guid? actorEmployeeId,
            string actorUserName,
            string reason,
            bool allowAdminOverride = false);
        Task<ApprovalRequestTimelineViewModel?> GetTimelineAsync(int approvalRequestId);
        Task<bool> CanActorActOnCurrentStepAsync(int approvalRequestId, Guid? actorEmployeeId, bool allowAdminOverride = false);
        Task<bool> IsPendingApproverForWorkScheduleAsync(Guid employeeId, int month, int year, int organizationStructureId);
        Task<int> GetPendingCountForEmployeeAsync(Guid employeeId, WorkflowProcessType? processType = null);

        // All in-progress requests waiting on the current step (admin / global queue view).
        Task<int> GetPendingQueueCountAsync(WorkflowProcessType? processType = null);

        Task<PendingApprovalIndexViewModel> GetPendingForEmployeeAsync(Guid employeeId);

        // Full pending queue for admins (all current-step items, optionally by process).
        Task<PendingApprovalIndexViewModel> GetPendingQueueAsync(WorkflowProcessType? processType = null);

        Task SeedDefaultWorkflowsAsync();
    }

    public class ApprovalWorkflowService : IApprovalWorkflowService
    {
        private readonly ApplicationDbContext _db;
        private readonly IServiceProvider _serviceProvider;
        private const string HrStepName = "HR Approver";

        public ApprovalWorkflowService(ApplicationDbContext db, IServiceProvider serviceProvider)
        {
            _db = db;
            _serviceProvider = serviceProvider;
        }

        public async Task<ApprovalWorkflow?> GetActiveWorkflowAsync(WorkflowProcessType processType)
        {
            return await _db.ApprovalWorkflows
                .AsNoTracking()
                .Include(w => w.Steps.OrderBy(s => s.StepOrder))
                    .ThenInclude(s => s.Approvers.OrderBy(a => a.Priority))
                .FirstOrDefaultAsync(w => w.ProcessType == processType && w.IsActive);
        }

        private async Task<ApprovalWorkflow?> ResolveWorkflowForRequestAsync(WorkflowProcessType processType)
        {
            if (processType != WorkflowProcessType.Default)
            {
                var specific = await QueryActiveWorkflow(processType).FirstOrDefaultAsync();
                if (specific != null)
                    return specific;
            }

            return await QueryActiveWorkflow(WorkflowProcessType.Default).FirstOrDefaultAsync();
        }

        private IQueryable<ApprovalWorkflow> QueryActiveWorkflow(WorkflowProcessType processType)
        {
            return _db.ApprovalWorkflows
                .Include(w => w.Steps.OrderBy(s => s.StepOrder))
                    .ThenInclude(s => s.Approvers.OrderBy(a => a.Priority))
                .Where(w => w.ProcessType == processType && w.IsActive);
        }

        public async Task<(bool success, string error, ApprovalRequest? request)> CreateRequestAsync(
            WorkflowProcessType processType,
            string referenceId,
            Guid? submitterEmployeeId,
            int? organizationAnchorId,
            string submittedByUserName)
        {
            var existing = await _db.ApprovalRequests
                .AsNoTracking()
                .AnyAsync(r =>
                    r.ProcessType == processType &&
                    r.ReferenceId == referenceId &&
                    r.Status != ApprovalRequestStatus.Cancelled &&
                    r.Status != ApprovalRequestStatus.Rejected &&
                    r.Status != ApprovalRequestStatus.Approved);

            if (existing)
                return (false, "An active approval request already exists for this transaction.", null);

            var workflow = await ResolveWorkflowForRequestAsync(processType);

            if (workflow == null)
            {
                var processLabel = WorkflowDisplayHelper.GetEnumDisplayName(processType);
                return (false,
                    $"No active approval workflow configured for {processLabel}, and no default fallback workflow is available.",
                    null);
            }

            if (workflow.Steps.Count == 0)
                return (false, "The approval workflow has no steps configured.", null);

            var anchorId = organizationAnchorId;
            if (!anchorId.HasValue && submitterEmployeeId.HasValue)
            {
                anchorId = await EmployeeOrganizationHelper.GetPrimaryOrganizationStructureIdAsync(
                    _db, submitterEmployeeId.Value);
            }

            if (!anchorId.HasValue)
                return (false, "Unable to determine organization context for approval routing.", null);

            var orgCache = await LoadOrganizationCacheAsync();
            if (!orgCache.ContainsKey(anchorId.Value))
                return (false, "Organization anchor not found.", null);

            var requestSteps = new List<ApprovalRequestStep>();
            foreach (var step in workflow.Steps.OrderBy(s => s.StepOrder))
            {
                var resolved = ResolveStepApprover(step, anchorId.Value, orgCache, submitterEmployeeId);
                if (resolved == null)
                {
                    requestSteps.Add(new ApprovalRequestStep
                    {
                        StepOrder = step.StepOrder,
                        StepName = step.Name,
                        ApproverDefinitionSummary = "Skipped — no matching role assignment found",
                        Status = ApprovalStepStatus.Skipped,
                        Remarks = "No approver could be resolved for this step.",
                        ActionDate = DateTime.Now
                    });
                    continue;
                }

                requestSteps.Add(new ApprovalRequestStep
                {
                    StepOrder = step.StepOrder,
                    StepName = step.Name,
                    ApproverEmployeeId = resolved.EmployeeId,
                    ResolvedOrganizationStructureId = resolved.OrganizationStructureId,
                    ApproverDefinitionSummary = resolved.DefinitionSummary,
                    Status = ApprovalStepStatus.Waiting
                });
            }

            var actionableSteps = requestSteps
                .Where(s => s.Status != ApprovalStepStatus.Skipped)
                .OrderBy(s => s.StepOrder)
                .ToList();

            if (actionableSteps.Count == 0)
            {
                var processLabel = WorkflowDisplayHelper.GetEnumDisplayName(processType);
                return (false,
                    $"Unable to route {processLabel} approval — no eligible approver was found for this employee's organization. " +
                    "Assign a DTR Approver, Schedule Approver, Head, or OIC on the unit/section/department (or HR Approver for the HR step), " +
                    "and ensure the submitter is not the only assigned approver.",
                    null);
            }

            actionableSteps[0].Status = ApprovalStepStatus.Pending;
            var request = new ApprovalRequest
            {
                ProcessType = processType,
                ReferenceId = referenceId,
                SubmittedByEmployeeId = submitterEmployeeId,
                OrganizationAnchorId = anchorId,
                WorkflowId = workflow.Id,
                SubmittedByUserName = submittedByUserName,
                SubmittedAt = DateTime.Now,
                Status = ApprovalRequestStatus.InProgress,
                CurrentStepOrder = actionableSteps[0].StepOrder,
                Steps = requestSteps
            };

            _db.ApprovalRequests.Add(request);
            await _db.SaveChangesAsync();

            return (true, string.Empty, request);
        }

        public async Task<(bool success, string error)> ApproveStepAsync(
            int approvalRequestId,
            Guid? actorEmployeeId,
            string actorUserName,
            string? remarks = null,
            bool allowAdminOverride = false)
        {
            var request = await LoadRequestForUpdateAsync(approvalRequestId);
            if (request == null)
                return (false, "Approval request not found.");

            if (request.Status != ApprovalRequestStatus.InProgress)
                return (false, "This approval request is no longer in progress.");

            var currentStep = GetCurrentStep(request);
            if (currentStep == null)
                return (false, "No pending approval step found.");

            if (!await CanActorActOnStepAsync(currentStep, actorEmployeeId, allowAdminOverride))
                return (false, "You are not authorized to approve this step.");

            currentStep.Status = ApprovalStepStatus.Approved;
            currentStep.Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
            currentStep.ActionDate = DateTime.Now;
            currentStep.ActionByUserName = actorUserName;

            var orderedSteps = request.Steps.OrderBy(s => s.StepOrder).ToList();
            var currentIndex = orderedSteps.FindIndex(s => s.Id == currentStep.Id);
            var nextStep = FindNextActionableStep(orderedSteps, currentIndex);

            if (nextStep != null)
            {
                nextStep.Status = ApprovalStepStatus.Pending;
                request.CurrentStepOrder = nextStep.StepOrder;
            }
            else
            {
                request.Status = ApprovalRequestStatus.Approved;
                request.CurrentStepOrder = null;
                request.CompletedAt = DateTime.Now;
            }

            await _db.SaveChangesAsync();
            await ApplyDomainSideEffectsAsync(request);
            return (true, string.Empty);
        }

        private async Task ApplyDomainSideEffectsAsync(ApprovalRequest request)
        {
            if (request.Status != ApprovalRequestStatus.Approved)
                return;

            var actorName = request.Steps
                .Where(s => s.Status == ApprovalStepStatus.Approved && !string.IsNullOrWhiteSpace(s.ActionByUserName))
                .OrderByDescending(s => s.StepOrder)
                .Select(s => s.ActionByUserName)
                .FirstOrDefault()
                ?? request.SubmittedByUserName
                ?? "system";

            if (request.ProcessType == WorkflowProcessType.ScheduleChange &&
                Guid.TryParse(request.ReferenceId, out var changeRequestId))
            {
                var scheduleChangeService = _serviceProvider.GetRequiredService<IScheduleChangeRequestService>();
                await scheduleChangeService.ApplyApprovedChangeByIdAsync(
                    changeRequestId,
                    actorName);
            }

            // Overtime: ensure Pending OT is recorded when the workflow completes.
            // ProcessAsync may call MarkFinallyApprovedAsync again with reviewer-adjusted times.
            if (request.ProcessType == WorkflowProcessType.Overtime &&
                Guid.TryParse(request.ReferenceId, out var overtimeId))
            {
                var overtimeService = _serviceProvider.GetRequiredService<IOvertimeRequestService>();
                await overtimeService.MarkFinallyApprovedAsync(overtimeId, actorName);
            }

            if (request.ProcessType == WorkflowProcessType.DutyExchange &&
                Guid.TryParse(request.ReferenceId, out var dutyExchangeId))
            {
                var dutyExchangeService = _serviceProvider.GetRequiredService<IDutyExchangeRequestService>();
                await dutyExchangeService.ApplyApprovedExchangeByIdAsync(dutyExchangeId, actorName);
            }

            if (request.ProcessType == WorkflowProcessType.ExchangeRestDay &&
                Guid.TryParse(request.ReferenceId, out var exchangeRestDayId))
            {
                var exchangeRestDayService = _serviceProvider.GetRequiredService<IExchangeRestDayRequestService>();
                await exchangeRestDayService.ApplyApprovedExchangeByIdAsync(exchangeRestDayId, actorName);
            }

            if (request.ProcessType == WorkflowProcessType.Undertime &&
                Guid.TryParse(request.ReferenceId, out var undertimeId))
            {
                var undertimeService = _serviceProvider.GetRequiredService<IUndertimeRequestService>();
                await undertimeService.MarkFinallyApprovedAsync(undertimeId, actorName);
            }
        }

        public async Task<(bool success, string error)> RejectStepAsync(
            int approvalRequestId,
            Guid? actorEmployeeId,
            string actorUserName,
            string reason,
            bool allowAdminOverride = false)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return (false, "Rejection reason is required.");

            var request = await LoadRequestForUpdateAsync(approvalRequestId);
            if (request == null)
                return (false, "Approval request not found.");

            if (request.Status != ApprovalRequestStatus.InProgress)
                return (false, "This approval request is no longer in progress.");

            var currentStep = GetCurrentStep(request);
            if (currentStep == null)
                return (false, "No pending approval step found.");

            if (!await CanActorActOnStepAsync(currentStep, actorEmployeeId, allowAdminOverride))
                return (false, "You are not authorized to reject this step.");

            currentStep.Status = ApprovalStepStatus.Rejected;
            currentStep.Remarks = reason.Trim();
            currentStep.ActionDate = DateTime.Now;
            currentStep.ActionByUserName = actorUserName;

            request.Status = ApprovalRequestStatus.Rejected;
            request.CurrentStepOrder = null;
            request.CompletedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            await ApplyDomainRejectionSideEffectsAsync(request);
            return (true, string.Empty);
        }

        private async Task ApplyDomainRejectionSideEffectsAsync(ApprovalRequest request)
        {
            if (request.Status != ApprovalRequestStatus.Rejected)
                return;

            if (request.ProcessType == WorkflowProcessType.DutyExchange &&
                Guid.TryParse(request.ReferenceId, out var dutyExchangeId))
            {
                var dutyExchange = await _db.DutyExchangeRequests
                    .FirstOrDefaultAsync(r => r.Id == dutyExchangeId && !r.IsDeleted);
                if (dutyExchange != null && dutyExchange.Status != JustificationStatus.Rejected)
                {
                    var dutyRejectRemarks = request.Steps
                        .Where(s => s.Status == ApprovalStepStatus.Rejected)
                        .OrderByDescending(s => s.StepOrder)
                        .Select(s => s.Remarks)
                        .FirstOrDefault();

                    dutyExchange.Status = JustificationStatus.Rejected;
                    dutyExchange.ReviewedOn = DateTime.Now;
                    dutyExchange.ReviewNotes = string.IsNullOrWhiteSpace(dutyRejectRemarks)
                        ? dutyExchange.ReviewNotes
                        : dutyRejectRemarks.Trim();
                    dutyExchange.UpdatedOn = DateTime.Now;

                    await _db.SaveChangesAsync();
                }

                return;
            }

            if (request.ProcessType == WorkflowProcessType.ExchangeRestDay &&
                Guid.TryParse(request.ReferenceId, out var exchangeRestDayId))
            {
                var restDay = await _db.ExchangeRestDayRequests
                    .FirstOrDefaultAsync(r => r.Id == exchangeRestDayId && !r.IsDeleted);
                if (restDay != null && restDay.Status != JustificationStatus.Rejected)
                {
                    var restRejectRemarks = request.Steps
                        .Where(s => s.Status == ApprovalStepStatus.Rejected)
                        .OrderByDescending(s => s.StepOrder)
                        .Select(s => s.Remarks)
                        .FirstOrDefault();

                    restDay.Status = JustificationStatus.Rejected;
                    restDay.ReviewedOn = DateTime.Now;
                    restDay.ReviewNotes = string.IsNullOrWhiteSpace(restRejectRemarks)
                        ? restDay.ReviewNotes
                        : restRejectRemarks.Trim();
                    restDay.UpdatedOn = DateTime.Now;

                    await _db.SaveChangesAsync();
                }

                return;
            }

            if (request.ProcessType == WorkflowProcessType.Undertime &&
                Guid.TryParse(request.ReferenceId, out var undertimeId))
            {
                var undertime = await _db.UndertimeRequests
                    .FirstOrDefaultAsync(r => r.Id == undertimeId && !r.IsDeleted);
                if (undertime != null && undertime.Status != JustificationStatus.Rejected)
                {
                    var undertimeRejectRemarks = request.Steps
                        .Where(s => s.Status == ApprovalStepStatus.Rejected)
                        .OrderByDescending(s => s.StepOrder)
                        .Select(s => s.Remarks)
                        .FirstOrDefault();

                    undertime.Status = JustificationStatus.Rejected;
                    undertime.ReviewedOn = DateTime.Now;
                    undertime.ReviewNotes = string.IsNullOrWhiteSpace(undertimeRejectRemarks)
                        ? undertime.ReviewNotes
                        : undertimeRejectRemarks.Trim();
                    undertime.UpdatedOn = DateTime.Now;

                    await _db.SaveChangesAsync();
                }

                return;
            }

            if (request.ProcessType != WorkflowProcessType.Overtime ||
                !Guid.TryParse(request.ReferenceId, out var overtimeId))
                return;

            var overtime = await _db.OvertimeRequests
                .FirstOrDefaultAsync(r => r.Id == overtimeId && !r.IsDeleted);
            if (overtime == null || overtime.Status == OvertimeStatus.Rejected)
                return;

            var rejectRemarks = request.Steps
                .Where(s => s.Status == ApprovalStepStatus.Rejected)
                .OrderByDescending(s => s.StepOrder)
                .Select(s => s.Remarks)
                .FirstOrDefault();

            overtime.Status = OvertimeStatus.Rejected;
            overtime.ReviewedOn = DateTime.Now;
            overtime.ReviewNotes = string.IsNullOrWhiteSpace(rejectRemarks)
                ? overtime.ReviewNotes
                : rejectRemarks.Trim();
            overtime.UpdatedOn = DateTime.Now;

            await _db.SaveChangesAsync();
        }

        public async Task<ApprovalRequestTimelineViewModel?> GetTimelineAsync(int approvalRequestId)
        {
            var request = await _db.ApprovalRequests
                .AsNoTracking()
                .Include(r => r.Steps.OrderBy(s => s.StepOrder))
                    .ThenInclude(s => s.ApproverEmployee)
                .Include(r => r.Steps)
                    .ThenInclude(s => s.ResolvedOrganizationStructure)
                .FirstOrDefaultAsync(r => r.Id == approvalRequestId);

            if (request == null)
                return null;

            return new ApprovalRequestTimelineViewModel
            {
                RequestId = request.Id,
                Status = request.Status,
                StatusDisplay = WorkflowDisplayHelper.GetEnumDisplayName(request.Status),
                CurrentStepOrder = request.CurrentStepOrder,
                Steps = MapVisibleTimelineSteps(request.Steps, request.CurrentStepOrder)
            };
        }

        public async Task<bool> CanActorActOnCurrentStepAsync(
            int approvalRequestId,
            Guid? actorEmployeeId,
            bool allowAdminOverride = false)
        {
            var request = await _db.ApprovalRequests
                .AsNoTracking()
                .Include(r => r.Steps)
                .FirstOrDefaultAsync(r => r.Id == approvalRequestId);

            if (request == null || request.Status != ApprovalRequestStatus.InProgress)
                return false;

            var currentStep = request.Steps
                .FirstOrDefault(s => s.StepOrder == request.CurrentStepOrder && s.Status == ApprovalStepStatus.Pending);

            if (currentStep == null)
                return false;

            return await CanActorActOnStepAsync(currentStep, actorEmployeeId, allowAdminOverride);
        }

        public async Task<bool> IsPendingApproverForWorkScheduleAsync(
            Guid employeeId,
            int month,
            int year,
            int organizationStructureId)
        {
            var schedule = await _db.WorkSchedules
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Month == month &&
                    s.Year == year &&
                    s.OrganizationStructureId == organizationStructureId);

            if (schedule?.ApprovalRequestId == null)
                return false;

            return await CanActorActOnCurrentStepAsync(schedule.ApprovalRequestId.Value, employeeId);
        }

        public async Task<int> GetPendingCountForEmployeeAsync(Guid employeeId, WorkflowProcessType? processType = null)
        {
            var query = FilterActionablePendingSteps(_db.ApprovalRequestSteps.AsNoTracking(), employeeId);
            if (processType.HasValue)
                query = query.Where(s => s.ApprovalRequest!.ProcessType == processType.Value);
            return await query.CountAsync();
        }

        public async Task<int> GetPendingQueueCountAsync(WorkflowProcessType? processType = null)
        {
            var query = FilterPendingCurrentSteps(_db.ApprovalRequestSteps.AsNoTracking());
            if (processType.HasValue)
                query = query.Where(s => s.ApprovalRequest!.ProcessType == processType.Value);
            return await query.CountAsync();
        }

        public async Task<PendingApprovalIndexViewModel> GetPendingForEmployeeAsync(Guid employeeId)
        {
            var query = FilterActionablePendingSteps(
                _db.ApprovalRequestSteps.AsNoTracking(), employeeId);
            return await BuildPendingIndexAsync(query);
        }

        public async Task<PendingApprovalIndexViewModel> GetPendingQueueAsync(WorkflowProcessType? processType = null)
        {
            var query = FilterPendingCurrentSteps(_db.ApprovalRequestSteps.AsNoTracking());
            if (processType.HasValue)
                query = query.Where(s => s.ApprovalRequest!.ProcessType == processType.Value);
            return await BuildPendingIndexAsync(query);
        }

        private static IQueryable<ApprovalRequestStep> FilterPendingCurrentSteps(
            IQueryable<ApprovalRequestStep> query)
        {
            return query.Where(s =>
                s.Status == ApprovalStepStatus.Pending &&
                s.ApprovalRequest!.Status == ApprovalRequestStatus.InProgress &&
                s.ApprovalRequest.CurrentStepOrder == s.StepOrder);
        }

        private async Task<PendingApprovalIndexViewModel> BuildPendingIndexAsync(
            IQueryable<ApprovalRequestStep> query)
        {
            var steps = await query
                .Include(s => s.ApprovalRequest)
                    .ThenInclude(r => r!.SubmittedByEmployee)
                .Include(s => s.ApprovalRequest)
                    .ThenInclude(r => r!.OrganizationAnchor)
                .OrderBy(s => s.ApprovalRequest!.SubmittedAt)
                .ThenBy(s => s.StepOrder)
                .ToListAsync();

            var items = new List<PendingApprovalListItemViewModel>();
            var subjectByRequestId = await BuildPendingSubjectsAsync(
                steps.Select(s => s.ApprovalRequest!).DistinctBy(r => r.Id).ToList());

            foreach (var step in steps)
            {
                var request = step.ApprovalRequest!;
                var actionUrls = BuildActionUrls(request);
                var externalUrl = actionUrls.ExternalUrl ?? await BuildWorkScheduleExternalUrlAsync(request);
                subjectByRequestId.TryGetValue(request.Id, out var subject);

                items.Add(new PendingApprovalListItemViewModel
                {
                    ApprovalRequestId = request.Id,
                    ProcessType = request.ProcessType,
                    ProcessTypeDisplay = WorkflowDisplayHelper.GetEnumDisplayName(request.ProcessType),
                    AccentClass = WorkflowDisplayHelper.GetProcessAccentClass(request.ProcessType),
                    SubjectName = subject?.SubjectName ?? WorkflowDisplayHelper.GetEnumDisplayName(request.ProcessType),
                    SubjectDesignation = subject?.SubjectDesignation,
                    ContextDateDisplay = subject?.ContextDateDisplay,
                    ContextDateLabel = subject?.ContextDateLabel,
                    ProfilePictureUrl = subject?.ProfilePictureUrl,
                    SubjectInitials = subject?.SubjectInitials ?? "?",
                    OrganizationName = request.OrganizationAnchor?.Name,
                    SubmittedByName = request.SubmittedByEmployee != null
                        ? EmployeeDisplayHelper.FormatEmployeeName(
                            request.SubmittedByEmployee.FirstName,
                            request.SubmittedByEmployee.Surname)
                        : request.SubmittedByUserName,
                    SubmittedAt = request.SubmittedAt,
                    ApproveUrl = actionUrls.ApproveUrl,
                    RejectUrl = actionUrls.RejectUrl,
                    ExternalUrl = externalUrl
                });
            }

            return new PendingApprovalIndexViewModel { Items = items };
        }

        public async Task SeedDefaultWorkflowsAsync()
        {
            if (await _db.ApprovalWorkflows.AnyAsync())
                return;

            var workSchedule = ApprovalWorkflowSeedBuilder.CreateWorkScheduleWorkflow();
            var scheduleChange = ApprovalWorkflowSeedBuilder.CreateScheduleChangeWorkflow();
            var timeLogJustification = ApprovalWorkflowSeedBuilder.CreateTimeLogJustificationWorkflow();
            var overtime = ApprovalWorkflowSeedBuilder.CreateOvertimeWorkflow();
            var dutyExchange = ApprovalWorkflowSeedBuilder.CreateDutyExchangeWorkflow();
            var exchangeRestDay = ApprovalWorkflowSeedBuilder.CreateExchangeRestDayWorkflow();
            var undertime = ApprovalWorkflowSeedBuilder.CreateUndertimeWorkflow();
            var defaultWorkflow = ApprovalWorkflowSeedBuilder.CreateDefaultWorkflow();

            var payroll = new ApprovalWorkflow
            {
                Name = "Payroll Workflow",
                ProcessType = WorkflowProcessType.Payroll,
                IsActive = true,
                Steps =
                {
                    CreateStep(1, "Department Head", ApproverSourceType.LeadershipRole,
                        OrganizationLevel.Department, leadershipRole: OrganizationLeadershipRole.Head),
                    CreateStep(2, "HR Approver", ApproverSourceType.FunctionalRole,
                        OrganizationLevel.Institution, functionalRole: OrganizationFunctionalRole.HRApprover),
                    CreateStep(3, "Payroll Approver", ApproverSourceType.FunctionalRole,
                        OrganizationLevel.Institution, functionalRole: OrganizationFunctionalRole.PayrollApprover)
                }
            };

            _db.ApprovalWorkflows.AddRange(
                workSchedule, scheduleChange, timeLogJustification, overtime, dutyExchange, exchangeRestDay, undertime, defaultWorkflow, payroll);
            await _db.SaveChangesAsync();
        }

        private static ApprovalWorkflowStep CreateStep(
            int order,
            string name,
            ApproverSourceType sourceType,
            OrganizationLevel level,
            OrganizationLeadershipRole? leadershipRole = null,
            OrganizationFunctionalRole? functionalRole = null)
        {
            return CreateStepWithFallback(
                order,
                name,
                CreateStepApprover(sourceType, level, leadershipRole, functionalRole, priority: 1));
        }

        private static ApprovalWorkflowStep CreateStepWithFallback(
            int order,
            string name,
            params ApprovalWorkflowStepApprover[] approvers)
        {
            return new ApprovalWorkflowStep
            {
                StepOrder = order,
                Name = name,
                Approvers = approvers.ToList()
            };
        }

        private static ApprovalWorkflowStepApprover CreateStepApprover(
            ApproverSourceType sourceType,
            OrganizationLevel level,
            OrganizationLeadershipRole? leadershipRole = null,
            OrganizationFunctionalRole? functionalRole = null,
            int priority = 1)
        {
            return new ApprovalWorkflowStepApprover
            {
                SourceType = sourceType,
                OrganizationLevel = level,
                LeadershipRole = leadershipRole,
                FunctionalRole = functionalRole,
                Priority = priority
            };
        }

        private static ApprovalRequestStep? FindNextActionableStep(
            IReadOnlyList<ApprovalRequestStep> orderedSteps,
            int currentIndex)
        {
            for (var i = currentIndex + 1; i < orderedSteps.Count; i++)
            {
                if (orderedSteps[i].Status == ApprovalStepStatus.Skipped)
                    continue;

                if (orderedSteps[i].Status == ApprovalStepStatus.Waiting)
                    return orderedSteps[i];
            }

            return null;
        }

        private async Task<ApprovalRequest?> LoadRequestForUpdateAsync(int approvalRequestId)
        {
            return await _db.ApprovalRequests
                .AsTracking()
                .Include(r => r.Steps)
                .FirstOrDefaultAsync(r => r.Id == approvalRequestId);
        }

        private static ApprovalRequestStep? GetCurrentStep(ApprovalRequest request)
        {
            return request.Steps
                .FirstOrDefault(s =>
                    s.StepOrder == request.CurrentStepOrder &&
                    s.Status == ApprovalStepStatus.Pending);
        }

        private IQueryable<ApprovalRequestStep> FilterActionablePendingSteps(
            IQueryable<ApprovalRequestStep> query,
            Guid employeeId)
        {
            return query.Where(s =>
                s.Status == ApprovalStepStatus.Pending &&
                s.ApprovalRequest!.Status == ApprovalRequestStatus.InProgress &&
                s.ApprovalRequest.CurrentStepOrder == s.StepOrder &&
                (
                    s.ApproverEmployeeId == employeeId ||
                    (s.StepName == HrStepName &&
                     _db.EmployeeRoleAssignments.Any(a =>
                        a.EmployeeId == employeeId &&
                        a.EffectiveTo == null &&
                        a.FunctionalRole == OrganizationFunctionalRole.HRApprover))
                ));
        }

        private static List<ApprovalRequestStepTimelineItem> MapVisibleTimelineSteps(
            IEnumerable<ApprovalRequestStep> steps,
            int? currentStepOrder)
        {
            return steps
                .Where(s => s.Status != ApprovalStepStatus.Skipped)
                .OrderBy(s => s.StepOrder)
                .Select((s, index) => new ApprovalRequestStepTimelineItem
                {
                    StepOrder = s.StepOrder,
                    DisplayStepNumber = index + 1,
                    StepName = s.StepName,
                    ApproverName = IsHrStep(s.StepName)
                        ? "Any HR Approver"
                        : s.ApproverEmployee != null
                            ? $"{s.ApproverEmployee.FirstName} {s.ApproverEmployee.Surname}"
                            : null,
                    OrganizationName = s.ResolvedOrganizationStructure?.Name,
                    Status = s.Status,
                    StatusDisplay = WorkflowDisplayHelper.GetEnumDisplayName(s.Status),
                    Remarks = s.Remarks,
                    ActionDate = s.ActionDate,
                    IsCurrent = currentStepOrder == s.StepOrder &&
                                s.Status == ApprovalStepStatus.Pending
                })
                .ToList();
        }

        private static int GetDisplayStepNumber(
            IEnumerable<ApprovalRequestStep> steps,
            int stepOrder)
        {
            var visible = steps
                .Where(s => s.Status != ApprovalStepStatus.Skipped)
                .OrderBy(s => s.StepOrder)
                .Select(s => s.StepOrder)
                .ToList();

            var index = visible.IndexOf(stepOrder);
            return index >= 0 ? index + 1 : stepOrder;
        }

        private async Task<bool> CanActorActOnStepAsync(
            ApprovalRequestStep step,
            Guid? actorEmployeeId,
            bool allowAdminOverride)
        {
            if (allowAdminOverride)
                return true;

            if (!actorEmployeeId.HasValue)
                return false;

            if (IsHrStep(step.StepName))
            {
                return await _db.EmployeeRoleAssignments
                    .AsNoTracking()
                    .AnyAsync(a =>
                        a.EmployeeId == actorEmployeeId.Value &&
                        a.EffectiveTo == null &&
                        a.FunctionalRole == OrganizationFunctionalRole.HRApprover);
            }

            if (!step.ApproverEmployeeId.HasValue)
                return false;

            return step.ApproverEmployeeId.Value == actorEmployeeId.Value;
        }

        private sealed record ResolvedApprover(
            Guid EmployeeId,
            int OrganizationStructureId,
            string DefinitionSummary);

        private ResolvedApprover? ResolveStepApprover(
            ApprovalWorkflowStep step,
            int anchorOrgId,
            Dictionary<int, OrganizationNode> orgCache,
            Guid? submitterEmployeeId)
        {
            if (!orgCache.ContainsKey(anchorOrgId))
                return null;

            foreach (var approverDef in step.Approvers.OrderBy(a => a.Priority))
            {
                // HR Approver is not derived from the schedule's org branch.
                // Resolve HR Approver globally by functional role.
                if (approverDef.SourceType == ApproverSourceType.FunctionalRole &&
                    approverDef.FunctionalRole == OrganizationFunctionalRole.HRApprover)
                {
                    var query = _db.EmployeeRoleAssignments
                        .AsNoTracking()
                        .Include(a => a.OrganizationStructure)
                        .Where(a =>
                            a.FunctionalRole == OrganizationFunctionalRole.HRApprover &&
                            a.EffectiveTo == null);

                    if (submitterEmployeeId.HasValue)
                        query = query.Where(a => a.EmployeeId != submitterEmployeeId.Value);

                    var assignment = query
                        .Select(a => new
                        {
                            a.EmployeeId,
                            a.OrganizationStructureId,
                            a.EffectiveFrom,
                            OrgCode = a.OrganizationStructure != null ? a.OrganizationStructure.Code : null,
                            OrgName = a.OrganizationStructure != null ? a.OrganizationStructure.Name : null
                        })
                        .OrderByDescending(a =>
                            a.OrgCode == "HR" ||
                            (a.OrgName != null && a.OrgName.Contains("Human Resources")))
                        .ThenBy(a => a.EffectiveFrom)
                        .FirstOrDefault();

                    if (assignment == null)
                        return null;

                    return new ResolvedApprover(
                        assignment.EmployeeId,
                        assignment.OrganizationStructureId,
                        WorkflowDisplayHelper.BuildApproverSummary(approverDef));
                }

                // Resolve by priority order, walking up the org tree via FindOrganizationAtLevel.
                // Do not require Step 1 to match only the employee's exact org level — that blocked
                // Unit→Section→Department fallbacks and left OT/justification with "no approver".
                var orgAtLevel = FindOrganizationAtLevel(anchorOrgId, approverDef.OrganizationLevel, orgCache);
                if (orgAtLevel == null)
                    continue;

                var resolved = ResolveApproverAtExactLevel(
                    orgAtLevel.Id,
                    approverDef,
                    submitterEmployeeId);
                if (resolved != null)
                    return resolved;
            }

            return null;
        }

        private ResolvedApprover? ResolveApproverAtExactLevel(
            int organizationStructureId,
            ApprovalWorkflowStepApprover approverDef,
            Guid? submitterEmployeeId)
        {
            var employeeId = FindRoleHolder(organizationStructureId, approverDef, submitterEmployeeId);
            if (!employeeId.HasValue)
                return null;

            return new ResolvedApprover(
                employeeId.Value,
                organizationStructureId,
                WorkflowDisplayHelper.BuildApproverSummary(approverDef));
        }

        private static OrganizationNode? FindOrganizationAtLevel(
            int startOrgId,
            OrganizationLevel targetLevel,
            Dictionary<int, OrganizationNode> orgCache)
        {
            var currentId = startOrgId;
            var visited = new HashSet<int>();

            while (currentId > 0 && visited.Add(currentId))
            {
                if (!orgCache.TryGetValue(currentId, out var node))
                    return null;

                if (node.Level == targetLevel)
                    return node;

                if (!node.ParentId.HasValue)
                    return null;

                currentId = node.ParentId.Value;
            }

            return null;
        }

        private Guid? FindRoleHolder(
            int organizationStructureId,
            ApprovalWorkflowStepApprover approverDef,
            Guid? excludeEmployeeId = null)
        {
            var query = _db.EmployeeRoleAssignments
                .AsNoTracking()
                .Where(a => a.OrganizationStructureId == organizationStructureId && a.EffectiveTo == null);

            if (excludeEmployeeId.HasValue)
                query = query.Where(a => a.EmployeeId != excludeEmployeeId.Value);

            if (approverDef.SourceType == ApproverSourceType.LeadershipRole && approverDef.LeadershipRole.HasValue)
            {
                query = query.Where(a => a.LeadershipRole == approverDef.LeadershipRole);
            }
            else if (approverDef.SourceType == ApproverSourceType.FunctionalRole && approverDef.FunctionalRole.HasValue)
            {
                query = query.Where(a => a.FunctionalRole == approverDef.FunctionalRole);
            }
            else
            {
                return null;
            }

            return query
                .OrderBy(a => a.EffectiveFrom)
                .Select(a => (Guid?)a.EmployeeId)
                .FirstOrDefault();
        }

        private async Task<Dictionary<int, OrganizationNode>> LoadOrganizationCacheAsync()
        {
            var nodes = await _db.OrganizationStructures
                .AsNoTracking()
                .Select(o => new OrganizationNode(o.Id, o.ParentId, o.Level))
                .ToListAsync();

            return nodes.ToDictionary(n => n.Id);
        }

        private sealed record OrganizationNode(int Id, int? ParentId, OrganizationLevel Level);

        private static bool IsHrStep(string? stepName) =>
            string.Equals(stepName, HrStepName, StringComparison.OrdinalIgnoreCase);

        private static (string? ApproveUrl, string? RejectUrl, string? ExternalUrl) BuildActionUrls(ApprovalRequest request)
        {
            var approved = (int)JustificationStatus.Approved;
            var rejected = (int)JustificationStatus.Rejected;
            var otApproved = (int)OvertimeStatus.Approved;
            var otRejected = (int)OvertimeStatus.Rejected;

            switch (request.ProcessType)
            {
                case WorkflowProcessType.ScheduleChange when Guid.TryParse(request.ReferenceId, out var changeRequestId):
                    return (
                        $"/ScheduleChangeRequest/Process?id={changeRequestId}&status={approved}",
                        $"/ScheduleChangeRequest/Process?id={changeRequestId}&status={rejected}",
                        null);

                case WorkflowProcessType.TimeLogJustification when Guid.TryParse(request.ReferenceId, out var justificationId):
                    return (
                        $"/DtrJustification/Process?id={justificationId}&status={approved}",
                        $"/DtrJustification/Process?id={justificationId}&status={rejected}",
                        null);

                case WorkflowProcessType.Overtime when Guid.TryParse(request.ReferenceId, out var overtimeId):
                    return (
                        $"/OvertimeRequest/Process?id={overtimeId}&status={otApproved}",
                        $"/OvertimeRequest/Process?id={overtimeId}&status={otRejected}",
                        null);

                case WorkflowProcessType.DutyExchange when Guid.TryParse(request.ReferenceId, out var dutyExchangeId):
                    return (
                        $"/DutyExchangeRequest/Process?id={dutyExchangeId}&status={approved}",
                        $"/DutyExchangeRequest/Process?id={dutyExchangeId}&status={rejected}",
                        null);

                case WorkflowProcessType.ExchangeRestDay when Guid.TryParse(request.ReferenceId, out var exchangeRestDayId):
                    return (
                        $"/ExchangeRestDayRequest/Process?id={exchangeRestDayId}&status={approved}",
                        $"/ExchangeRestDayRequest/Process?id={exchangeRestDayId}&status={rejected}",
                        null);

                case WorkflowProcessType.Undertime when Guid.TryParse(request.ReferenceId, out var undertimeId):
                    return (
                        $"/UndertimeRequest/Process?id={undertimeId}&status={approved}",
                        $"/UndertimeRequest/Process?id={undertimeId}&status={rejected}",
                        null);

                case WorkflowProcessType.WorkSchedule when int.TryParse(request.ReferenceId, out _):
                    return (null, null, null);
            }

            return (null, null, null);
        }

        private async Task<string?> BuildWorkScheduleExternalUrlAsync(ApprovalRequest request)
        {
            if (request.ProcessType != WorkflowProcessType.WorkSchedule ||
                !int.TryParse(request.ReferenceId, out var scheduleId))
                return null;

            var schedule = await _db.WorkSchedules
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == scheduleId);

            if (schedule == null)
                return null;

            return $"/MySchedule/Detail?month={schedule.Month}&year={schedule.Year}&organizationStructureId={schedule.OrganizationStructureId}";
        }

        private sealed class PendingSubjectSnapshot
        {
            public string SubjectName { get; set; } = string.Empty;
            public string? SubjectDesignation { get; set; }
            public string? ContextDateDisplay { get; set; }
            public string? ContextDateLabel { get; set; }
            public string? ProfilePictureUrl { get; set; }
            public string SubjectInitials { get; set; } = "?";
        }

        private async Task<Dictionary<int, PendingSubjectSnapshot>> BuildPendingSubjectsAsync(
            List<ApprovalRequest> requests)
        {
            var result = new Dictionary<int, PendingSubjectSnapshot>();
            if (requests.Count == 0)
                return result;

            var scheduleChangeIds = requests
                .Where(r => r.ProcessType == WorkflowProcessType.ScheduleChange)
                .Select(r => r.ReferenceId)
                .Where(id => Guid.TryParse(id, out _))
                .Select(Guid.Parse)
                .ToList();

            var justificationIds = requests
                .Where(r => r.ProcessType == WorkflowProcessType.TimeLogJustification)
                .Select(r => r.ReferenceId)
                .Where(id => Guid.TryParse(id, out _))
                .Select(Guid.Parse)
                .ToList();

            var overtimeIds = requests
                .Where(r => r.ProcessType == WorkflowProcessType.Overtime)
                .Select(r => r.ReferenceId)
                .Where(id => Guid.TryParse(id, out _))
                .Select(Guid.Parse)
                .ToList();

            var dutyExchangeIds = requests
                .Where(r => r.ProcessType == WorkflowProcessType.DutyExchange)
                .Select(r => r.ReferenceId)
                .Where(id => Guid.TryParse(id, out _))
                .Select(Guid.Parse)
                .ToList();

            var exchangeRestDayIds = requests
                .Where(r => r.ProcessType == WorkflowProcessType.ExchangeRestDay)
                .Select(r => r.ReferenceId)
                .Where(id => Guid.TryParse(id, out _))
                .Select(Guid.Parse)
                .ToList();

            var undertimeIds = requests
                .Where(r => r.ProcessType == WorkflowProcessType.Undertime)
                .Select(r => r.ReferenceId)
                .Where(id => Guid.TryParse(id, out _))
                .Select(Guid.Parse)
                .ToList();

            var scheduleIds = requests
                .Where(r => r.ProcessType == WorkflowProcessType.WorkSchedule)
                .Select(r => r.ReferenceId)
                .Where(id => int.TryParse(id, out _))
                .Select(int.Parse)
                .ToList();

            var scheduleChanges = scheduleChangeIds.Count == 0
                ? new List<ScheduleChangeRequest>()
                : await _db.ScheduleChangeRequests
                    .AsNoTracking()
                    .Include(r => r.Employee)
                        .ThenInclude(e => e.ApplicationUser)
                    .Include(r => r.WorkSchedule)
                    .Where(r => scheduleChangeIds.Contains(r.Id))
                    .ToListAsync();

            var justifications = justificationIds.Count == 0
                ? new List<TimeLogJustification>()
                : await _db.TimeLogJustifications
                    .AsNoTracking()
                    .Include(j => j.Employee)
                        .ThenInclude(e => e.ApplicationUser)
                    .Where(j => justificationIds.Contains(j.Id))
                    .ToListAsync();

            var overtimeRequests = overtimeIds.Count == 0
                ? new List<OvertimeRequest>()
                : await _db.OvertimeRequests
                    .AsNoTracking()
                    .Include(r => r.Employee)
                        .ThenInclude(e => e.ApplicationUser)
                    .Where(r => overtimeIds.Contains(r.Id))
                    .ToListAsync();

            var dutyExchangeRequests = dutyExchangeIds.Count == 0
                ? new List<DutyExchangeRequest>()
                : await _db.DutyExchangeRequests
                    .AsNoTracking()
                    .Include(r => r.FirstEmployee)
                        .ThenInclude(e => e.ApplicationUser)
                    .Include(r => r.SecondEmployee)
                    .Where(r => dutyExchangeIds.Contains(r.Id))
                    .ToListAsync();

            var exchangeRestDayRequests = exchangeRestDayIds.Count == 0
                ? new List<ExchangeRestDayRequest>()
                : await _db.ExchangeRestDayRequests
                    .AsNoTracking()
                    .Include(r => r.FirstEmployee)
                        .ThenInclude(e => e.ApplicationUser)
                    .Include(r => r.SecondEmployee)
                    .Where(r => exchangeRestDayIds.Contains(r.Id))
                    .ToListAsync();

            var undertimeRequests = undertimeIds.Count == 0
                ? new List<UndertimeRequest>()
                : await _db.UndertimeRequests
                    .AsNoTracking()
                    .Include(r => r.Employee)
                        .ThenInclude(e => e.ApplicationUser)
                    .Where(r => undertimeIds.Contains(r.Id))
                    .ToListAsync();

            var schedules = scheduleIds.Count == 0
                ? new List<WorkSchedule>()
                : await _db.WorkSchedules
                    .AsNoTracking()
                    .Include(s => s.OrganizationStructure)
                    .Where(s => scheduleIds.Contains(s.Id))
                    .ToListAsync();

            var employeeIds = scheduleChanges.Select(r => r.EmployeeId)
                .Concat(justifications.Select(j => j.EmployeeId))
                .Concat(overtimeRequests.Select(r => r.EmployeeId))
                .Concat(dutyExchangeRequests.Select(r => r.FirstEmployeeId))
                .Concat(exchangeRestDayRequests.Select(r => r.FirstEmployeeId))
                .Distinct()
                .ToList();

            var designations = await EmployeeOrganizationHelper.GetActiveDesignationNamesAsync(_db, employeeIds);

            foreach (var request in requests)
            {
                switch (request.ProcessType)
                {
                    case WorkflowProcessType.ScheduleChange when Guid.TryParse(request.ReferenceId, out var changeId):
                        {
                            var item = scheduleChanges.FirstOrDefault(r => r.Id == changeId);
                            if (item?.Employee == null)
                                break;

                            designations.TryGetValue(item.EmployeeId, out var designation);
                            result[request.Id] = new PendingSubjectSnapshot
                            {
                                SubjectName = EmployeeDisplayHelper.FormatEmployeeName(
                                    item.Employee.FirstName, item.Employee.Surname),
                                SubjectDesignation = designation,
                                ContextDateDisplay = new DateOnly(
                                    item.WorkSchedule.Year, item.WorkSchedule.Month, item.Day)
                                    .ToString("dddd, MMMM dd, yyyy"),
                                ContextDateLabel = "Schedule date",
                                ProfilePictureUrl = EmployeeDisplayHelper.GetProfilePictureUrl(
                                    item.Employee.ApplicationUser?.ProfilePicture),
                                SubjectInitials = EmployeeDisplayHelper.GetInitials(
                                    item.Employee.FirstName, item.Employee.Surname)
                            };
                            break;
                        }
                    case WorkflowProcessType.TimeLogJustification when Guid.TryParse(request.ReferenceId, out var justificationId):
                        {
                            var item = justifications.FirstOrDefault(j => j.Id == justificationId);
                            if (item?.Employee == null)
                                break;

                            designations.TryGetValue(item.EmployeeId, out var designation);
                            result[request.Id] = new PendingSubjectSnapshot
                            {
                                SubjectName = EmployeeDisplayHelper.FormatEmployeeName(
                                    item.Employee.FirstName, item.Employee.Surname),
                                SubjectDesignation = designation,
                                ContextDateDisplay = item.EntryDate.ToString("dddd, MMMM dd, yyyy"),
                                ContextDateLabel = "Date covered",
                                ProfilePictureUrl = EmployeeDisplayHelper.GetProfilePictureUrl(
                                    item.Employee.ApplicationUser?.ProfilePicture),
                                SubjectInitials = EmployeeDisplayHelper.GetInitials(
                                    item.Employee.FirstName, item.Employee.Surname)
                            };
                            break;
                        }
                    case WorkflowProcessType.Overtime when Guid.TryParse(request.ReferenceId, out var overtimeId):
                        {
                            var item = overtimeRequests.FirstOrDefault(r => r.Id == overtimeId);
                            if (item?.Employee == null)
                                break;

                            designations.TryGetValue(item.EmployeeId, out var designation);
                            result[request.Id] = new PendingSubjectSnapshot
                            {
                                SubjectName = EmployeeDisplayHelper.FormatEmployeeName(
                                    item.Employee.FirstName, item.Employee.Surname),
                                SubjectDesignation = designation,
                                ContextDateDisplay = OvertimeRequest.FormatDateRange(item.StartDate, item.EndDate),
                                ContextDateLabel = "OT dates",
                                ProfilePictureUrl = EmployeeDisplayHelper.GetProfilePictureUrl(
                                    item.Employee.ApplicationUser?.ProfilePicture),
                                SubjectInitials = EmployeeDisplayHelper.GetInitials(
                                    item.Employee.FirstName, item.Employee.Surname)
                            };
                            break;
                        }
                    case WorkflowProcessType.DutyExchange when Guid.TryParse(request.ReferenceId, out var dutyExchangeId):
                        {
                            var item = dutyExchangeRequests.FirstOrDefault(r => r.Id == dutyExchangeId);
                            if (item?.FirstEmployee == null || item.SecondEmployee == null)
                                break;

                            designations.TryGetValue(item.FirstEmployeeId, out var designation);
                            result[request.Id] = new PendingSubjectSnapshot
                            {
                                SubjectName = $"{EmployeeDisplayHelper.FormatEmployeeName(item.FirstEmployee.FirstName, item.FirstEmployee.Surname)} ↔ {EmployeeDisplayHelper.FormatEmployeeName(item.SecondEmployee.FirstName, item.SecondEmployee.Surname)}",
                                SubjectDesignation = designation,
                                ContextDateDisplay = $"{item.FirstOriginalDate:MMM dd, yyyy} / {item.SecondOriginalDate:MMM dd, yyyy}",
                                ContextDateLabel = "Duty dates",
                                ProfilePictureUrl = EmployeeDisplayHelper.GetProfilePictureUrl(
                                    item.FirstEmployee.ApplicationUser?.ProfilePicture),
                                SubjectInitials = EmployeeDisplayHelper.GetInitials(
                                    item.FirstEmployee.FirstName, item.FirstEmployee.Surname)
                            };
                            break;
                        }
                    case WorkflowProcessType.ExchangeRestDay when Guid.TryParse(request.ReferenceId, out var exchangeRestDayId):
                        {
                            var item = exchangeRestDayRequests.FirstOrDefault(r => r.Id == exchangeRestDayId);
                            if (item?.FirstEmployee == null || item.SecondEmployee == null)
                                break;

                            designations.TryGetValue(item.FirstEmployeeId, out var designation);
                            result[request.Id] = new PendingSubjectSnapshot
                            {
                                SubjectName = $"{EmployeeDisplayHelper.FormatEmployeeName(item.FirstEmployee.FirstName, item.FirstEmployee.Surname)} ↔ {EmployeeDisplayHelper.FormatEmployeeName(item.SecondEmployee.FirstName, item.SecondEmployee.Surname)}",
                                SubjectDesignation = designation,
                                ContextDateDisplay = $"{item.FirstRestDate:MMM dd, yyyy} / {item.SecondRestDate:MMM dd, yyyy}",
                                ContextDateLabel = "Rest days",
                                ProfilePictureUrl = EmployeeDisplayHelper.GetProfilePictureUrl(
                                    item.FirstEmployee.ApplicationUser?.ProfilePicture),
                                SubjectInitials = EmployeeDisplayHelper.GetInitials(
                                    item.FirstEmployee.FirstName, item.FirstEmployee.Surname)
                            };
                            break;
                        }
                    case WorkflowProcessType.Undertime when Guid.TryParse(request.ReferenceId, out var undertimeId):
                        {
                            var item = undertimeRequests.FirstOrDefault(r => r.Id == undertimeId);
                            if (item?.Employee == null)
                                break;

                            designations.TryGetValue(item.EmployeeId, out var designation);
                            result[request.Id] = new PendingSubjectSnapshot
                            {
                                SubjectName = EmployeeDisplayHelper.FormatEmployeeName(
                                    item.Employee.FirstName, item.Employee.Surname),
                                SubjectDesignation = designation,
                                ContextDateDisplay = item.DateOfUndertime.ToString("MMM dd, yyyy"),
                                ContextDateLabel = "Undertime date",
                                ProfilePictureUrl = EmployeeDisplayHelper.GetProfilePictureUrl(
                                    item.Employee.ApplicationUser?.ProfilePicture),
                                SubjectInitials = EmployeeDisplayHelper.GetInitials(
                                    item.Employee.FirstName, item.Employee.Surname)
                            };
                            break;
                        }
                    case WorkflowProcessType.WorkSchedule when int.TryParse(request.ReferenceId, out var scheduleId):
                        {
                            var schedule = schedules.FirstOrDefault(s => s.Id == scheduleId);
                            if (schedule == null)
                                break;

                            var monthName = new DateTime(schedule.Year, schedule.Month, 1).ToString("MMMM yyyy");
                            var orgName = schedule.OrganizationStructure?.Name
                                ?? request.OrganizationAnchor?.Name
                                ?? "Organization";

                            result[request.Id] = new PendingSubjectSnapshot
                            {
                                SubjectName = $"{monthName} Work Schedule",
                                SubjectDesignation = orgName,
                                ProfilePictureUrl = null,
                                SubjectInitials = EmployeeDisplayHelper.GetInitialsFromLabel(orgName)
                            };
                            break;
                        }
                }
            }

            return result;
        }
    }
}
