using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class EmployeeViewModel
    {
        public Guid Id { get; set; }

        [Display(Name = "Employee ID")]
        public string EmployeeIdNumber { get; set; }

        [Display(Name = "Surname")]
        [Required(ErrorMessage = "Surname is required")]
        public string Surname { get; set; }

        [Display(Name = "First Name")]
        [Required(ErrorMessage = "First Name is required")]
        public string FirstName { get; set; }

        [Display(Name = "Middle Name")]
        public string? MiddleName { get; set; }

        [Display(Name = "Name Extension (Jr., Sr.)")]
        public string? NameExtension { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date of Birth")]
        public DateOnly? DateOfBirth { get; set; }

        [Display(Name = "Sex")]
        public string Sex { get; set; }

        [Display(Name = "Telephone No.")]
        public string? TelephoneNo { get; set; }

        [Display(Name = "Mobile No.")]
        public string? MobileNo { get; set; }

        [EmailAddress]
        [Display(Name = "Email Address")]
        public string EmailAddress { get; set; }

        [Required]
        [Display(Name = "Employment Basis")]
        public EmploymentBasis EmploymentBasis { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date Hired")]
        public DateOnly? DateHired { get; set; }

        [Display(Name = "Designation")]
        public int DesignationId { get; set; }

        public string? DesignationName { get; set; }

        [Display(Name = "Organization Units")]
        public List<int> OrganizationStructureIds { get; set; } = new();

        [Display(Name = "Primary Schedule Unit")]
        public int? PrimaryOrganizationStructureId { get; set; }

        public string? OrganizationUnitsDisplay { get; set; }

        [Display(Name = "Default Rate Type")]
        public int? DefaultRateTypeId { get; set; }

        public bool IsActive { get; set; } = true;

        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {MiddleName} {Surname}".Trim();

        // Navigation properties for dropdowns (not mapped to database)
        public IEnumerable<Designation>? DesignationOptions { get; set; }
        public List<ParentOptionViewModel> OrganizationUnits { get; set; } = new();
        public IEnumerable<RateType>? RateTypes { get; set; }

        // Active org placements (unit + role), from Staff Structure
        public List<string> OrganizationPlacements { get; set; } = new();

        // --- Login account (when creating a new employee so they can sign in) ---
        [Display(Name = "Username")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores.")]
        public string UserName { get; set; }

        [Display(Name = "Email (for login)")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [StringLength(100)]
        public string? LoginEmail { get; set; }

        [Display(Name = "Password")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,100}$", ErrorMessage = "Password must contain at least one letter, one number, and one special character (@$!%*?&).")]
        public string? Password { get; set; }

        [Display(Name = "Confirm Password")]
        [Compare(nameof(Password), ErrorMessage = "Password and confirmation do not match.")]
        public string? ConfirmPassword { get; set; }

        [Display(Name = "System Role")]
        public string RoleId { get; set; }

        [Display(Name = "Biometric ID")]
        [MaxLength(20, ErrorMessage = "Biometric ID must not exceed 20 characters.")]
        public string? BiometricId { get; set; }
    }
}
