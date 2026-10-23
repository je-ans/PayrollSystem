using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models.BuildUp;
using PayrollSystem.Models.Workflow;

namespace PayrollSystem.Models
{
    public class DutyExchangeRequest : UserActivity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public int? OrganizationStructureId { get; set; }
        public virtual OrganizationStructure? OrganizationStructure { get; set; }

        [DataType(DataType.Date)]
        public DateOnly DateFiled { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required]
        public Guid FirstEmployeeId { get; set; }
        public virtual Employee FirstEmployee { get; set; } = null!;

        [Required]
        [DataType(DataType.Date)]
        public DateOnly FirstOriginalDate { get; set; }

        [Required]
        public int FirstOriginalShiftTypeId { get; set; }
        public virtual ShiftType FirstOriginalShiftType { get; set; } = null!;

        [Required]
        [DataType(DataType.Date)]
        public DateOnly FirstExchangedDate { get; set; }

        [Required]
        public int FirstExchangedShiftTypeId { get; set; }
        public virtual ShiftType FirstExchangedShiftType { get; set; } = null!;

        [Required]
        public Guid SecondEmployeeId { get; set; }
        public virtual Employee SecondEmployee { get; set; } = null!;

        [Required]
        [DataType(DataType.Date)]
        public DateOnly SecondOriginalDate { get; set; }

        [Required]
        public int SecondOriginalShiftTypeId { get; set; }
        public virtual ShiftType SecondOriginalShiftType { get; set; } = null!;

        [Required]
        [DataType(DataType.Date)]
        public DateOnly SecondExchangedDate { get; set; }

        [Required]
        public int SecondExchangedShiftTypeId { get; set; }
        public virtual ShiftType SecondExchangedShiftType { get; set; } = null!;

        [Required]
        [MaxLength(2000)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? SubmittedBy { get; set; }

        public DateTime SubmittedOn { get; set; } = DateTime.Now;

        public JustificationStatus Status { get; set; } = JustificationStatus.Pending;

        [MaxLength(1000)]
        public string? ReviewNotes { get; set; }

        [MaxLength(256)]
        public string? ReviewedById { get; set; }
        public virtual ApplicationUser? ReviewedBy { get; set; }

        public DateTime? ReviewedOn { get; set; }

        public int? ApprovalRequestId { get; set; }
        public virtual ApprovalRequest? ApprovalRequest { get; set; }
    }
}
