using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class BiometricImportViewModel
    {
        [Required]
        [Display(Name = "Biometric Data")]
        public string BiometricData { get; set; }

        [Display(Name = "Skip First Row")]
        public bool SkipFirstRow { get; set; } = true;

        [Display(Name = "Skip Header Rows")]
        public bool SkipHeaderRows { get; set; } = true;

        [Display(Name = "Header Row Count")]
        public int HeaderRowCount { get; set; } = 4; // Skip the first 4 lines (title, date range, headers)

        public List<BiometricEntry> ParsedEntries { get; set; } = new List<BiometricEntry>();
    }

    public class BiometricEntry
    {
        public string EmployeeIdNumber { get; set; }
        public string EmployeeName { get; set; }
        public string Department { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly? FirstTimeZoneIn { get; set; }
        public TimeOnly? FirstTimeZoneOut { get; set; }
        public TimeOnly? SecondTimeZoneIn { get; set; }
        public TimeOnly? SecondTimeZoneOut { get; set; }
        public int LateMinutes { get; set; }
        public int LeaveEarlyMinutes { get; set; }
        public int AbsenceMinutes { get; set; }
        public int TotalMinutes { get; set; }
        public string Note { get; set; }

        public List<TimeEntry> ToTimeEntries()
        {
            var entries = new List<TimeEntry>();

            // Create time entries for first time zone
            if (FirstTimeZoneIn.HasValue)
            {
                entries.Add(new TimeEntry
                {
                    EmployeeId = Guid.Empty, // Will be resolved by employee ID number
                    EntryType = EntryType.TimeIn,
                    EntryDate = Date,
                    EntryTime = FirstTimeZoneIn.Value,
                    Source = EntrySource.Imported,
                    BiometricDeviceId = "BiometricImport",
                    Location = Department,
                    IsManualEntry = false,
                    Status = EntryStatus.Valid,
                    CreatedBy = "Biometric System",
                    CreatedDate = DateTime.Now
                });
            }

            if (FirstTimeZoneOut.HasValue && FirstTimeZoneOut != FirstTimeZoneIn)
            {
                entries.Add(new TimeEntry
                {
                    EmployeeId = Guid.Empty,
                    EntryType = EntryType.TimeOut,
                    EntryDate = Date,
                    EntryTime = FirstTimeZoneOut.Value,
                    Source = EntrySource.Imported,
                    BiometricDeviceId = "BiometricImport",
                    Location = Department,
                    IsManualEntry = false,
                    Status = EntryStatus.Valid,
                    CreatedBy = "Biometric System",
                    CreatedDate = DateTime.Now
                });
            }

            // Create time entries for second time zone (if present)
            if (SecondTimeZoneIn.HasValue)
            {
                entries.Add(new TimeEntry
                {
                    EmployeeId = Guid.Empty,
                    EntryType = EntryType.LunchStart, // Assuming this is lunch break
                    EntryDate = Date,
                    EntryTime = SecondTimeZoneIn.Value,
                    Source = EntrySource.Imported,
                    BiometricDeviceId = "BiometricImport",
                    Location = Department,
                    IsManualEntry = false,
                    Status = EntryStatus.Valid,
                    CreatedBy = "Biometric System",
                    CreatedDate = DateTime.Now
                });
            }

            if (SecondTimeZoneOut.HasValue)
            {
                entries.Add(new TimeEntry
                {
                    EmployeeId = Guid.Empty,
                    EntryType = EntryType.LunchEnd, // Assuming this is lunch break
                    EntryDate = Date,
                    EntryTime = SecondTimeZoneOut.Value,
                    Source = EntrySource.Imported,
                    BiometricDeviceId = "BiometricImport",
                    Location = Department,
                    IsManualEntry = false,
                    Status = EntryStatus.Valid,
                    CreatedBy = "Biometric System",
                    CreatedDate = DateTime.Now
                });
            }

            return entries;
        }
    }
}