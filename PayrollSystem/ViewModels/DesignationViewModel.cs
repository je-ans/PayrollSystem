using System.Collections.Generic;

namespace PayrollSystem.ViewModels
{
    public class DesignationViewModel
    {
        public int Id { get; set; }
        public string DesignationName { get; set; }
        public string? DesignationDescription { get; set; }

        public bool IsActive { get; set; } = true;

        public int UserCount => Users?.Count ?? 0;
        public List<UserViewModel> Users { get; set; } = new();

        public string DisplayName => $"{DesignationName} ({UserCount} Users)";
    }
}
