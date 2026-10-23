using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PayrollSystem.Data;
using PayrollSystem.Models;

namespace PayrollSystem.Services
{
    // Attendance-generation functions were removed from the app UI flow,
    // but pairing/validation and biometric-log processing remain in use.
    public class TimeEntryProcessingService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<TimeEntryProcessingService> _logger;

        public TimeEntryProcessingService(
            ApplicationDbContext db,
            ILogger<TimeEntryProcessingService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<BiometricImportResult> ImportBiometricLogs(
            List<BiometricLogData> rawData,
            string importedBy)
        {
            var result = new BiometricImportResult();
            var logs = new List<BiometricLog>();

            foreach (var data in rawData)
            {
                try
                {
                    var exists = await _db.BiometricLogs
                        .AnyAsync(b => b.EmployeeId == data.EmployeeId
                                    && b.Timestamp == data.Timestamp
                                    && b.DeviceId == data.DeviceId);

                    if (exists)
                    {
                        result.DuplicateCount++;
                        continue;
                    }

                    logs.Add(new BiometricLog
                    {
                        EmployeeId = data.EmployeeId,
                        Timestamp = data.Timestamp,
                        DeviceId = data.DeviceId,
                        Location = data.Location,
                        BiometricId = data.BiometricId,
                        RawData = data.RawData,
                        ImportedBy = importedBy,
                        ImportedAt = DateTime.Now
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error importing biometric log for employee: {EmployeeId}", data.EmployeeId);
                    result.ErrorCount++;
                }
            }

            if (logs.Any())
            {
                _db.BiometricLogs.AddRange(logs);
                await _db.SaveChangesAsync();
                result.ImportedCount = logs.Count;
            }

            return result;
        }

        public async Task<ProcessingResult> ProcessUnprocessedLogs()
        {
            var result = new ProcessingResult();

            var unprocessed = await _db.BiometricLogs
                .Where(b => !b.IsProcessed && !b.HasError)
                .OrderBy(b => b.EmployeeId)
                .ThenBy(b => b.Timestamp)
                .ToListAsync();

            // Group by employee and date for better processing
            var groupedLogs = unprocessed
                .GroupBy(log => new { log.EmployeeId, Date = DateOnly.FromDateTime(log.Timestamp) })
                .ToList();

            foreach (var group in groupedLogs)
            {
                var employeeId = group.Key.EmployeeId;
                var date = group.Key.Date;
                var logsForDay = group.OrderBy(l => l.Timestamp).ToList();

                try
                {
                    var employee = await _db.Employees
                        .FirstOrDefaultAsync(e => e.Id == employeeId);

                    if (employee == null)
                    {
                        foreach (var log in logsForDay)
                        {
                            log.HasError = true;
                            log.ErrorMessage = "Employee not found";
                            result.ErrorCount++;
                        }
                        continue;
                    }

                    // Get existing TimeEntries for this employee on this date
                    var existingEntries = await _db.TimeEntries
                        .Where(e => e.EmployeeId == employeeId && e.EntryDate == date)
                        .OrderBy(e => e.EntryTime)
                        .ToListAsync();

                    var logIdsForDay = logsForDay.Select(l => l.Id).ToList();
                    var existingTimeEntriesByLogId = await _db.TimeEntries
                        .Where(te => te.BiometricLogId != null && logIdsForDay.Contains(te.BiometricLogId.Value))
                        .Select(te => new { te.BiometricLogId, te.Id })
                        .ToListAsync();
                    var processedLogIds = existingTimeEntriesByLogId.ToDictionary(x => x.BiometricLogId!.Value, x => x.Id);

                    var allEntriesForDay = new List<(TimeOnly Time, EntryType Type, bool IsNew, TimeEntry? Entry)>();

                    foreach (var existing in existingEntries)
                    {
                        allEntriesForDay.Add((existing.EntryTime, existing.EntryType, false, existing));
                    }

                    // Process each log and determine type based on sequence
                    foreach (var log in logsForDay)
                    {
                        if (processedLogIds.TryGetValue(log.Id, out var existingTimeEntryId))
                        {
                            log.IsProcessed = true;
                            log.ProcessedAt = DateTime.Now;
                            log.TimeEntryId = existingTimeEntryId;
                            continue;
                        }

                        var time = TimeOnly.FromDateTime(log.Timestamp);

                        // Determine entry type based on all entries so far
                        var entryType = DetermineEntryTypeFromSequence(allEntriesForDay, time);

                        var timeEntry = new TimeEntry
                        {
                            EmployeeId = log.EmployeeId,
                            EntryDate = date,
                            EntryTime = time,
                            EntryType = entryType,
                            Source = EntrySource.Biometric,
                            BiometricLogId = log.Id,
                            BiometricDeviceId = log.DeviceId,
                            Location = log.Location,
                            IsManualEntry = false,
                            Status = EntryStatus.Valid,
                            CreatedBy = "Biometric System",
                            CreatedDate = DateTime.Now
                        };

                        _db.TimeEntries.Add(timeEntry);

                        allEntriesForDay.Add((time, entryType, true, timeEntry));

                        allEntriesForDay = allEntriesForDay.OrderBy(e => e.Time).ToList();

                        log.IsProcessed = true;
                        log.ProcessedAt = DateTime.Now;
                        log.TimeEntryId = timeEntry.Id;

                        result.ProcessedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing biometric logs for employee {EmployeeId} on {Date}",
                        employeeId, date);

                    foreach (var log in logsForDay)
                    {
                        log.HasError = true;
                        log.ErrorMessage = ex.Message;
                        result.ErrorCount++;
                    }
                }
            }

            await _db.SaveChangesAsync();
            return result;
        }

        // Determine entry type based on sequence of times
        private EntryType DetermineEntryTypeFromSequence(
            List<(TimeOnly Time, EntryType Type, bool IsNew, TimeEntry? Entry)> allEntries,
            TimeOnly newTime)
        {
            // Get the last entry before this time
            var previousEntry = allEntries
                .Where(e => e.Time < newTime)
                .OrderByDescending(e => e.Time)
                .FirstOrDefault();

            // If no previous entry, this is the first punch of the day = TimeIn
            if (previousEntry.Entry == null && previousEntry.Type == default)
            {
                return EntryType.TimeIn;
            }

            // Calculate time difference
            var timeDiff = (newTime.ToTimeSpan() - previousEntry.Time.ToTimeSpan()).TotalMinutes;

            // If less than 5 minutes apart, likely a duplicate - use same type
            if (timeDiff < 5)
            {
                return previousEntry.Type;
            }

            // Simple alternating logic: TimeIn → TimeOut → TimeIn → TimeOut
            // This works for most cases
            switch (previousEntry.Type)
            {
                case EntryType.TimeIn:
                    // After TimeIn comes TimeOut (if enough time has passed)
                    return timeDiff > 120 ? EntryType.TimeOut : EntryType.TimeIn;

                case EntryType.TimeOut:
                    // After TimeOut comes TimeIn (if enough time has passed - like lunch break)
                    return timeDiff > 30 ? EntryType.TimeIn : EntryType.TimeOut;

                case EntryType.BreakStart:
                    return EntryType.BreakEnd;

                case EntryType.BreakEnd:
                    return EntryType.TimeIn;

                case EntryType.LunchStart:
                    return EntryType.LunchEnd;

                case EntryType.LunchEnd:
                    return EntryType.TimeIn;

                default:
                    // Default: alternate
                    return EntryType.TimeIn;
            }
        }

        public async Task<ValidationResult> ValidateAndPairEntries(DateOnly date)
        {
            var result = new ValidationResult();

            var entries = await _db.TimeEntries
                .Where(e => e.EntryDate == date)
                .OrderBy(e => e.EmployeeId)
                .ThenBy(e => e.EntryTime)
                .ToListAsync();

            var groupedByEmployee = entries.GroupBy(e => e.EmployeeId);

            foreach (var group in groupedByEmployee)
            {
                var employeeEntries = group.ToList();
                await PairAndValidateEmployeeEntries(employeeEntries, result);
            }

            await _db.SaveChangesAsync();
            return result;
        }

        private static async Task PairAndValidateEmployeeEntries(
            List<TimeEntry> entries,
            ValidationResult result)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var current = entries[i];

                if (current.PairedEntryId != null) continue;

                var duplicates = entries
                    .Where(e => e.Id != current.Id
                             && Math.Abs((e.EntryTime.ToTimeSpan() - current.EntryTime.ToTimeSpan()).TotalMinutes) < 5)
                    .ToList();

                if (duplicates.Any())
                {
                    current.Status = EntryStatus.Duplicate;
                    current.ValidationNotes = $"Duplicate punch within 5 minutes: {string.Join(", ", duplicates.Select(d => d.EntryTime))}";
                    result.DuplicatesFound++;
                    continue;
                }

                if (current.EntryType == EntryType.TimeIn)
                {
                    var timeOut = entries.Skip(i + 1)
                        .FirstOrDefault(e => e.EntryType == EntryType.TimeOut
                                          && e.PairedEntryId == null
                                          && e.EntryTime > current.EntryTime);

                    if (timeOut != null)
                    {
                        current.PairedEntryId = timeOut.Id;
                        timeOut.PairedEntryId = current.Id;
                        current.Status = EntryStatus.Auto_Paired;
                        timeOut.Status = EntryStatus.Auto_Paired;
                        result.PairedCount++;
                    }
                    else
                    {
                        current.Status = EntryStatus.Missing_Pair;
                        current.ValidationNotes = "No matching TimeOut found";
                        result.MissingPairsFound++;
                    }
                }
                else if (current.EntryType == EntryType.BreakStart)
                {
                    var breakEnd = entries.Skip(i + 1)
                        .FirstOrDefault(e => e.EntryType == EntryType.BreakEnd
                                          && e.PairedEntryId == null
                                          && e.EntryTime > current.EntryTime);

                    if (breakEnd != null)
                    {
                        current.PairedEntryId = breakEnd.Id;
                        breakEnd.PairedEntryId = current.Id;
                        current.Status = EntryStatus.Auto_Paired;
                        breakEnd.Status = EntryStatus.Auto_Paired;
                    }
                }
                else if (current.EntryType == EntryType.LunchStart)
                {
                    var lunchEnd = entries.Skip(i + 1)
                        .FirstOrDefault(e => e.EntryType == EntryType.LunchEnd
                                          && e.PairedEntryId == null
                                          && e.EntryTime > current.EntryTime);

                    if (lunchEnd != null)
                    {
                        current.PairedEntryId = lunchEnd.Id;
                        lunchEnd.PairedEntryId = current.Id;
                        current.Status = EntryStatus.Auto_Paired;
                        lunchEnd.Status = EntryStatus.Auto_Paired;
                    }
                }
            }

            await Task.CompletedTask;
        }
    }

    public class BiometricImportResult
    {
        public int ImportedCount { get; set; }
        public int DuplicateCount { get; set; }
        public int ErrorCount { get; set; }
    }

    public class BiometricLogData
    {
        public Guid EmployeeId { get; set; }
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? BiometricId { get; set; }
        public string? RawData { get; set; }
    }

    public class ProcessingResult
    {
        public int ProcessedCount { get; set; }
        public int ErrorCount { get; set; }
    }

    public class ValidationResult
    {
        public int PairedCount { get; set; }
        public int MissingPairsFound { get; set; }
        public int DuplicatesFound { get; set; }
    }

}
