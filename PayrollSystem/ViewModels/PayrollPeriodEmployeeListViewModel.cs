using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PayrollSystem.ViewModels
{
    public class PayrollPeriodEmployeeListViewModel
    {
        public int PayrollPeriodId { get; set; }
        public string PayrollPeriodName { get; set; } = string.Empty;
        public string PeriodRange { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public List<PayrollPeriodEmployeeListRowViewModel> Rows { get; set; } = new();
    }

    public class PayrollPeriodEmployeeListRowViewModel
    {
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string OrganizationUnitName { get; set; } = string.Empty;
        public string EmploymentTypeName { get; set; } = string.Empty;
        public string BatchLabel { get; set; } = string.Empty;
        public int BatchId { get; set; }
        public int LateMinutes { get; set; }
        public int UndertimeMinutes { get; set; }
        public decimal AbsentHours { get; set; }
        public string? Notes { get; set; }

        // Per-day-type hour details for this employee's time card
        public List<TimeCardDetailRowViewModel> Details { get; set; } = new();
    }
}
