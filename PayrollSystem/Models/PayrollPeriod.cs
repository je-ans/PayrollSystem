using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PayrollSystem.Models;

public class PayrollPeriod : UserActivity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Display(Name = "Period Name")]
    public string Name { get; set; }

    [MaxLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Period Start Date")]
    public DateOnly PeriodStartDate { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Period End Date")]
    public DateOnly PeriodEndDate { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Pay Date")]
    public DateOnly PayDate { get; set; }

    [Display(Name = "Is Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Is Locked")]
    public bool IsLocked { get; set; } = false;

    // Navigation properties
    public virtual ICollection<PayrollRun> PayrollRuns { get; set; } = new List<PayrollRun>();
    public virtual ICollection<PayrollPeriodEmployee> PayrollPeriodEmployees { get; set; } = new List<PayrollPeriodEmployee>();
    public virtual ICollection<PayrollPeriodBatch> PayrollPeriodBatches { get; set; } = new List<PayrollPeriodBatch>();

    // Computed properties
    [NotMapped]
    public string PeriodRange => $"{PeriodStartDate:MMM dd, yyyy} - {PeriodEndDate:MMM dd, yyyy}";

    [NotMapped]
    public int DaysInPeriod => PeriodEndDate.DayNumber - PeriodStartDate.DayNumber + 1;

    [NotMapped]
    public bool IsFirstHalfOfMonth => PeriodStartDate.Day <= 15 && PeriodEndDate.Day <= 15;

    [NotMapped]
    public bool IsSecondHalfOfMonth => PeriodStartDate.Day >= 16 && PeriodEndDate.Day >= 16;
}