using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PayrollSystem.ViewModels
{
    public class PayrollHoursRowViewModel
    {
        public int PayrollEntryId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int? OrganizationStructureId { get; set; }
        public string OrganizationUnitName { get; set; } = string.Empty;

        [Range(0, double.MaxValue)]
        public decimal RegularHours { get; set; }

        [Range(0, double.MaxValue)]
        public decimal OvertimeHours { get; set; }
    }

    public class PayrollHoursEditViewModel
    {
        public int PayrollRunId { get; set; }
        public string PayrollRunName { get; set; } = string.Empty;

        public int? SelectedOrganizationStructureId { get; set; }
        public List<SelectListItem> OrganizationUnits { get; set; } = new List<SelectListItem>();

        public List<PayrollHoursRowViewModel> Rows { get; set; } = new List<PayrollHoursRowViewModel>();
    }
}
