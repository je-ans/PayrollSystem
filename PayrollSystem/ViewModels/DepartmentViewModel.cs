using System.Collections.Generic;

namespace PayrollSystem.ViewModels
{
    public class DepartmentViewModel
    {
        public int Id { get; set; }
        public string DepartmentName { get; set; }
        public string? DepartmentDescription { get; set; }

        public bool IsActive { get; set; } = true;

        public int UserCount => Users?.Count ?? 0;
        public List<UserViewModel> Users { get; set; } = new();

        public string DisplayName => $"{DepartmentName} ({UserCount} Users)";

    }
}
