namespace PayrollSystem.Services
{
    public interface IDeductionService
    {
        Task<DeductionSaveResult> CreateWithAssignmentsAsync(DeductionSaveRequest request, string? userId);
        Task<DeductionSaveResult> UpdateWithAssignmentsAsync(DeductionSaveRequest request, string? userId);
    }

    public class DeductionSaveRequest
    {
        public int? Id { get; set; }
        public string DeductionName { get; set; } = string.Empty;
        public string? Code { get; set; }
        public int DeductionTypeId { get; set; }
        public decimal Amount { get; set; }
        public DateOnly EffectiveStartDate { get; set; }
        public DateOnly? EffectiveEndDate { get; set; }
        public string? Remarks { get; set; }
        public bool IsActive { get; set; } = true;
        public bool ApplyAllActiveEmployees { get; set; }
        public List<Guid> EmployeeIds { get; set; } = new();
    }

    public class DeductionSaveResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int DeductionId { get; set; }
        public int AssignedEmployeeCount { get; set; }
    }
}
