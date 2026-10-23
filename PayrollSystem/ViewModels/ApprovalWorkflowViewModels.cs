using System.ComponentModel.DataAnnotations;
using System.Reflection;
using PayrollSystem.Models;
using PayrollSystem.Models.Workflow;

namespace PayrollSystem.ViewModels
{
    public class ApprovalWorkflowViewModel
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public WorkflowProcessType ProcessType { get; set; }

        public bool IsActive { get; set; } = true;

        public int StepCount { get; set; }
        public string ProcessTypeDisplay { get; set; } = string.Empty;
    }

    public class ApprovalWorkflowConfigureViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public WorkflowProcessType ProcessType { get; set; }
        public string ProcessTypeDisplay { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<ApprovalWorkflowStep> Steps { get; set; } = new();
    }

    public class ApprovalWorkflowStepViewModel
    {
        public int Id { get; set; }
        public int WorkflowId { get; set; }
        public string WorkflowName { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public int StepOrder { get; set; }

        public List<ApprovalWorkflowStepApproverViewModel> Approvers { get; set; } = new();
    }

    public class ApprovalWorkflowStepApproverViewModel
    {
        public int Id { get; set; }
        public int WorkflowStepId { get; set; }

        [Required]
        public ApproverSourceType SourceType { get; set; }

        [Required]
        public OrganizationLevel OrganizationLevel { get; set; }

        public OrganizationLeadershipRole? LeadershipRole { get; set; }
        public OrganizationFunctionalRole? FunctionalRole { get; set; }

        public int Priority { get; set; } = 1;

        public string Summary { get; set; } = string.Empty;
    }

    public class ApprovalRequestTimelineViewModel
    {
        public int RequestId { get; set; }
        public ApprovalRequestStatus Status { get; set; }
        public string StatusDisplay { get; set; } = string.Empty;
        public int? CurrentStepOrder { get; set; }
        public List<ApprovalRequestStepTimelineItem> Steps { get; set; } = new();
    }

    public class ApprovalRequestStepTimelineItem
    {
        // Original workflow step order (may have gaps when steps are skipped)
        public int StepOrder { get; set; }

        public int DisplayStepNumber { get; set; }

        public string StepName { get; set; } = string.Empty;
        public string? ApproverName { get; set; }
        public string? OrganizationName { get; set; }
        public ApprovalStepStatus Status { get; set; }
        public string StatusDisplay { get; set; } = string.Empty;
        public string? Remarks { get; set; }
        public DateTime? ActionDate { get; set; }
        public bool IsCurrent { get; set; }
    }

    public class PendingApprovalListItemViewModel
    {
        public int ApprovalRequestId { get; set; }
        public WorkflowProcessType ProcessType { get; set; }
        public string ProcessTypeDisplay { get; set; } = string.Empty;
        public string AccentClass { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string? SubjectDesignation { get; set; }
        public string? ContextDateDisplay { get; set; }
        public string? ContextDateLabel { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string SubjectInitials { get; set; } = "?";
        public string? OrganizationName { get; set; }
        public string? SubmittedByName { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string? ApproveUrl { get; set; }
        public string? RejectUrl { get; set; }
        public string? ExternalUrl { get; set; }
        public bool HasDirectActions => !string.IsNullOrEmpty(ApproveUrl) && !string.IsNullOrEmpty(RejectUrl);

        public string SearchText =>
            $"{ProcessTypeDisplay} {SubjectName} {SubjectDesignation} {ContextDateDisplay} {OrganizationName} {SubmittedByName}"
                .ToLowerInvariant();
    }

    public class PendingApprovalIndexViewModel
    {
        public List<PendingApprovalListItemViewModel> Items { get; set; } = new();
        public int TotalCount => Items.Count;
    }

    public static class WorkflowDisplayHelper
    {
        public static string GetEnumDisplayName(Enum value)
        {
            var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
            var display = member?.GetCustomAttribute<DisplayAttribute>();
            return display?.Name ?? value.ToString();
        }

        public static string BuildApproverSummary(ApprovalWorkflowStepApprover approver)
        {
            var level = GetEnumDisplayName(approver.OrganizationLevel);
            if (approver.SourceType == ApproverSourceType.LeadershipRole && approver.LeadershipRole.HasValue)
                return $"{level} {GetEnumDisplayName(approver.LeadershipRole.Value)}";
            if (approver.SourceType == ApproverSourceType.FunctionalRole && approver.FunctionalRole.HasValue)
                return $"{level} {GetEnumDisplayName(approver.FunctionalRole.Value)}";
            return level;
        }

        public static string GetProcessIcon(WorkflowProcessType processType) => processType switch
        {
            WorkflowProcessType.Default => "fa-shield-alt",
            WorkflowProcessType.WorkSchedule => "fa-calendar-alt",
            WorkflowProcessType.ScheduleChange => "fa-exchange-alt",
            WorkflowProcessType.TimeLogJustification => "fa-fingerprint",
            WorkflowProcessType.Dtr => "fa-clipboard-list",
            WorkflowProcessType.Leave => "fa-plane-departure",
            WorkflowProcessType.Overtime => "fa-business-time",
            WorkflowProcessType.DutyExchange => "fa-exchange-alt",
            WorkflowProcessType.ExchangeRestDay => "fa-calendar-day",
            WorkflowProcessType.Undertime => "fa-hourglass-half",
            WorkflowProcessType.Payroll => "fa-money-check-alt",
            _ => "fa-project-diagram"
        };

        public static string GetProcessAccentClass(WorkflowProcessType processType) => processType switch
        {
            WorkflowProcessType.Default => "aw-accent-indigo",
            WorkflowProcessType.WorkSchedule => "aw-accent-blue",
            WorkflowProcessType.ScheduleChange => "aw-accent-orange",
            WorkflowProcessType.TimeLogJustification => "aw-accent-teal",
            WorkflowProcessType.Dtr => "aw-accent-slate",
            WorkflowProcessType.Leave => "aw-accent-purple",
            WorkflowProcessType.Overtime => "aw-accent-orange",
            WorkflowProcessType.DutyExchange => "aw-accent-teal",
            WorkflowProcessType.ExchangeRestDay => "aw-accent-purple",
            WorkflowProcessType.Undertime => "aw-accent-red",
            WorkflowProcessType.Payroll => "aw-accent-green",
            _ => "aw-accent-slate"
        };

        public static string GetStepStatusIcon(ApprovalStepStatus status) => status switch
        {
            ApprovalStepStatus.Approved => "fa-check",
            ApprovalStepStatus.Rejected => "fa-times",
            ApprovalStepStatus.Pending => "fa-hourglass-half",
            ApprovalStepStatus.Skipped => "fa-forward",
            _ => "fa-circle"
        };
    }
}
