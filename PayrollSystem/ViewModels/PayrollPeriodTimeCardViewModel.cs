using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PayrollSystem.ViewModels
{
    public class PayrollPeriodTimeCardRowViewModel
    {
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int? OrganizationStructureId { get; set; }
        public string OrganizationUnitName { get; set; } = string.Empty;
        public bool IsAssigned { get; set; }

        [Range(0, int.MaxValue)]
        public int LateMinutes { get; set; }

        [Range(0, int.MaxValue)]
        public int UndertimeMinutes { get; set; }

        [Range(0, double.MaxValue)]
        public decimal AbsentHours { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        // Per-day-type hour details
        public List<TimeCardDetailRowViewModel> Details { get; set; } = new();
    }

    public class PayrollPeriodTimeCardViewModel
    {
        public int PayrollPeriodId { get; set; }
        public string PayrollPeriodName { get; set; } = string.Empty;
        public string PeriodRange { get; set; } = string.Empty;
        public bool IsLocked { get; set; }

        public int? SelectedOrganizationStructureId { get; set; }
        public List<SelectListItem> OrganizationUnits { get; set; } = new();
        public List<DayTypeSelectItem> DayTypes { get; set; } = new();

        public List<PayrollPeriodTimeCardRowViewModel> Rows { get; set; } = new();
    }
}
