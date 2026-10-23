using PayrollSystem.ViewModels;

namespace PayrollSystem.Services
{
    public interface IAllowanceService
    {
        Task<AllowanceSaveResult> CreateWithAssignmentsAsync(AllowanceSaveRequest request, string? userId);
        Task<AllowanceSaveResult> UpdateWithAssignmentsAsync(AllowanceSaveRequest request, string? userId);
    }

    public class AllowanceSaveRequest
    {
        public int? Id { get; set; }
        public string AllowanceName { get; set; } = string.Empty;
        public string? Code { get; set; }
        public int AllowanceTypeId { get; set; }
        public decimal Amount { get; set; }
        public Models.AllowanceFrequency Frequency { get; set; }
        public bool IsTaxable { get; set; } = true;
        public bool IsMandatory { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public bool ApplyAllActiveEmployees { get; set; }
        public List<Guid> EmployeeIds { get; set; } = new();
    }

    public class AllowanceSaveResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int AllowanceId { get; set; }
        public int AssignedEmployeeCount { get; set; }
    }
}
