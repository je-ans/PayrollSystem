namespace PayrollSystem.Models.Workflow
{
    public class ApprovalWorkflow
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public WorkflowProcessType ProcessType { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<ApprovalWorkflowStep> Steps { get; set; } = new List<ApprovalWorkflowStep>();
    }
}
