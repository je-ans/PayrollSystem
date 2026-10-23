using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace PayrollSystem.Services
{
    public class BiometricDataParser
    {
        private readonly ILogger<BiometricDataParser> _logger;

        public BiometricDataParser(ILogger<BiometricDataParser> logger)
        {
            _logger = logger;
        }

        // Parses Attendance Record Report (Format 1) - the one with concatenated times.
        // Example: "08:2208:2218:4018:40" = punches at 08:22, 08:22, 18:40, 18:40
        public BiometricParseResult ParseAttendanceRecordReport(string reportData)
        {
            var result = new BiometricParseResult();

            try
            {
                var lines = reportData.Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToArray();

                var dateRangeLine = lines.FirstOrDefault(l => l.Contains("Att. Time") || l.Contains("~"));
                var dateRange = ExtractDateRange(dateRangeLine);

                if (dateRange == null)
                {
                    result.ErrorMessage = "Could not parse date range from report header";
                    return result;
                }

                result.StartDate = dateRange.Value.Start;
                result.EndDate = dateRange.Value.End;

                int dayNumbersLineIndex = FindDayNumbersLine(lines);
                if (dayNumbersLineIndex == -1)
                {
                    result.ErrorMessage = "Could not find day numbers row";
                    return result;
                }

                var dayNumbers = ParseDayNumbers(lines[dayNumbersLineIndex]);

                for (int i = dayNumbersLineIndex + 1; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("ID:"))
                    {
                        var employeeInfo = ParseEmployeeInfo(lines[i]);

                        if (employeeInfo != null && i + 1 < lines.Length)
                        {
                            var punchLine = lines[i + 1];
                            var employeeRecord = ParseEmployeePunchData(
                                employeeInfo,
                                punchLine,
                                dayNumbers,
                                result.StartDate);

                            if (employeeRecord != null)
                            {
                                result.EmployeeRecords.Add(employeeRecord);
                            }
                        }

                        i++; // Skip punch line
                    }
                }

                result.Success = result.EmployeeRecords.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing attendance report");
                result.ErrorMessage = $"Parse error: {ex.Message}";
            }

            return result;
        }

        private (DateTime Start, DateTime End)? ExtractDateRange(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;

            var match = Regex.Match(line, @"(\d{4})-(\d{2})-(\d{2})\s*~\s*(\d{4})-(\d{2})-(\d{2})");

            if (match.Success)
            {
                try
                {
                    var start = new DateTime(
                        int.Parse(match.Groups[1].Value),
                        int.Parse(match.Groups[2].Value),
                        int.Parse(match.Groups[3].Value));

                    var end = new DateTime(
                        int.Parse(match.Groups[4].Value),
                        int.Parse(match.Groups[5].Value),
                        int.Parse(match.Groups[6].Value));

                    return (start, end);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private int FindDayNumbersLine(string[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (Regex.IsMatch(lines[i], @"^\d+[\s\t]+\d+[\s\t]+\d+"))
                {
                    return i;
                }
            }
            return -1;
        }

        private List<int> ParseDayNumbers(string line)
        {
            return Regex.Matches(line, @"\d+")
                .Select(m => int.Parse(m.Value))
                .ToList();
        }

        private EmployeeInfo? ParseEmployeeInfo(string line)
        {
            try
            {
                var idMatch = Regex.Match(line, @"ID:\s*(\d+)");
                var nameMatch = Regex.Match(line, @"Name:\s*([^;]+);?");
                var deptMatch = Regex.Match(line, @"Dept\.?:\s*(.+?)(?:\s{2,}|$)");

                if (!idMatch.Success) return null;

                return new EmployeeInfo
                {
                    BiometricId = idMatch.Groups[1].Value.Trim(),
                    EmployeeName = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "Unknown",
                    Department = deptMatch.Success ? deptMatch.Groups[1].Value.Trim() : ""
                };
            }
            catch
            {
                return null;
            }
        }

        private EmployeeBiometricRecord? ParseEmployeePunchData(
            EmployeeInfo employeeInfo,
            string punchLine,
            List<int> dayNumbers,
            DateTime startDate)
        {
            try
            {
                var record = new EmployeeBiometricRecord
                {
                    BiometricId = employeeInfo.BiometricId,
                    EmployeeName = employeeInfo.EmployeeName,
                    Department = employeeInfo.Department,
                    DailyRecords = new List<DailyPunchRecord>()
                };

                var punchDataColumns = punchLine.Split('\t');

                for (int i = 0; i < Math.Min(dayNumbers.Count, punchDataColumns.Length); i++)
                {
                    var dayNumber = dayNumbers[i];
                    var punchData = punchDataColumns[i].Trim();

                    if (string.IsNullOrWhiteSpace(punchData))
                        continue;

                    DateTime date;
                    try
                    {
                        date = new DateTime(startDate.Year, startDate.Month, dayNumber);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        var nextMonth = startDate.AddMonths(1);
                        date = new DateTime(nextMonth.Year, nextMonth.Month, dayNumber);
                    }

                    var times = ExtractTimesFromConcatenated(punchData);

                    if (times.Any())
                    {
                        record.DailyRecords.Add(new DailyPunchRecord
                        {
                            Date = DateOnly.FromDateTime(date),
                            PunchTimes = times
                        });
                    }
                }

                return record.DailyRecords.Any() ? record : null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse punch data for {EmployeeId}", employeeInfo.BiometricId);
                return null;
            }
        }

        private List<TimeOnly> ExtractTimesFromConcatenated(string concatenatedTimes)
        {
            var times = new List<TimeOnly>();

            // Remove whitespace
            var clean = Regex.Replace(concatenatedTimes, @"\s+", "");

            // Extract all HH:MM patterns
            var matches = Regex.Matches(clean, @"\d{2}:\d{2}");

            foreach (Match match in matches)
            {
                try
                {
                    var time = TimeOnly.Parse(match.Value);
                    times.Add(time);
                }
                catch
                {
                    _logger.LogWarning("Invalid time format: {Time}", match.Value);
                }
            }

            // Remove consecutive duplicates
            var deduplicated = new List<TimeOnly>();
            TimeOnly? lastTime = null;

            foreach (var time in times)
            {
                if (lastTime == null || time != lastTime.Value)
                {
                    deduplicated.Add(time);
                    lastTime = time;
                }
            }

            return deduplicated;
        }
    }

    #region Supporting Classes

    public class EmployeeInfo
    {
        public string BiometricId { get; set; }
        public string EmployeeName { get; set; }
        public string Department { get; set; }
    }

    public class BiometricParseResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<EmployeeBiometricRecord> EmployeeRecords { get; set; } = new();

        public int TotalPunchesCount => EmployeeRecords
            .SelectMany(e => e.DailyRecords)
            .Sum(d => d.PunchTimes.Count);
    }

    public class EmployeeBiometricRecord
    {
        public string BiometricId { get; set; }
        public string EmployeeName { get; set; }
        public string Department { get; set; }
        public List<DailyPunchRecord> DailyRecords { get; set; } = new();
    }

    public class DailyPunchRecord
    {
        public DateOnly Date { get; set; }
        public List<TimeOnly> PunchTimes { get; set; } = new();
    }

    #endregion
}