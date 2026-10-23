using System;
using System.ComponentModel.DataAnnotations;

namespace PayrollSystem.Models
{
    public enum EmploymentBasis
    {
        Regular,
        Casual,
        Contractual
    }

    public enum RateBasis
    {
        Monthly,
        Daily,
        Hourly
    }

    public enum IncomeBasis
    {
        [Display(Name = "Annual")]
        Annual,

        [Display(Name = "Monthly")]
        Monthly,

        [Display(Name = "Semi-Monthly")]
        SemiMonthly,

        [Display(Name = "Bi-Weekly")]
        BiWeekly,

        [Display(Name = "Weekly")]
        Weekly,

        [Display(Name = "Daily")]
        Daily
    }

    public enum HourType
    {
        [Display(Name = "Basic Hours")]
        Basic,

        [Display(Name = "Overtime")]
        Overtime,

        [Display(Name = "Night Differential")]
        NightDifferential,

        [Display(Name = "Night Differential Overtime")]
        NightDifferentialOvertime
    }

    public enum AttendanceStatus
    {
        [Display(Name = "Present")]
        Present,

        [Display(Name = "Late")]
        Late,

        [Display(Name = "Absent")]
        Absent,

        [Display(Name = "On Leave")]
        OnLeave,

        [Display(Name = "Half Day")]
        HalfDay,

        [Display(Name = "Undertime")]
        Undertime,

        [Display(Name = "Day Off")]
        DayOff,

        [Display(Name = "Holiday")]
        Holiday,

        [Display(Name = "Rest Day")]
        RestDay
    }

    public enum DtrStatus
    {
        [Display(Name = "Draft")]
        Draft,

        [Display(Name = "Employee Review")]
        Employee_Review,

        [Display(Name = "Submitted")]
        Submitted,

        [Display(Name = "Supervisor Review")]
        Supervisor_Review,

        [Display(Name = "Approved")]
        Approved,

        [Display(Name = "Payroll Processed")]
        Payroll_Processed,

        [Display(Name = "Disputed")]
        Disputed,

        [Display(Name = "Locked")]
        Locked,

        [Display(Name = "Archived")]
        Archived
    }

    public enum RoundingMode
    {
        [Display(Name = "Round Up")]
        Up,

        [Display(Name = "Round Down")]
        Down,

        [Display(Name = "Round to Nearest")]
        Nearest
    }

    public enum EntryType
    {
        [Display(Name = "Time In")]
        TimeIn,

        [Display(Name = "Time Out")]
        TimeOut,

        [Display(Name = "Break Start")]
        BreakStart,

        [Display(Name = "Break End")]
        BreakEnd,

        [Display(Name = "Lunch Start")]
        LunchStart,

        [Display(Name = "Lunch End")]
        LunchEnd
    }

    public enum EntrySource
    {
        [Display(Name = "Biometric Device")]
        Biometric,

        [Display(Name = "Manual Entry")]
        Manual,

        [Display(Name = "Imported")]
        Imported,

        [Display(Name = "System Generated")]
        SystemGenerated
    }

    public enum EntryStatus
    {
        [Display(Name = "Valid")]
        Valid,

        [Display(Name = "Pending Pair")]
        Pending_Pair,

        [Display(Name = "Auto Paired")]
        Auto_Paired,

        [Display(Name = "Manually Paired")]
        Manually_Paired,

        [Display(Name = "Duplicate")]
        Duplicate,

        [Display(Name = "Invalid Sequence")]
        Invalid_Sequence,

        [Display(Name = "Missing Pair")]
        Missing_Pair,

        [Display(Name = "Disputed")]
        Disputed,

        [Display(Name = "Corrected")]
        Corrected,

        [Display(Name = "Flagged")]
        Flagged
    }

    public enum JustificationStatus
    {
        [Display(Name = "Pending")]
        Pending = 0,
        [Display(Name = "Approved")]
        Approved = 1,
        [Display(Name = "Rejected")]
        Rejected = 2
    }

    public enum OvertimeStatus
    {
        [Display(Name = "Pending")]
        Pending = 0,
        [Display(Name = "Approved")]
        Approved = 1,
        [Display(Name = "Rejected")]
        Rejected = 2
    }

    public enum HolidayType
    {
        [Display(Name = "Regular Holiday")]
        Regular = 0,
        [Display(Name = "Special Non-Working Holiday")]
        Special = 1,
        [Display(Name = "Company Holiday")]
        Company = 2
    }

    public enum OvertimeRoundingRule
    {
        [Display(Name = "None")]
        None = 0,
        [Display(Name = "Nearest 15 minutes")]
        Nearest15Minutes = 1,
        [Display(Name = "Nearest 30 minutes")]
        Nearest30Minutes = 2,
        [Display(Name = "Round up to next 15 minutes")]
        RoundUp15Minutes = 3,
        [Display(Name = "Round up to next 30 minutes")]
        RoundUp30Minutes = 4
    }

    public enum WorkScheduleStatus
    {
        [Display(Name = "Draft")]
        Draft = 0,
        [Display(Name = "Submitted")]
        Submitted = 1,
        [Display(Name = "Approved")]
        Approved = 2,
        [Display(Name = "Rejected")]
        Rejected = 3
    }

    public enum OrganizationLevel
    {
        [Display(Name = "Institution")]
        Institution = 1,
        [Display(Name = "Office")]
        Office = 2,
        [Display(Name = "Division")]
        Division = 3,
        [Display(Name = "Department")]
        Department = 4,
        [Display(Name = "Section")]
        Section = 5,
        [Display(Name = "Unit")]
        Unit = 6,
        [Display(Name = "Team")]
        Team = 7
    }

    public enum OrganizationLeadershipRole
    {
        [Display(Name = "Head")]
        Head = 1,
        [Display(Name = "Officer-in-Charge")]
        OIC = 2,
        [Display(Name = "Assistant Head")]
        AssistantHead = 3
    }

    public enum OrganizationFunctionalRole
    {
        [Display(Name = "Scheduler")]
        Scheduler = 1,
        [Display(Name = "Timekeeper")]
        Timekeeper = 2,
        [Display(Name = "HR Approver")]
        HRApprover = 3,
        [Display(Name = "Schedule Approver")]
        ScheduleApprover = 4,
        [Display(Name = "DTR Approver")]
        DtrApprover = 5,
        [Display(Name = "Payroll Processor")]
        PayrollProcessor = 6,
        [Display(Name = "Payroll Approver")]
        PayrollApprover = 7
    }

    public enum WorkflowProcessType
    {
        [Display(Name = "Default (Fallback)")]
        Default = 0,
        [Display(Name = "Work Schedule")]
        WorkSchedule = 1,
        [Display(Name = "DTR")]
        Dtr = 2,
        [Display(Name = "Leave")]
        Leave = 3,
        [Display(Name = "Overtime")]
        Overtime = 4,
        [Display(Name = "Payroll")]
        Payroll = 5,
        [Display(Name = "Schedule Change")]
        ScheduleChange = 6,
        [Display(Name = "Time Log Justification")]
        TimeLogJustification = 7,
        [Display(Name = "Exchange of Duty")]
        DutyExchange = 8,
        [Display(Name = "Exchange Rest Day")]
        ExchangeRestDay = 9,
        [Display(Name = "Undertime")]
        Undertime = 10
    }

    public enum ApproverSourceType
    {
        [Display(Name = "Leadership Role")]
        LeadershipRole = 1,
        [Display(Name = "Functional Role")]
        FunctionalRole = 2
    }

    public enum ApprovalRequestStatus
    {
        [Display(Name = "Pending")]
        Pending = 0,
        [Display(Name = "In Progress")]
        InProgress = 1,
        [Display(Name = "Approved")]
        Approved = 2,
        [Display(Name = "Rejected")]
        Rejected = 3,
        [Display(Name = "Cancelled")]
        Cancelled = 4
    }

    public enum ApprovalStepStatus
    {
        [Display(Name = "Waiting")]
        Waiting = 0,
        [Display(Name = "Pending")]
        Pending = 1,
        [Display(Name = "Approved")]
        Approved = 2,
        [Display(Name = "Rejected")]
        Rejected = 3,
        [Display(Name = "Skipped")]
        Skipped = 4
    }

    //public enum ShiftType
    //{
    //    [Display(Name = "Regular Day")]
    //    Regular,

    //    [Display(Name = "Morning Shift (8AM-5PM)")]
    //    Morning,

    //    [Display(Name = "Afternoon Shift (2PM-11PM)")]
    //    Afternoon,

    //    [Display(Name = "Night Shift")]
    //    Night,

    //    [Display(Name = "Flexible Hours")]
    //    Flexible,

    //    [Display(Name = "Split Shift")]
    //    Split,

    //    [Display(Name = "Custom")]
    //    Custom
    //}

    public enum PayrollStatus
    {
        [Display(Name = "Draft")]
        Draft,

        [Display(Name = "Processing")]
        Processing,

        [Display(Name = "Completed")]
        Completed,

        [Display(Name = "Cancelled")]
        Cancelled
    }

    public enum PayrollFrequency
    {
        [Display(Name = "Weekly")]
        Weekly,

        [Display(Name = "Bi-Weekly")]
        BiWeekly,

        [Display(Name = "Semi-Monthly")]
        SemiMonthly,

        [Display(Name = "Monthly")]
        Monthly
    }

    public enum EmployeeLoanStatus
    {
        [Display(Name = "Active")]
        Active,

        [Display(Name = "Completed")]
        Completed,

        [Display(Name = "Cancelled")]
        Cancelled
    }

    public enum BenefitType
    {
        [Display(Name = "Basic Salary")]
        BasicSalary,

        [Display(Name = "Overtime Pay")]
        Overtime,

        [Display(Name = "Night Differential")]
        NightDifferential,

        [Display(Name = "Holiday Pay")]
        HolidayPay,

        [Display(Name = "Allowance")]
        Allowance,

        [Display(Name = "Bonus")]
        Bonus,

        [Display(Name = "13th Month Pay")]
        ThirteenthMonth,

        [Display(Name = "Other")]
        Other
    }

    public enum TaxBracketType
    {
        [Display(Name = "Progressive")]
        Progressive = 0,

        [Display(Name = "Percentage")]
        Percentage = 1,

        [Display(Name = "Fixed Amount")]
        FixedAmount = 2,

        [Display(Name = "Daily")]
        Daily = 3,

        [Display(Name = "Weekly")]
        Weekly = 4,

        [Display(Name = "Bi-Weekly")]
        BiWeekly = 5,

        [Display(Name = "Semi-Monthly")]
        SemiMonthly = 6,

        [Display(Name = "Monthly")]
        Monthly = 7,

        [Display(Name = "Annually")]
        Annually = 8
    }

    public enum BenefitCalculationMethod
    {
        [Display(Name = "Fixed Amount")]
        FixedAmount,

        [Display(Name = "Percentage of Basic Pay")]
        PercentageOfBasicPay,

        [Display(Name = "Amount Per Hour")]
        AmountPerHour,

        [Display(Name = "Amount Per Day")]
        AmountPerDay
    }

    public enum DeductionCalculationMethod
    {
        [Display(Name = "Fixed Amount")]
        FixedAmount,

        [Display(Name = "Percentage of Gross Pay")]
        PercentageOfGrossPay,

        [Display(Name = "Percentage of Basic Pay")]
        PercentageOfBasicPay
    }

    public enum ContributionCalculationMethod
    {
        [Display(Name = "Fixed Amount")]
        Fixed,

        [Display(Name = "Percentage")]
        Percentage,

        [Display(Name = "Tiered")]
        Tiered
    }

    public enum SalaryType
    {
        [Display(Name = "Monthly")]
        Monthly,

        [Display(Name = "Daily")]
        Daily,

        [Display(Name = "Hourly")]
        Hourly
    }

    public enum PaymentMethod
    {
        [Display(Name = "Bank Transfer")]
        BankTransfer,

        [Display(Name = "Cash")]
        Cash,

        [Display(Name = "Check")]
        Check
    }

    public enum AllowanceFrequency
    {
        [Display(Name = "Monthly")]
        Monthly,

        [Display(Name = "Daily")]
        Daily,

        [Display(Name = "Hourly")]
        Hourly,

        [Display(Name = "Yearly")]
        Yearly,

        [Display(Name = "One-time")]
        OneTime
    }

    public enum SalaryChangeType
    {
        [Display(Name = "Salary Increase")]
        Increase,

        [Display(Name = "Salary Decrease")]
        Decrease,

        [Display(Name = "Promotion")]
        Promotion,

        [Display(Name = "Demotion")]
        Demotion,

        [Display(Name = "Cost of Living Adjustment")]
        COLA,

        [Display(Name = "Performance Bonus")]
        PerformanceBonus,

        [Display(Name = "Market Adjustment")]
        MarketAdjustment,

        [Display(Name = "Other")]
        Other
    }

    public enum ContributionApplicationRule
    {
        [Display(Name = "Every Payroll")]
        EveryPayroll = 0,

        [Display(Name = "First Period Only")]
        FirstPeriodOnly = 1,

        [Display(Name = "Never")]
        Never = 2
    }

}