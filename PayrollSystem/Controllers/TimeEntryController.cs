using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Services;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    public class TimeEntryController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<TimeEntryController> _logger;
        private readonly BiometricDataParser _biometricParser;
        private readonly TimeEntryProcessingService _processingService;

        public TimeEntryController(
            ApplicationDbContext db,
            ILogger<TimeEntryController> logger,
            BiometricDataParser biometricParser,
            TimeEntryProcessingService processingService)
        {
            _db = db;
            _logger = logger;
            _biometricParser = biometricParser;
            _processingService = processingService;
        }

        //=======================================================Private Method=================================================================

        private JsonResult ValidationErrorResponse()
        {
            var errorMessages = ModelStateHelper.GetModelErrors(ModelState);
            _logger.LogWarning("Validation failed: {Errors}", string.Join("; ", errorMessages));
            return Json(new { success = false, message = string.Join("; ", errorMessages) });
        }

        private async Task ReprocessAttendanceForDateAsync(Guid employeeId, DateOnly date)
        {
            await _processingService.ValidateAndPairEntries(date);
        }

        //=======================================================Get Time Entries=============================================================\

        [Permission("TIME_ENTRY_VIEW")]
        public async Task<ActionResult> Index(DateTime? startDate = null, DateTime? endDate = null, Guid? employeeId = null)
        {
            var query = _db.TimeEntries
                .Include(te => te.Employee)
                .AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(te => te.EntryDate >= DateOnly.FromDateTime(startDate.Value));
            }

            if (endDate.HasValue)
            {
                query = query.Where(te => te.EntryDate <= DateOnly.FromDateTime(endDate.Value));
            }

            if (employeeId.HasValue)
            {
                query = query.Where(te => te.EmployeeId == employeeId.Value);
            }

            var timeEntries = await query
                .OrderByDescending(te => te.EntryDate)
                .ThenByDescending(te => te.EntryTime)
                .Take(1000)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Employees = await _db.Employees
                .Where(e => !e.IsDeleted && e.IsActive)
                .Select(e => new
                {
                    Id = e.Id,
                    Name = $"{e.FirstName} {e.Surname} ({e.EmployeeIdNumber})"
                })
                .AsNoTracking()
                .ToListAsync();

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.EmployeeId = employeeId;

            return View(timeEntries);
        }

        //=======================================================Import Biometric Data==============================================================\

        [Permission("TIME_ENTRY_VIEW")]
        public ActionResult ImportBiometricData()
        {
            var viewModel = new BiometricImportViewModel();
            return PartialView("Partials/_ImportBiometricData", viewModel);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("TIME_ENTRY_VIEW")]
        public async Task<ActionResult> ImportBiometricData(BiometricImportViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            try
            {
                _logger.LogInformation("Starting biometric import");

                var parseResult = _biometricParser.ParseAttendanceRecordReport(model.BiometricData);

                if (!parseResult.Success)
                {
                    return Json(new
                    {
                        success = false,
                        message = parseResult.ErrorMessage ?? "Failed to parse biometric data"
                    });
                }

                var importedCount = 0;
                var duplicateCount = 0;
                var invalidEmployeeCount = 0;
                var biometricLogs = new List<BiometricLog>();
                var debugMessages = new List<string>
                {
                    $"Report date range (from header): {parseResult.StartDate:yyyy-MM-dd} to {parseResult.EndDate:yyyy-MM-dd}"
                };

                foreach (var employeeRecord in parseResult.EmployeeRecords)
                {
                    debugMessages.Add($"Processing Biometric ID: {employeeRecord.BiometricId}, Name: {employeeRecord.EmployeeName}");

                    var employee = await _db.Employees
                        .FirstOrDefaultAsync(e =>
                            e.BiometricId == employeeRecord.BiometricId &&
                            !e.IsDeleted &&
                            e.IsActive);

                    if (employee == null)
                    {
                        _logger.LogWarning("Employee not found with BiometricId: {BiometricId}",
                            employeeRecord.BiometricId);

                        debugMessages.Add($"  ❌ Biometric ID '{employeeRecord.BiometricId}' not mapped to any employee");

                        var mappedEmployees = await _db.Employees
                            .Where(e => !e.IsDeleted && e.IsActive && e.BiometricId != null)
                            .Select(e => new { e.BiometricId, e.EmployeeIdNumber, e.FirstName, e.Surname })
                            .Take(10)
                            .ToListAsync();

                        if (mappedEmployees.Any())
                        {
                            debugMessages.Add($"  Mapped Biometric IDs: {string.Join(", ", mappedEmployees.Select(e => $"{e.BiometricId}→{e.EmployeeIdNumber} ({e.FirstName} {e.Surname})"))}");
                        }
                        else
                        {
                            debugMessages.Add("  ⚠ No employees have BiometricId set! Please run the mapping SQL.");
                        }

                        invalidEmployeeCount++;
                        continue;
                    }

                    debugMessages.Add($"  ✓ Mapped to: {employee.EmployeeIdNumber} - {employee.FirstName} {employee.Surname}");

                    foreach (var dailyRecord in employeeRecord.DailyRecords)
                    {
                        debugMessages.Add($"    {dailyRecord.Date:yyyy-MM-dd}: {dailyRecord.PunchTimes.Count} punches");

                        foreach (var punchTime in dailyRecord.PunchTimes)
                        {
                            var timestamp = dailyRecord.Date.ToDateTime(punchTime);

                            var exists = await _db.BiometricLogs
                                .AnyAsync(b => b.EmployeeId == employee.Id
                                            && b.Timestamp >= timestamp.AddMinutes(-1)
                                            && b.Timestamp <= timestamp.AddMinutes(1));

                            if (exists)
                            {
                                duplicateCount++;
                                continue;
                            }

                            biometricLogs.Add(new BiometricLog
                            {
                                EmployeeId = employee.Id,
                                Timestamp = timestamp,
                                DeviceId = "IMPORTED_ATTENDANCE_REPORT",
                                Location = employeeRecord.Department,
                                BiometricId = employeeRecord.BiometricId,
                                RawData = $"{dailyRecord.Date:yyyy-MM-dd} {punchTime:HH:mm}",
                                ImportedBy = User.Identity?.Name ?? "System",
                                ImportedAt = DateTime.Now,
                                IsProcessed = false
                            });

                            importedCount++;
                        }
                    }
                }

                if (biometricLogs.Any())
                {
                    _db.BiometricLogs.AddRange(biometricLogs);
                    await _db.SaveChangesAsync();

                    var processingResult = await _processingService.ProcessUnprocessedLogs();

                    for (var date = DateOnly.FromDateTime(parseResult.StartDate);
                        date <= DateOnly.FromDateTime(parseResult.EndDate);
                        date = date.AddDays(1))
                    {
                        await _processingService.ValidateAndPairEntries(date);
                    }

                    var message = $"<strong>✓ Import Successful</strong><br/><br/>" +
                                $"• Imported: {importedCount} punches<br/>" +
                                $"• Processed: {processingResult.ProcessedCount} time entries<br/>";

                    if (duplicateCount > 0)
                        message += $"<br/>⚠ Skipped {duplicateCount} duplicates";
                    if (invalidEmployeeCount > 0)
                        message += $"<br/>⚠ {invalidEmployeeCount} unmapped biometric IDs";

                    message += $"<br/><br/><details><summary>Show details</summary><pre style='font-size:11px; max-height:300px; overflow-y:auto;'>{string.Join("\n", debugMessages)}</pre></details>";

                    return Json(new { success = true, message = message });
                }
                else
                {
                    var message = $"<strong>No data imported</strong><br/><br/>";
                    if (duplicateCount > 0)
                        message += $"All {duplicateCount} punches were skipped as duplicates (already exist in BiometricLogs).<br/>";
                    if (invalidEmployeeCount > 0)
                        message += $"{invalidEmployeeCount} employee(s) could not be mapped (BiometricId not found).<br/>";
                    if (duplicateCount == 0 && invalidEmployeeCount == 0)
                        message += "No valid punch data was found in the report.<br/>";
                    message += $"<br/><details open><summary>Details</summary><pre style='font-size:11px; max-height:400px; overflow-y:auto;'>{string.Join("\n", debugMessages)}</pre></details>";

                    return Json(new { success = false, message = message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing biometric data");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        private List<BiometricEntry> ParseBiometricData(string data, int skipLines = 4)
        {
            var entries = new List<BiometricEntry>();
            var lines = data.Split('\n').Skip(skipLines).Where(line => !string.IsNullOrWhiteSpace(line));

            foreach (var line in lines)
            {
                try
                {
                    // Split by tab character
                    var parts = line.Split('\t');
                    if (parts.Length < 12) continue;

                    var entry = new BiometricEntry
                    {
                        EmployeeIdNumber = parts[0].Trim(),
                        EmployeeName = parts[1].Trim(),
                        Department = parts[2].Trim(),
                        Date = DateOnly.Parse(parts[3].Trim()),
                        FirstTimeZoneIn = ParseTime(parts[4].Trim()),
                        FirstTimeZoneOut = ParseTime(parts[5].Trim()),
                        SecondTimeZoneIn = ParseTime(parts[6].Trim()),
                        SecondTimeZoneOut = ParseTime(parts[7].Trim()),
                        LateMinutes = ParseInt(parts[8].Trim()),
                        LeaveEarlyMinutes = ParseInt(parts[9].Trim()),
                        AbsenceMinutes = ParseInt(parts[10].Trim()),
                        TotalMinutes = ParseInt(parts[11].Trim()),
                        Note = parts.Length > 12 ? parts[12].Trim() : ""
                    };

                    entries.Add(entry);
                }
                catch (Exception)
                {
                    // Skip invalid lines
                    continue;
                }
            }

            return entries;
        }

        private TimeOnly? ParseTime(string timeString)
        {
            if (string.IsNullOrWhiteSpace(timeString)) return null;

            try
            {
                return TimeOnly.Parse(timeString);
            }
            catch
            {
                return null;
            }
        }

        private int ParseInt(string intString)
        {
            if (string.IsNullOrWhiteSpace(intString)) return 0;

            try
            {
                return int.Parse(intString);
            }
            catch
            {
                return 0;
            }
        }

        //=======================================================Import Time Entries==============================================================\

        [Permission("TIME_ENTRY_VIEW")]
        public ActionResult ImportTimeEntries()
        {
            var timeEntries = new List<TimeEntry>();
            return PartialView("Partials/_ImportTimeEntries", timeEntries);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("TIME_ENTRY_VIEW")]
        public async Task<ActionResult> ImportTimeEntries(string timeEntriesJson)
        {
            if (string.IsNullOrWhiteSpace(timeEntriesJson))
            {
                return Json(new { success = false, message = "No data provided." });
            }

            try
            {
                var timeEntries = System.Text.Json.JsonSerializer.Deserialize<List<TimeEntry>>(timeEntriesJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (timeEntries == null || !timeEntries.Any())
                {
                    return Json(new { success = false, message = "No valid time entries found in the data." });
                }

                var importedCount = 0;
                var duplicateCount = 0;
                var invalidEmployeeCount = 0;

                foreach (var entry in timeEntries)
                {
                    // Validate required fields
                    if (entry.EmployeeId == Guid.Empty || entry.EntryDate == default || entry.EntryTime == default)
                    {
                        invalidEmployeeCount++;
                        continue;
                    }

                    // Check for duplicate entries (same employee, same datetime, same entry type)
                    var existingEntry = await _db.TimeEntries
                        .FirstOrDefaultAsync(te =>
                            te.EmployeeId == entry.EmployeeId &&
                            te.EntryDate == entry.EntryDate &&
                            te.EntryTime == entry.EntryTime &&
                            te.EntryType == entry.EntryType);

                    if (existingEntry == null)
                    {
                        entry.Source = entry.Source == default ? EntrySource.Imported : entry.Source;
                        entry.Status = entry.Status == default ? EntryStatus.Valid : entry.Status;
                        entry.CreatedDate = DateTime.Now;
                        entry.CreatedBy = User.Identity?.Name ?? "Import System";
                        _db.TimeEntries.Add(entry);
                        importedCount++;
                    }
                    else
                    {
                        duplicateCount++;
                    }
                }

                var result = await _db.SaveChangesAsync();

                var affectedDates = timeEntries
                    .Select(te => new { te.EmployeeId, te.EntryDate })
                    .Distinct()
                    .ToList();

                foreach (var item in affectedDates)
                {
                    await ReprocessAttendanceForDateAsync(item.EmployeeId, item.EntryDate);
                }

                var message = $"Successfully imported {importedCount} time entries.";
                if (duplicateCount > 0)
                {
                    message += $" {duplicateCount} duplicates were skipped.";
                }
                if (invalidEmployeeCount > 0)
                {
                    message += $" {invalidEmployeeCount} entries were invalid.";
                }

                return Json(new { success = true, message = message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing time entries");
                return Json(new { success = false, message = "An error occurred while importing time entries. Please check the JSON format." });
            }
        }

        //=======================================================Create Manual Time Entry==============================================================\

        [Permission("TIME_ENTRY_VIEW")]
        public async Task<ActionResult> CreateTimeEntry()
        {
            ViewBag.Employees = await _db.Employees
                .Where(e => !e.IsDeleted && e.IsActive)
                .Select(e => new
                {
                    Id = e.Id,
                    Name = $"{e.FirstName} {e.Surname} ({e.EmployeeIdNumber})"
                })
                .AsNoTracking()
                .ToListAsync();

            var viewModel = new TimeEntryViewModel
            {
                IsManualEntry = true,
                EntryDateTime = DateTime.Now
            };

            return PartialView("Partials/_CreateTimeEntry", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("TIME_ENTRY_VIEW")]
        public async Task<ActionResult> CreateTimeEntry(TimeEntryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            if (!model.EmployeeId.HasValue || !model.EntryDateTime.HasValue)
            {
                return ValidationErrorResponse();
            }

            try
            {
                var timeEntry = new TimeEntry
                {
                    EmployeeId = model.EmployeeId.Value,
                    EntryType = model.EntryType,
                    EntryDate = DateOnly.FromDateTime(model.EntryDateTime.Value),
                    EntryTime = TimeOnly.FromDateTime(model.EntryDateTime.Value),
                    Source = EntrySource.Manual,
                    BiometricDeviceId = model.BiometricDeviceId,
                    Location = model.Location,
                    Notes = model.Notes,
                    IsManualEntry = true,
                    Status = EntryStatus.Valid,
                    CreatedBy = User.Identity?.Name,
                    CreatedDate = DateTime.Now
                };

                _db.TimeEntries.Add(timeEntry);
                var result = await _db.SaveChangesAsync();

                await ReprocessAttendanceForDateAsync(timeEntry.EmployeeId, timeEntry.EntryDate);

                if (result <= 0)
                {
                    _logger.LogError("Failed to save manual time entry for employee: {EmployeeId}", model.EmployeeId);
                    return Json(new { success = false, message = "Failed to create time entry." });
                }

                return Json(new { success = true, message = "Time entry created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating manual time entry for employee: {EmployeeId}", model.EmployeeId);
                return Json(new { success = false, message = "An error occurred while creating the time entry." });
            }
        }

        //=======================================================Edit Time Entry==============================================================\

        [Permission("TIME_ENTRY_VIEW")]
        public async Task<ActionResult> EditTimeEntry(Guid id)
        {
            ViewBag.Employees = await _db.Employees
                .Where(e => !e.IsDeleted && e.IsActive)
                .Select(e => new
                {
                    Id = e.Id,
                    Name = $"{e.FirstName} {e.Surname} ({e.EmployeeIdNumber})"
                })
                .AsNoTracking()
                .ToListAsync();

            var timeEntry = await _db.TimeEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(te => te.Id == id);

            if (timeEntry == null)
            {
                return NotFound();
            }

            var viewModel = new TimeEntryViewModel
            {
                Id = timeEntry.Id,
                EmployeeId = timeEntry.EmployeeId,
                EntryType = timeEntry.EntryType,
                EntryDateTime = timeEntry.EntryDate.ToDateTime(timeEntry.EntryTime),
                BiometricDeviceId = timeEntry.BiometricDeviceId,
                Location = timeEntry.Location,
                Notes = timeEntry.Notes,
                IsManualEntry = timeEntry.IsManualEntry
            };

            return PartialView("Partials/_EditTimeEntry", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("TIME_ENTRY_VIEW")]
        public async Task<ActionResult> EditTimeEntry(TimeEntryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            if (!model.Id.HasValue || !model.EmployeeId.HasValue || !model.EntryDateTime.HasValue)
            {
                return ValidationErrorResponse();
            }

            try
            {
                var existingEntry = await _db.TimeEntries
                    .FirstOrDefaultAsync(te => te.Id == model.Id.Value);

                if (existingEntry == null)
                {
                    return Json(new { success = false, message = "Time entry not found." });
                }

                existingEntry.EmployeeId = model.EmployeeId.Value;
                existingEntry.EntryType = model.EntryType;
                existingEntry.EntryDate = DateOnly.FromDateTime(model.EntryDateTime.Value);
                existingEntry.EntryTime = TimeOnly.FromDateTime(model.EntryDateTime.Value);
                existingEntry.BiometricDeviceId = model.BiometricDeviceId;
                existingEntry.Location = model.Location;
                existingEntry.Notes = model.Notes;
                existingEntry.ModifiedBy = User.Identity?.Name;
                existingEntry.ModifiedDate = DateTime.Now;

                _db.TimeEntries.Update(existingEntry);

                var result = await _db.SaveChangesAsync();

                await ReprocessAttendanceForDateAsync(existingEntry.EmployeeId, existingEntry.EntryDate);

                return Json(new { success = true, message = "Time entry updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating time entry: {EntryId}", model.Id);
                return Json(new { success = false, message = "An error occurred while updating the time entry." });
            }
        }

        //=======================================================Delete Time Entry==============================================================\

        [Permission("TIME_ENTRY_VIEW")]
        public async Task<ActionResult> DeleteTimeEntry(Guid id)
        {
            var timeEntry = await _db.TimeEntries
                .Include(te => te.Employee)
                .AsNoTracking()
                .FirstOrDefaultAsync(te => te.Id == id);

            if (timeEntry == null)
            {
                return NotFound();
            }

            return PartialView("Partials/_DeleteTimeEntry", timeEntry);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("TIME_ENTRY_VIEW")]
        public async Task<ActionResult> DeleteTimeEntryConfirmed(Guid id)
        {
            try
            {
                var timeEntry = await _db.TimeEntries
                    .FirstOrDefaultAsync(te => te.Id == id);

                if (timeEntry == null)
                {
                    return Json(new { success = false, message = "Time entry not found." });
                }

                _db.TimeEntries.Remove(timeEntry);
                var result = await _db.SaveChangesAsync();

                await ReprocessAttendanceForDateAsync(timeEntry.EmployeeId, timeEntry.EntryDate);

                if (result <= 0)
                {
                    _logger.LogError("Failed to delete time entry: {EntryId}", id);
                    return Json(new { success = false, message = "Failed to delete time entry." });
                }

                return Json(new { success = true, message = "Time entry deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting time entry: {EntryId}", id);
                return Json(new { success = false, message = "An error occurred while deleting the time entry." });
            }
        }

        //=======================================================API Endpoints for Biometric Integration==============================================================\

        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult> BiometricEntry([FromBody] BiometricEntryRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.EmployeeIdNumber) || !request.EntryDateTime.HasValue)
                {
                    return BadRequest("Invalid request data");
                }

                // Find employee by ID number
                var employee = await _db.Employees
                    .FirstOrDefaultAsync(e =>
                        e.EmployeeIdNumber == request.EmployeeIdNumber &&
                        !e.IsDeleted &&
                        e.IsActive);

                if (employee == null)
                {
                    return NotFound("Employee not found");
                }

                // Create time entry
                var timeEntry = new TimeEntry
                {
                    EmployeeId = employee.Id,
                    EntryType = request.EntryType,
                    EntryDate = DateOnly.FromDateTime(request.EntryDateTime.Value),
                    EntryTime = TimeOnly.FromDateTime(request.EntryDateTime.Value),
                    Source = EntrySource.Biometric,
                    BiometricDeviceId = request.DeviceId,
                    Location = request.Location,
                    Notes = request.Notes,
                    IsManualEntry = false,
                    Status = EntryStatus.Valid,
                    CreatedBy = "Biometric System",
                    CreatedDate = DateTime.Now
                };

                _db.TimeEntries.Add(timeEntry);
                await _db.SaveChangesAsync();

                await ReprocessAttendanceForDateAsync(timeEntry.EmployeeId, timeEntry.EntryDate);

                return Ok(new { success = true, message = "Time entry recorded successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing biometric entry");
                return StatusCode(500, "Internal server error");
            }
        }

        public class BiometricEntryRequest
        {
            public string EmployeeIdNumber { get; set; } = string.Empty;
            public EntryType EntryType { get; set; }
            public DateTime? EntryDateTime { get; set; }
            public string DeviceId { get; set; } = string.Empty;
            public string Location { get; set; } = string.Empty;
            public string Notes { get; set; } = string.Empty;
        }
    }
}