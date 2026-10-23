namespace PayrollSystem.Models.Workflow
{
    public class ApprovalWorkflowStep
    {
        public int Id { get; set; }

        public int WorkflowId { get; set; }
        public ApprovalWorkflow? Workflow { get; set; }

        public int StepOrder { get; set; }

        public string Name { get; set; } = string.Empty;

        public ICollection<ApprovalWorkflowStepApprover> Approvers { get; set; } = new List<ApprovalWorkflowStepApprover>();
    }
}
