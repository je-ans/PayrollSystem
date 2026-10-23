using PayrollSystem.Models;
using PayrollSystem.Models.Workflow;

namespace PayrollSystem.Utility
{
    public static class ApprovalWorkflowSeedBuilder
    {
        public static ApprovalWorkflow CreateWorkScheduleWorkflow()
        {
            return new ApprovalWorkflow
            {
                Name = "Work Schedule Workflow",
                ProcessType = WorkflowProcessType.WorkSchedule,
                IsActive = true,
                Steps =
                {
                    CreateStepWithFallback(
                        1,
                        "Local Approver",
                        CreateApprover(
                            ApproverSourceType.FunctionalRole,
                            OrganizationLevel.Unit,
                            functionalRole: OrganizationFunctionalRole.ScheduleApprover,
                            priority: 1),
                        CreateApprover(
                            ApproverSourceType.FunctionalRole,
                            OrganizationLevel.Section,
                            functionalRole: OrganizationFunctionalRole.ScheduleApprover,
                            priority: 2),
                        CreateApprover(
                            ApproverSourceType.FunctionalRole,
                            OrganizationLevel.Department,
                            functionalRole: OrganizationFunctionalRole.ScheduleApprover,
                            priority: 3),
                        CreateApprover(
                            ApproverSourceType.LeadershipRole,
                            OrganizationLevel.Unit,
                            leadershipRole: OrganizationLeadershipRole.Head,
                            priority: 4),
                        CreateApprover(
                            ApproverSourceType.LeadershipRole,
                            OrganizationLevel.Unit,
                            leadershipRole: OrganizationLeadershipRole.OIC,
                            priority: 5),
                        CreateApprover(
                            ApproverSourceType.LeadershipRole,
                            OrganizationLevel.Section,
                            leadershipRole: OrganizationLeadershipRole.Head,
                            priority: 6),
                        CreateApprover(
                            ApproverSourceType.LeadershipRole,
                            OrganizationLevel.Section,
                            leadershipRole: OrganizationLeadershipRole.OIC,
                            priority: 7),
                        CreateApprover(
                            ApproverSourceType.LeadershipRole,
                            OrganizationLevel.Department,
                            leadershipRole: OrganizationLeadershipRole.Head,
                            priority: 8),
                        CreateApprover(
                            ApproverSourceType.LeadershipRole,
                            OrganizationLevel.Department,
                            leadershipRole: OrganizationLeadershipRole.OIC,
                            priority: 9)),
                    CreateStepWithFallback(
                        2,
                        "Division Approver",
                        CreateApprover(
                            ApproverSourceType.FunctionalRole,
                            OrganizationLevel.Division,
                            functionalRole: OrganizationFunctionalRole.ScheduleApprover,
                            priority: 1),
                        CreateApprover(
                            ApproverSourceType.LeadershipRole,
                            OrganizationLevel.Division,
                            leadershipRole: OrganizationLeadershipRole.Head,
                            priority: 2),
                        CreateApprover(
                            ApproverSourceType.LeadershipRole,
                            OrganizationLevel.Division,
                            leadershipRole: OrganizationLeadershipRole.OIC,
                            priority: 3)),
                    CreateStepWithFallback(
                        3,
                        "HR Approver",
                        CreateApprover(
                            ApproverSourceType.FunctionalRole,
                            OrganizationLevel.Office,
                            functionalRole: OrganizationFunctionalRole.HRApprover,
                            priority: 1))
                }
            };
        }

        public static ApprovalWorkflow CreateScheduleChangeWorkflow()
        {
            return new ApprovalWorkflow
            {
                Name = "Schedule Change Workflow",
                ProcessType = WorkflowProcessType.ScheduleChange,
                IsActive = true,
                Steps =
                {
                    CreateStepWithFallback(
                        1,
                        "Organization Approver",
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Unit,
                            functionalRole: OrganizationFunctionalRole.ScheduleApprover, priority: 1),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Section,
                            functionalRole: OrganizationFunctionalRole.ScheduleApprover, priority: 2),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Department,
                            functionalRole: OrganizationFunctionalRole.ScheduleApprover, priority: 3),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Division,
                            functionalRole: OrganizationFunctionalRole.ScheduleApprover, priority: 4),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Office,
                            functionalRole: OrganizationFunctionalRole.ScheduleApprover, priority: 5),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Unit,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 6),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Section,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 7),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Department,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 8),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Division,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 9),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Office,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 10),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Unit,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 11),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Section,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 12),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Department,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 13),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Division,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 14),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Office,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 15)),
                    CreateStepWithFallback(
                        2,
                        "HR Approver",
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Office,
                            functionalRole: OrganizationFunctionalRole.HRApprover, priority: 1),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Institution,
                            functionalRole: OrganizationFunctionalRole.HRApprover, priority: 2))
                }
            };
        }

        public static ApprovalWorkflow CreateDefaultWorkflow()
        {
            return new ApprovalWorkflow
            {
                Name = "Default Approval Workflow",
                ProcessType = WorkflowProcessType.Default,
                IsActive = true,
                Steps =
                {
                    CreateStepWithFallback(
                        1,
                        "Organization Approver",
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Unit,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 1),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Section,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 2),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Department,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 3),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Division,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 4),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Office,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 5),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Unit,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 6),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Section,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 7),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Department,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 8),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Division,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 9),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Office,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 10)),
                    CreateStepWithFallback(
                        2,
                        "HR Approver",
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Office,
                            functionalRole: OrganizationFunctionalRole.HRApprover, priority: 1),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Institution,
                            functionalRole: OrganizationFunctionalRole.HRApprover, priority: 2))
                }
            };
        }

        public static ApprovalWorkflow CreateOvertimeWorkflow()
        {
            return new ApprovalWorkflow
            {
                Name = "Overtime Request Workflow",
                ProcessType = WorkflowProcessType.Overtime,
                IsActive = true,
                Steps =
                {
                    CreateStepWithFallback(
                        1,
                        "Organization Approver",
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Unit,
                            functionalRole: OrganizationFunctionalRole.DtrApprover, priority: 1),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Section,
                            functionalRole: OrganizationFunctionalRole.DtrApprover, priority: 2),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Department,
                            functionalRole: OrganizationFunctionalRole.DtrApprover, priority: 3),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Division,
                            functionalRole: OrganizationFunctionalRole.DtrApprover, priority: 4),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Office,
                            functionalRole: OrganizationFunctionalRole.DtrApprover, priority: 5),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Unit,
                            functionalRole: OrganizationFunctionalRole.ScheduleApprover, priority: 6),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Section,
                            functionalRole: OrganizationFunctionalRole.ScheduleApprover, priority: 7),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Department,
                            functionalRole: OrganizationFunctionalRole.ScheduleApprover, priority: 8),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Division,
                            functionalRole: OrganizationFunctionalRole.ScheduleApprover, priority: 9),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Office,
                            functionalRole: OrganizationFunctionalRole.ScheduleApprover, priority: 10),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Unit,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 11),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Section,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 12),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Department,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 13),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Division,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 14),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Office,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 15),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Unit,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 16),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Section,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 17),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Department,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 18),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Division,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 19),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Office,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 20)),
                    CreateStepWithFallback(
                        2,
                        "HR Approver",
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Office,
                            functionalRole: OrganizationFunctionalRole.HRApprover, priority: 1),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Institution,
                            functionalRole: OrganizationFunctionalRole.HRApprover, priority: 2))
                }
            };
        }

        public static ApprovalWorkflow CreateDutyExchangeWorkflow()
        {
            var template = CreateScheduleChangeWorkflow();
            return new ApprovalWorkflow
            {
                Name = "Exchange of Duty Workflow",
                ProcessType = WorkflowProcessType.DutyExchange,
                IsActive = template.IsActive,
                Steps = template.Steps.Select(step => new ApprovalWorkflowStep
                {
                    StepOrder = step.StepOrder,
                    Name = step.Name,
                    Approvers = step.Approvers.Select(a => new ApprovalWorkflowStepApprover
                    {
                        SourceType = a.SourceType,
                        OrganizationLevel = a.OrganizationLevel,
                        LeadershipRole = a.LeadershipRole,
                        FunctionalRole = a.FunctionalRole,
                        Priority = a.Priority
                    }).ToList()
                }).ToList()
            };
        }

        public static ApprovalWorkflow CreateExchangeRestDayWorkflow()
        {
            var template = CreateScheduleChangeWorkflow();
            return new ApprovalWorkflow
            {
                Name = "Exchange Rest Day Workflow",
                ProcessType = WorkflowProcessType.ExchangeRestDay,
                IsActive = template.IsActive,
                Steps = template.Steps.Select(step => new ApprovalWorkflowStep
                {
                    StepOrder = step.StepOrder,
                    Name = step.Name,
                    Approvers = step.Approvers.Select(a => new ApprovalWorkflowStepApprover
                    {
                        SourceType = a.SourceType,
                        OrganizationLevel = a.OrganizationLevel,
                        LeadershipRole = a.LeadershipRole,
                        FunctionalRole = a.FunctionalRole,
                        Priority = a.Priority
                    }).ToList()
                }).ToList()
            };
        }

        public static ApprovalWorkflow CreateUndertimeWorkflow()
        {
            var template = CreateTimeLogJustificationWorkflow();
            return new ApprovalWorkflow
            {
                Name = "Undertime Request Workflow",
                ProcessType = WorkflowProcessType.Undertime,
                IsActive = template.IsActive,
                Steps = template.Steps.Select(step => new ApprovalWorkflowStep
                {
                    StepOrder = step.StepOrder,
                    Name = step.Name,
                    Approvers = step.Approvers.Select(a => new ApprovalWorkflowStepApprover
                    {
                        SourceType = a.SourceType,
                        OrganizationLevel = a.OrganizationLevel,
                        LeadershipRole = a.LeadershipRole,
                        FunctionalRole = a.FunctionalRole,
                        Priority = a.Priority
                    }).ToList()
                }).ToList()
            };
        }

        public static ApprovalWorkflow CreateTimeLogJustificationWorkflow()
        {
            return new ApprovalWorkflow
            {
                Name = "Time Log Justification Workflow",
                ProcessType = WorkflowProcessType.TimeLogJustification,
                IsActive = true,
                Steps =
                {
                    CreateStepWithFallback(
                        1,
                        "Organization Approver",
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Unit,
                            functionalRole: OrganizationFunctionalRole.DtrApprover, priority: 1),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Section,
                            functionalRole: OrganizationFunctionalRole.DtrApprover, priority: 2),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Department,
                            functionalRole: OrganizationFunctionalRole.DtrApprover, priority: 3),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Division,
                            functionalRole: OrganizationFunctionalRole.DtrApprover, priority: 4),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Office,
                            functionalRole: OrganizationFunctionalRole.DtrApprover, priority: 5),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Unit,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 6),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Section,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 7),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Department,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 8),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Division,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 9),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Office,
                            leadershipRole: OrganizationLeadershipRole.Head, priority: 10),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Unit,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 11),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Section,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 12),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Department,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 13),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Division,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 14),
                        CreateApprover(ApproverSourceType.LeadershipRole, OrganizationLevel.Office,
                            leadershipRole: OrganizationLeadershipRole.OIC, priority: 15)),
                    CreateStepWithFallback(
                        2,
                        "HR Approver",
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Office,
                            functionalRole: OrganizationFunctionalRole.HRApprover, priority: 1),
                        CreateApprover(ApproverSourceType.FunctionalRole, OrganizationLevel.Institution,
                            functionalRole: OrganizationFunctionalRole.HRApprover, priority: 2))
                }
            };
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
                CreateApprover(sourceType, level, leadershipRole, functionalRole, priority: 1));
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

        private static ApprovalWorkflowStepApprover CreateApprover(
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
    }
}
