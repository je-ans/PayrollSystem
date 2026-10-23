using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PayrollSystem.ViewModels
{
    public class AssignEmployeesAndTimeCardViewModel
    {
        public int PayrollPeriodId { get; set; }
        public string PayrollPeriodName { get; set; } = string.Empty;
        public string PeriodRange { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public bool HasCompletedPayrollRun { get; set; }
        public List<PayrollPeriodBatchSummaryRowViewModel> Batches { get; set; } = new();
        public int TotalEmployees => Batches?.Sum(b => b.EmployeeCount) ?? 0;
    }

    public class PayrollPeriodBatchSummaryRowViewModel
    {
        public int BatchId { get; set; }
        public string OrganizationUnitName { get; set; } = string.Empty;
        public string EmploymentTypeName { get; set; } = string.Empty;
        public int EmployeeCount { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class AssignEmployeesAndTimeCardBatchEditViewModel
    {
        public int PayrollPeriodId { get; set; }
        public int? BatchId { get; set; }
        public string PayrollPeriodName { get; set; } = string.Empty;
        public string PeriodRange { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public List<int> SelectedOrganizationStructureIds { get; set; } = new();
        public int? SelectedEmploymentBasis { get; set; }
        public List<SelectListItem> OrganizationUnits { get; set; } = new();
        public List<SelectListItem> EmploymentTypes { get; set; } = new();
        public List<AssignEmployeesAndTimeCardRowViewModel> Rows { get; set; } = new();
        public int ExcludedCount { get; set; }

        // Available day types, passed from the controller so the view can render per-day-type columns.
        public List<DayTypeSelectItem> DayTypes { get; set; } = new();

        public int RegularDayTypeIndex { get; set; } = -1;
    }

    public class DayTypeSelectItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // One row: employee + time card in one model.
    // Hours are broken down by DayType via Details; LateMinutes, UndertimeMinutes,
    // and AbsentHours remain on the header (Absence Deduction / Late-Undertime).
    public class AssignEmployeesAndTimeCardRowViewModel
    {
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string OrganizationUnitName { get; set; } = string.Empty;
        public string EmploymentTypeName { get; set; } = string.Empty;

        public bool IsSelected { get; set; }

        [Range(0, int.MaxValue)]
        public int LateMinutes { get; set; }

        [Range(0, int.MaxValue)]
        public int UndertimeMinutes { get; set; }

        [Range(0, double.MaxValue)]
        public decimal AbsentHours { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        // Per-day-type hour details. Index matches the DayTypes list in the parent view model
        public List<TimeCardDetailRowViewModel> Details { get; set; } = new();
    }

    //Hours for a single DayType within one employee's time card
    public class TimeCardDetailRowViewModel
    {
        public int DayTypeId { get; set; }
        public string DayTypeName { get; set; } = string.Empty;

        [Range(0, double.MaxValue)]
        public decimal RegularHours { get; set; }

        [Range(0, double.MaxValue)]
        public decimal OvertimeHours { get; set; }

        [Range(0, double.MaxValue)]
        public decimal NightDifferentialHours { get; set; }

        [Range(0, double.MaxValue)]
        public decimal NightDifferentialOvertimeHours { get; set; }
    }

    public class ImportTimeCardFromDtrRequest
    {
        public int PayrollPeriodId { get; set; }
        public List<Guid> EmployeeIds { get; set; } = new();
    }

    public class ImportTimeCardFromDtrRowResult
    {
        public Guid EmployeeId { get; set; }
        public int LateMinutes { get; set; }
        public int UndertimeMinutes { get; set; }
        public decimal AbsentHours { get; set; }
        public string? Notes { get; set; }
        public Dictionary<string, decimal> HoursByDayTypeName { get; set; } = new();
        public Dictionary<string, decimal> NightDifferentialHoursByDayTypeName { get; set; } = new();
        public Dictionary<string, decimal> OvertimeHoursByDayTypeName { get; set; } = new();
        public Dictionary<string, decimal> NightDifferentialOvertimeHoursByDayTypeName { get; set; } = new();
    }

    public class CreateBatchQuickRequest
    {
        public int PayrollPeriodId { get; set; }

        public string? EmployeeIds { get; set; }

        public bool ImportFromDtr { get; set; } = true;
    }
}
