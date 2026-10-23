using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PayrollSystem.Models.Workflow;

namespace PayrollSystem.Models
{
    public class TimeLogJustification
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid EmployeeId { get; set; }
        public virtual Employee Employee { get; set; } = null!;

        [Required]
        [DataType(DataType.Date)]
        public DateOnly EntryDate { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Justification { get; set; } = string.Empty;

        public TimeOnly? RequestedAmIn { get; set; }
        public TimeOnly? RequestedLunchOut { get; set; }
        public TimeOnly? RequestedLunchIn { get; set; }
        public TimeOnly? RequestedPmOut { get; set; }

        [Required]
        public JustificationStatus Status { get; set; } = JustificationStatus.Pending;

        [MaxLength(1000)]
        public string? ReviewNotes { get; set; }

        [MaxLength(256)]
        public string? SubmittedBy { get; set; }

        public DateTime SubmittedOn { get; set; } = DateTime.Now;

        public string? ReviewedById { get; set; }
        public virtual ApplicationUser? ReviewedBy { get; set; }

        public DateTime? ReviewedOn { get; set; }

        public int? ApprovalRequestId { get; set; }
        public virtual ApprovalRequest? ApprovalRequest { get; set; }

        [NotMapped]
        public string RequestedTimesDisplay
        {
            get
            {
                var parts = new List<string>();
                if (RequestedAmIn.HasValue) parts.Add($"AM In: {RequestedAmIn.Value:hh:mm tt}");
                if (RequestedLunchOut.HasValue) parts.Add($"Lunch Out: {RequestedLunchOut.Value:hh:mm tt}");
                if (RequestedLunchIn.HasValue) parts.Add($"Lunch In: {RequestedLunchIn.Value:hh:mm tt}");
                if (RequestedPmOut.HasValue) parts.Add($"PM Out: {RequestedPmOut.Value:hh:mm tt}");
                return parts.Count > 0 ? string.Join(" | ", parts) : "-";
            }
        }
    }
}
