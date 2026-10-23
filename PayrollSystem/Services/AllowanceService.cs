using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Services
{
    public class AllowanceService : IAllowanceService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<AllowanceService> _logger;

        public AllowanceService(ApplicationDbContext db, ILogger<AllowanceService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public Task<AllowanceSaveResult> CreateWithAssignmentsAsync(AllowanceSaveRequest request, string? userId)
            => SaveAsync(request, userId, isCreate: true);

        public Task<AllowanceSaveResult> UpdateWithAssignmentsAsync(AllowanceSaveRequest request, string? userId)
            => SaveAsync(request, userId, isCreate: false);

        private async Task<AllowanceSaveResult> SaveAsync(AllowanceSaveRequest request, string? userId, bool isCreate)
        {
            var targetEmployeeIds = await ResolveTargetEmployeeIdsAsync(request);
            if (!targetEmployeeIds.Any())
            {
                return new AllowanceSaveResult
                {
                    Success = false,
                    Message = request.ApplyAllActiveEmployees
                        ? "There are no active employees to assign."
                        : "Select at least one employee, or enable Apply to all active employees."
                };
            }

            var allowanceTypeExists = await _db.AllowanceTypes
                .AnyAsync(at => at.Id == request.AllowanceTypeId && !at.IsDeleted);
            if (!allowanceTypeExists)
            {
                return new AllowanceSaveResult
                {
                    Success = false,
                    Message = "Selected allowance type not found."
                };
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                Allowance? allowance;
                var now = DateTime.Now;

                if (isCreate)
                {
                    allowance = MapNewAllowance(request, userId, now);
                    _db.Allowances.Add(allowance);
                    await _db.SaveChangesAsync();
                }
                else
                {
                    if (!request.Id.HasValue)
                    {
                        return new AllowanceSaveResult { Success = false, Message = "Allowance id is required." };
                    }

                    allowance = await _db.Allowances
                        .AsTracking()
                        .FirstOrDefaultAsync(a => a.Id == request.Id.Value && !a.IsDeleted);

                    if (allowance == null)
                    {
                        return new AllowanceSaveResult { Success = false, Message = "Allowance not found." };
                    }

                    ApplyAllowanceChanges(allowance, request, userId, now);
                    await _db.SaveChangesAsync();
                }

                var assignedCount = await SyncAssignmentsAsync(allowance.Id, targetEmployeeIds, userId, now);
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return new AllowanceSaveResult
                {
                    Success = true,
                    AllowanceId = allowance.Id,
                    AssignedEmployeeCount = assignedCount,
                    Message = isCreate
                        ? $"Allowance \"{allowance.AllowanceName}\" created and assigned to {assignedCount} employee(s)."
                        : $"Allowance \"{allowance.AllowanceName}\" updated and assigned to {assignedCount} employee(s)."
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to save allowance {AllowanceId}", request.Id);
                return new AllowanceSaveResult
                {
                    Success = false,
                    Message = "Could not save the allowance. Please try again."
                };
            }
        }

        private async Task<List<Guid>> ResolveTargetEmployeeIdsAsync(AllowanceSaveRequest request)
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

        private static Allowance MapNewAllowance(AllowanceSaveRequest request, string? userId, DateTime now)
        {
            return new Allowance
            {
                AllowanceName = request.AllowanceName.Trim(),
                Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
                AllowanceTypeId = request.AllowanceTypeId,
                Amount = request.Amount,
                Frequency = request.Frequency,
                IsTaxable = request.IsTaxable,
                IsMandatory = request.IsMandatory,
                EffectiveDate = request.EffectiveDate,
                EndDate = request.EndDate,
                Description = request.Description,
                IsActive = request.IsActive,
                CreatedById = userId,
                CreatedOn = now
            };
        }

        private static void ApplyAllowanceChanges(Allowance allowance, AllowanceSaveRequest request, string? userId, DateTime now)
        {
            allowance.AllowanceName = request.AllowanceName.Trim();
            allowance.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
            allowance.AllowanceTypeId = request.AllowanceTypeId;
            allowance.Amount = request.Amount;
            allowance.Frequency = request.Frequency;
            allowance.IsTaxable = request.IsTaxable;
            allowance.IsMandatory = request.IsMandatory;
            allowance.EffectiveDate = request.EffectiveDate;
            allowance.EndDate = request.EndDate;
            allowance.Description = request.Description;
            allowance.IsActive = request.IsActive;
            allowance.UpdatedById = userId;
            allowance.UpdatedOn = now;
        }

        private async Task<int> SyncAssignmentsAsync(
            int allowanceId,
            List<Guid> targetEmployeeIds,
            string? userId,
            DateTime now)
        {
            var targetSet = targetEmployeeIds.ToHashSet();

            var assignments = await _db.EmployeeAllowances
                .AsTracking()
                .Where(ea => ea.AllowanceId == allowanceId)
                .ToListAsync();

            var keeperByEmployee = new Dictionary<Guid, EmployeeAllowance>();

            foreach (var assignment in assignments.OrderBy(ea => ea.Id))
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

                _db.EmployeeAllowances.Add(new EmployeeAllowance
                {
                    AllowanceId = allowanceId,
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
