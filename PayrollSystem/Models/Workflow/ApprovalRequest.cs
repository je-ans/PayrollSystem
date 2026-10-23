namespace PayrollSystem.Models.Workflow
{
    public class ApprovalRequest
    {
        public int Id { get; set; }

        public WorkflowProcessType ProcessType { get; set; }

        // Identifier of the source transaction (e.g. work schedule id, DTR id)
        public string ReferenceId { get; set; } = string.Empty;

        public ApprovalRequestStatus Status { get; set; } = ApprovalRequestStatus.Pending;

        public int? CurrentStepOrder { get; set; }

        public Guid? SubmittedByEmployeeId { get; set; }
        public Employee? SubmittedByEmployee { get; set; }

        public int? OrganizationAnchorId { get; set; }
        public BuildUp.OrganizationStructure? OrganizationAnchor { get; set; }

        public int WorkflowId { get; set; }
        public ApprovalWorkflow? Workflow { get; set; }

        public string? SubmittedByUserName { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.Now;

        public DateTime? CompletedAt { get; set; }

        public ICollection<ApprovalRequestStep> Steps { get; set; } = new List<ApprovalRequestStep>();
    }
}
