using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;
using PayrollSystem.Models.Workflow;

namespace PayrollSystem.ViewModels
{
    public class UndertimeRequestListViewModel
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeIdNumber { get; set; } = string.Empty;
        public DateOnly DateFiled { get; set; }
        public DateOnly DateOfUndertime { get; set; }
        public decimal DurationHours { get; set; }
        public decimal TotalHours { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public JustificationStatus Status { get; set; }
        public int? ApprovalRequestId { get; set; }
        public ApprovalRequestStatus? ApprovalWorkflowStatus { get; set; }
        public int? CurrentStepOrder { get; set; }
        public bool CanReview { get; set; }
        public string SubmittedBy { get; set; } = string.Empty;
        public DateTime SubmittedOn { get; set; }
        public bool IsHistoricalDate => DateOfUndertime < DateFiled;
    }

    public class UndertimeRequestFormViewModel
    {
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeIdNumber { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date filed")]
        public DateOnly DateFiled { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date of undertime")]
        public DateOnly DateOfUndertime { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required]
        [Range(0.01, 24)]
        [Display(Name = "Duration (hours)")]
        public decimal? DurationHours { get; set; }

        [Required]
        [Range(0.01, 24)]
        [Display(Name = "Total no. of hours")]
        public decimal? TotalHours { get; set; }

        [Required]
        [MaxLength(2000)]
        [Display(Name = "Purpose")]
        public string Purpose { get; set; } = string.Empty;

        public bool HasExistingRequest { get; set; }
        public string? StatusMessage { get; set; }
        public decimal? SuggestedHours { get; set; }
        public int? SuggestedMinutes { get; set; }
        public bool RequiresApproval { get; set; } = true;
    }

    public class UndertimeProcessViewModel
    {
        public Guid Id { get; set; }
        public JustificationStatus ReviewAction { get; set; }

        [Display(Name = "Review notes")]
        public string ReviewNotes { get; set; } = string.Empty;

        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeIdNumber { get; set; } = string.Empty;
        public DateOnly DateFiled { get; set; }
        public DateOnly DateOfUndertime { get; set; }
        public decimal DurationHours { get; set; }
        public decimal TotalHours { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public int? ComputedUndertimeMinutesAtSubmit { get; set; }
        public bool IsHistoricalDate { get; set; }

        public int? ApprovalRequestId { get; set; }
        public JustificationStatus CurrentStatus { get; set; } = JustificationStatus.Pending;
        public string SubmittedBy { get; set; } = string.Empty;
        public DateTime SubmittedOn { get; set; }
        public DateTime? ReviewedOn { get; set; }
        public bool CanApprove { get; set; }
        public bool CanReject { get; set; }
        public ApprovalRequestTimelineViewModel? ApprovalTimeline { get; set; }
    }

    public class UndertimeRequestPrintViewModel
    {
        public string CompanyName { get; set; } = "Company";
        public string? CompanyAddress { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeIdNumber { get; set; } = string.Empty;
        public DateOnly DateFiled { get; set; }
        public string PositionName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public DateOnly DateOfUndertime { get; set; }
        public decimal DurationHours { get; set; }
        public decimal TotalHours { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public string? CompanyLogoUrl { get; set; }
        public string SubmittedBy { get; set; } = string.Empty;
        public DateTime SubmittedOn { get; set; }
        public DateTime PrintedOn { get; set; } = DateTime.Now;
        public JustificationStatus Status { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedOn { get; set; }
        public string? ReviewNotes { get; set; }
        public int? ComputedUndertimeMinutesAtSubmit { get; set; }
        public bool IsHistoricalDate { get; set; }
    }
}
