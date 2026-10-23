using PayrollSystem.Models;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Utility
{
    public static class EmployeeDisplayHelper
    {
        public static string GetInitials(string? firstName, string? surname)
        {
            var parts = new List<char>();
            if (!string.IsNullOrWhiteSpace(firstName))
                parts.Add(char.ToUpperInvariant(firstName.Trim()[0]));
            if (!string.IsNullOrWhiteSpace(surname))
                parts.Add(char.ToUpperInvariant(surname.Trim()[0]));

            return parts.Count == 0 ? "?" : new string(parts.ToArray());
        }

        public static string GetInitialsFromLabel(string? label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return "?";

            var words = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return "?";

            if (words.Length == 1)
                return char.ToUpperInvariant(words[0][0]).ToString();

            return $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[^1][0])}";
        }

        public static string? GetProfilePictureUrl(string? profilePicture)
        {
            if (string.IsNullOrWhiteSpace(profilePicture))
                return null;

            return $"/uploads/{profilePicture}";
        }

        public static string FormatEmployeeName(string firstName, string surname) =>
            $"{firstName} {surname}".Trim();

        public static ProcessRequestSummaryViewModel BuildSummary(
            Employee employee,
            string? designation,
            string? organizationUnitDisplay,
            DateTime? submittedOn = null,
            string? contextDateDisplay = null,
            string contextDateLabel = "Date")
        {
            return new ProcessRequestSummaryViewModel
            {
                EmployeeName = FormatEmployeeName(employee.FirstName, employee.Surname),
                Designation = designation ?? string.Empty,
                EmployeeIdNumber = employee.EmployeeIdNumber ?? string.Empty,
                OrganizationUnitDisplay = organizationUnitDisplay ?? string.Empty,
                ProfilePictureUrl = GetProfilePictureUrl(employee.ApplicationUser?.ProfilePicture),
                Initials = GetInitials(employee.FirstName, employee.Surname),
                SubmittedOn = submittedOn,
                ContextDateDisplay = contextDateDisplay,
                ContextDateLabel = contextDateLabel
            };
        }
    }
}