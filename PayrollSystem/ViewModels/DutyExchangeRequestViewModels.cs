using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;
using PayrollSystem.Models.Workflow;

namespace PayrollSystem.ViewModels
{
    public class DutyExchangeRequestListViewModel
    {
        public Guid Id { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public DateOnly DateFiled { get; set; }
        public Guid FirstEmployeeId { get; set; }
        public string FirstEmployeeName { get; set; } = string.Empty;
        public Guid SecondEmployeeId { get; set; }
        public string SecondEmployeeName { get; set; } = string.Empty;
        public DateOnly FirstOriginalDate { get; set; }
        public DateOnly SecondOriginalDate { get; set; }
        public string FirstOriginalShiftCode { get; set; } = string.Empty;
        public string SecondOriginalShiftCode { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public JustificationStatus Status { get; set; }
        public int? ApprovalRequestId { get; set; }
        public ApprovalRequestStatus? ApprovalWorkflowStatus { get; set; }
        public int? CurrentStepOrder { get; set; }
        public bool CanReview { get; set; }
        public string SubmittedBy { get; set; } = string.Empty;
        public DateTime SubmittedOn { get; set; }
    }

    public class DutyExchangeEmployeeOptionViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EmployeeIdNumber { get; set; } = string.Empty;
    }

    public class DutyExchangeRequestFormViewModel
    {
        public Guid FirstEmployeeId { get; set; }
        public string FirstEmployeeName { get; set; } = string.Empty;
        public string FirstEmployeeIdNumber { get; set; } = string.Empty;

        public int? OrganizationStructureId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date filed")]
        public DateOnly DateFiled { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required(ErrorMessage = "Please select the second employee.")]
        [Display(Name = "Second employee")]
        public Guid? SecondEmployeeId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Original date")]
        public DateOnly FirstOriginalDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        public int? FirstOriginalShiftTypeId { get; set; }
        public string FirstOriginalShiftDisplay { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Exchanged date")]
        public DateOnly FirstExchangedDate { get; set; }

        public int? FirstExchangedShiftTypeId { get; set; }
        public string FirstExchangedShiftDisplay { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Original date")]
        public DateOnly SecondOriginalDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        public int? SecondOriginalShiftTypeId { get; set; }
        public string SecondOriginalShiftDisplay { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Exchanged date")]
        public DateOnly SecondExchangedDate { get; set; }

        public int? SecondExchangedShiftTypeId { get; set; }
        public string SecondExchangedShiftDisplay { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        [Display(Name = "Reason for exchange")]
        public string Reason { get; set; } = string.Empty;

        public List<DutyExchangeEmployeeOptionViewModel> ColleagueOptions { get; set; } = new();
        public bool RequiresApproval { get; set; } = true;
    }

    public class DutyExchangeProcessViewModel
    {
        public Guid Id { get; set; }
        public JustificationStatus ReviewAction { get; set; }

        [Display(Name = "Review notes")]
        public string ReviewNotes { get; set; } = string.Empty;

        public string DepartmentName { get; set; } = string.Empty;
        public DateOnly DateFiled { get; set; }
        public string Reason { get; set; } = string.Empty;

        public string FirstEmployeeName { get; set; } = string.Empty;
        public string FirstEmployeeIdNumber { get; set; } = string.Empty;
        public DateOnly FirstOriginalDate { get; set; }
        public string FirstOriginalShiftDisplay { get; set; } = string.Empty;
        public DateOnly FirstExchangedDate { get; set; }
        public string FirstExchangedShiftDisplay { get; set; } = string.Empty;

        public string SecondEmployeeName { get; set; } = string.Empty;
        public string SecondEmployeeIdNumber { get; set; } = string.Empty;
        public DateOnly SecondOriginalDate { get; set; }
        public string SecondOriginalShiftDisplay { get; set; } = string.Empty;
        public DateOnly SecondExchangedDate { get; set; }
        public string SecondExchangedShiftDisplay { get; set; } = string.Empty;

        public int? ApprovalRequestId { get; set; }
        public JustificationStatus CurrentStatus { get; set; } = JustificationStatus.Pending;
        public string SubmittedBy { get; set; } = string.Empty;
        public DateTime SubmittedOn { get; set; }
        public DateTime? ReviewedOn { get; set; }
        public bool CanApprove { get; set; }
        public bool CanReject { get; set; }
        public ApprovalRequestTimelineViewModel? ApprovalTimeline { get; set; }
    }

    public class DutyExchangeRequestPrintViewModel
    {
        public string CompanyName { get; set; } = "Company";
        public string? CompanyAddress { get; set; }
        public string? CompanyLogoUrl { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public DateOnly DateFiled { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string SubmittedBy { get; set; } = string.Empty;
        public DateTime SubmittedOn { get; set; }
        public DateTime PrintedOn { get; set; } = DateTime.Now;

        public string FirstEmployeeName { get; set; } = string.Empty;
        public string FirstEmployeeIdNumber { get; set; } = string.Empty;
        public DateOnly FirstOriginalDate { get; set; }
        public string FirstOriginalShiftDisplay { get; set; } = string.Empty;
        public DateOnly FirstExchangedDate { get; set; }
        public string FirstExchangedShiftDisplay { get; set; } = string.Empty;

        public string SecondEmployeeName { get; set; } = string.Empty;
        public string SecondEmployeeIdNumber { get; set; } = string.Empty;
        public DateOnly SecondOriginalDate { get; set; }
        public string SecondOriginalShiftDisplay { get; set; } = string.Empty;
        public DateOnly SecondExchangedDate { get; set; }
        public string SecondExchangedShiftDisplay { get; set; } = string.Empty;
    }
}
