using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;

namespace PayrollSystem.Services
{
    public class DeductionService : IDeductionService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<DeductionService> _logger;

        public DeductionService(ApplicationDbContext db, ILogger<DeductionService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public Task<DeductionSaveResult> CreateWithAssignmentsAsync(DeductionSaveRequest request, string? userId)
            => SaveAsync(request, userId, isCreate: true);

        public Task<DeductionSaveResult> UpdateWithAssignmentsAsync(DeductionSaveRequest request, string? userId)
            => SaveAsync(request, userId, isCreate: false);

        private async Task<DeductionSaveResult> SaveAsync(DeductionSaveRequest request, string? userId, bool isCreate)
        {
            var targetEmployeeIds = await ResolveTargetEmployeeIdsAsync(request);
            if (!targetEmployeeIds.Any())
            {
                return new DeductionSaveResult
                {
                    Success = false,
                    Message = request.ApplyAllActiveEmployees
                        ? "There are no active employees to assign."
                        : "Select at least one employee, or enable Apply to all active employees."
                };
            }

            var deductionType = await _db.DeductionTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(dt => dt.Id == request.DeductionTypeId);

            if (deductionType == null)
            {
                return new DeductionSaveResult
                {
                    Success = false,
                    Message = "Selected deduction type not found."
                };
            }

            if (string.Equals(deductionType.Code, "LOAN", StringComparison.OrdinalIgnoreCase))
            {
                return new DeductionSaveResult
                {
                    Success = false,
                    Message = "Loan deductions use Employee Loans, not recurring deductions."
                };
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                Deduction? deduction;
                var now = DateTime.Now;

                if (isCreate)
                {
                    deduction = MapNewDeduction(request, userId, now);
                    _db.Deductions.Add(deduction);
                    await _db.SaveChangesAsync();
                }
                else
                {
                    if (!request.Id.HasValue)
                    {
                        return new DeductionSaveResult { Success = false, Message = "Deduction id is required." };
                    }

                    deduction = await _db.Deductions
                        .AsTracking()
                        .FirstOrDefaultAsync(d => d.Id == request.Id.Value && !d.IsDeleted);

                    if (deduction == null)
                    {
                        return new DeductionSaveResult { Success = false, Message = "Deduction not found." };
                    }

                    ApplyDeductionChanges(deduction, request, userId, now);
                    await _db.SaveChangesAsync();
                }

                var assignedCount = await SyncAssignmentsAsync(deduction.Id, targetEmployeeIds, userId, now);
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return new DeductionSaveResult
                {
                    Success = true,
                    DeductionId = deduction.Id,
                    AssignedEmployeeCount = assignedCount,
                    Message = isCreate
                        ? $"Deduction \"{deduction.DeductionName}\" created and assigned to {assignedCount} employee(s)."
                        : $"Deduction \"{deduction.DeductionName}\" updated and assigned to {assignedCount} employee(s)."
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to save deduction {DeductionId}", request.Id);
                return new DeductionSaveResult
                {
                    Success = false,
                    Message = "Could not save the deduction. Please try again."
                };
            }
        }

        private async Task<List<Guid>> ResolveTargetEmployeeIdsAsync(DeductionSaveRequest request)
        {
            if (request.ApplyAllActiveEmployees)
            {
                return await _db.Employees
                    .AsNoTracking()
                    .Where(e => e.IsActive && !e.IsDeleted)
                    .Select(e => e.Id)
                    .ToListAsync();
            }

            return request.EmployeeIds?.Distinct().ToList() ?? new List<Guid>();
        }

        private static Deduction MapNewDeduction(DeductionSaveRequest request, string? userId, DateTime now)
        {
            return new Deduction
            {
                DeductionName = request.DeductionName.Trim(),
                Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
                DeductionTypeId = request.DeductionTypeId,
                Amount = request.Amount,
                EffectiveStartDate = request.EffectiveStartDate,
                EffectiveEndDate = request.EffectiveEndDate,
                Remarks = request.Remarks,
                IsActive = request.IsActive,
                CreatedById = userId,
                CreatedOn = now
            };
        }

        private static void ApplyDeductionChanges(Deduction deduction, DeductionSaveRequest request, string? userId, DateTime now)
        {
            deduction.DeductionName = request.DeductionName.Trim();
            deduction.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
            deduction.DeductionTypeId = request.DeductionTypeId;
            deduction.Amount = request.Amount;
            deduction.EffectiveStartDate = request.EffectiveStartDate;
            deduction.EffectiveEndDate = request.EffectiveEndDate;
            deduction.Remarks = request.Remarks;
            deduction.IsActive = request.IsActive;
            deduction.UpdatedById = userId;
            deduction.UpdatedOn = now;
        }

        private async Task<int> SyncAssignmentsAsync(
            int deductionId,
            List<Guid> targetEmployeeIds,
            string? userId,
            DateTime now)
        {
            var targetSet = targetEmployeeIds.ToHashSet();

            var assignments = await _db.EmployeeDeductions
                .AsTracking()
                .Where(ed => ed.DeductionId == deductionId)
                .ToListAsync();

            var keeperByEmployee = new Dictionary<Guid, EmployeeDeduction>();

            foreach (var assignment in assignments.OrderBy(ed => ed.Id))
            {
                if (!keeperByEmployee.ContainsKey(assignment.EmployeeId))
                    keeperByEmployee[assignment.EmployeeId] = assignment;
            }

            foreach (var assignment in assignments)
            {
                if (!keeperByEmployee.TryGetValue(assignment.EmployeeId, out var keeper) || keeper.Id != assignment.Id)
                {
                    if (!assignment.IsDeleted)
                    {
                        assignment.IsDeleted = true;
                        assignment.IsActive = false;
                        assignment.UpdatedById = userId;
                        assignment.UpdatedOn = now;
                    }
                    continue;
                }

                if (!targetSet.Contains(assignment.EmployeeId))
                {
                    if (!assignment.IsDeleted)
                    {
                        assignment.IsDeleted = true;
                        assignment.IsActive = false;
                        assignment.UpdatedById = userId;
                        assignment.UpdatedOn = now;
                    }
                }
            }

            var activeCount = 0;
            foreach (var employeeId in targetSet)
            {
                if (keeperByEmployee.TryGetValue(employeeId, out var existing))
                {
                    if (existing.IsDeleted || !existing.IsActive)
                    {
                        existing.IsDeleted = false;
                        existing.IsActive = true;
                        existing.UpdatedById = userId;
                        existing.UpdatedOn = now;
                    }

                    activeCount++;
                    continue;
                }

                _db.EmployeeDeductions.Add(new EmployeeDeduction
                {
                    DeductionId = deductionId,
                    EmployeeId = employeeId,
                    IsActive = true,
                    CreatedById = userId,
                    CreatedOn = now
                });
                activeCount++;
            }

            return activeCount;
        }
    }
}
