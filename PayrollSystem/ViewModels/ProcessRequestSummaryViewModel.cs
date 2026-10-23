namespace PayrollSystem.ViewModels
{
    public class ProcessRequestSummaryViewModel
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public string EmployeeIdNumber { get; set; } = string.Empty;
        public string OrganizationUnitDisplay { get; set; } = string.Empty;
        public string? ProfilePictureUrl { get; set; }
        public string Initials { get; set; } = "?";
        public DateTime? SubmittedOn { get; set; }
        public string? ContextDateDisplay { get; set; }
        public string ContextDateLabel { get; set; } = "Date";
    }
}
