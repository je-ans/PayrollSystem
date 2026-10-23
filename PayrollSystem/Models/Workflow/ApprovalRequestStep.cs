namespace PayrollSystem.Models.Workflow
{
    public class ApprovalRequestStep
    {
        public int Id { get; set; }

        public int ApprovalRequestId { get; set; }
        public ApprovalRequest? ApprovalRequest { get; set; }

        public int StepOrder { get; set; }

        public string StepName { get; set; } = string.Empty;

        public Guid? ApproverEmployeeId { get; set; }
        public Employee? ApproverEmployee { get; set; }

        public int? ResolvedOrganizationStructureId { get; set; }
        public BuildUp.OrganizationStructure? ResolvedOrganizationStructure { get; set; }

        public string? ApproverDefinitionSummary { get; set; }

        public ApprovalStepStatus Status { get; set; } = ApprovalStepStatus.Waiting;

        public string? Remarks { get; set; }

        public DateTime? ActionDate { get; set; }

        public string? ActionByUserName { get; set; }
    }
}
