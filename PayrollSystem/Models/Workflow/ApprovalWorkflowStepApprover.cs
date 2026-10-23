namespace PayrollSystem.Models.Workflow
{
    public class ApprovalWorkflowStepApprover
    {
        public int Id { get; set; }

        public int WorkflowStepId { get; set; }
        public ApprovalWorkflowStep? WorkflowStep { get; set; }

        public ApproverSourceType SourceType { get; set; }

        public OrganizationLevel OrganizationLevel { get; set; }

        public OrganizationLeadershipRole? LeadershipRole { get; set; }

        public OrganizationFunctionalRole? FunctionalRole { get; set; }

        public int Priority { get; set; }
    }
}
