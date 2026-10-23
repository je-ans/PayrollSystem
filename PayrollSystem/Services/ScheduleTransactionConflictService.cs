using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.Services
{
    // Detects overlapping schedule transactions for the same employee + calendar date.
    // Checks Schedule Change, Duty Exchange, and Exchange Rest Day.
    public interface IScheduleTransactionConflictService
    {
        Task<(bool hasConflict, string message)> FindConflictAsync(
            Guid employeeId,
            DateOnly date,
            ScheduleTransactionExclusion? exclude = null);

        Task<(bool hasConflict, string message)> FindConflictsAsync(
            IEnumerable<(Guid EmployeeId, DateOnly Date)> keys,
            ScheduleTransactionExclusion? exclude = null);
    }

    // Identifiers to ignore when validating the request currently being submitted or applied.
    public sealed class ScheduleTransactionExclusion
    {
        public Guid? ScheduleChangeRequestId { get; init; }
        public Guid? DutyExchangeRequestId { get; init; }
        public Guid? ExchangeRestDayRequestId { get; init; }
    }

    public class ScheduleTransactionConflictService : IScheduleTransactionConflictService
    {
        private readonly ApplicationDbContext _db;

        public ScheduleTransactionConflictService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<(bool hasConflict, string message)> FindConflictAsync(
            Guid employeeId,
            DateOnly date,
            ScheduleTransactionExclusion? exclude = null)
        {
            return await FindConflictsAsync(new[] { (employeeId, date) }, exclude);
        }

        public async Task<(bool hasConflict, string message)> FindConflictsAsync(
            IEnumerable<(Guid EmployeeId, DateOnly Date)> keys,
            ScheduleTransactionExclusion? exclude = null)
        {
            var keyList = keys
                .Distinct()
                .ToList();

            if (keyList.Count == 0)
                return (false, string.Empty);

            foreach (var (employeeId, date) in keyList)
            {
                var scheduleChangeConflict = await FindPendingScheduleChangeAsync(
                    employeeId, date, exclude?.ScheduleChangeRequestId);
                if (scheduleChangeConflict != null)
                    return (true, scheduleChangeConflict);

                var dutyExchangeConflict = await FindPendingDutyExchangeAsync(
                    employeeId, date, exclude?.DutyExchangeRequestId);
                if (dutyExchangeConflict != null)
                    return (true, dutyExchangeConflict);

                var restDayConflict = await FindPendingExchangeRestDayAsync(
                    employeeId, date, exclude?.ExchangeRestDayRequestId);
                if (restDayConflict != null)
                    return (true, restDayConflict);
            }

            return (false, string.Empty);
        }

        private async Task<string?> FindPendingScheduleChangeAsync(
            Guid employeeId,
            DateOnly date,
            Guid? excludeId)
        {
            var candidates = await _db.ScheduleChangeRequests
                .AsNoTracking()
                .Include(r => r.ApprovalRequest)
                .Include(r => r.WorkSchedule)
                .Where(r =>
                    r.EmployeeId == employeeId &&
                    r.Day == date.Day &&
                    r.WorkSchedule.Month == date.Month &&
                    r.WorkSchedule.Year == date.Year)
                .Where(r => !excludeId.HasValue || r.Id != excludeId.Value)
                .ToListAsync();

            var pending = candidates.FirstOrDefault(r =>
                ApprovalRequestStatusHelper.IsPending(r.Status, r.ApprovalRequest?.Status));

            if (pending == null)
                return null;

            return $"A pending schedule change already exists for this employee on {date:MMM dd, yyyy}.";
        }

        private async Task<string?> FindPendingDutyExchangeAsync(
            Guid employeeId,
            DateOnly date,
            Guid? excludeId)
        {
            var candidates = await _db.DutyExchangeRequests
                .AsNoTracking()
                .Include(r => r.ApprovalRequest)
                .Where(r => !r.IsDeleted)
                .Where(r =>
                    (r.FirstEmployeeId == employeeId &&
                     (r.FirstOriginalDate == date || r.FirstExchangedDate == date)) ||
                    (r.SecondEmployeeId == employeeId &&
                     (r.SecondOriginalDate == date || r.SecondExchangedDate == date)))
                .Where(r => !excludeId.HasValue || r.Id != excludeId.Value)
                .ToListAsync();

            var pending = candidates.FirstOrDefault(r =>
                ApprovalRequestStatusHelper.IsPending(r.Status, r.ApprovalRequest?.Status));

            if (pending == null)
                return null;

            return $"A pending exchange of duty already exists for this employee on {date:MMM dd, yyyy}.";
        }

        private async Task<string?> FindPendingExchangeRestDayAsync(
            Guid employeeId,
            DateOnly date,
            Guid? excludeId)
        {
            var candidates = await _db.ExchangeRestDayRequests
                .AsNoTracking()
                .Include(r => r.ApprovalRequest)
                .Where(r => !r.IsDeleted)
                .Where(r =>
                    (r.FirstEmployeeId == employeeId &&
                     (r.FirstRestDate == date || r.SecondRestDate == date)) ||
                    (r.SecondEmployeeId == employeeId &&
                     (r.FirstRestDate == date || r.SecondRestDate == date)))
                .Where(r => !excludeId.HasValue || r.Id != excludeId.Value)
                .ToListAsync();

            var pending = candidates.FirstOrDefault(r =>
                ApprovalRequestStatusHelper.IsPending(r.Status, r.ApprovalRequest?.Status));

            if (pending == null)
                return null;

            return $"A pending exchange rest day already exists for this employee on {date:MMM dd, yyyy}.";
        }
    }
}
