using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace PayrollSystem.ViewModels
{
    public class UserViewModel
    {
        [Key]
        public string? Id { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [StringLength(100, ErrorMessage = "Email length cannot exceed 100 characters.")]
        public string Email { get; set; }


        [Required(ErrorMessage = "Username is required.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores.")]
        public string UserName { get; set; }


        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 50 characters.")]
        public string FirstName { get; set; }


        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 50 characters.")]
        public string LastName { get; set; }

        public string? FullName { get; set; }

        [Display(Name = "Custom Name")]
        public string? CustomizeName { get; set; }


        [Phone(ErrorMessage = "Invalid phone number.")]
        [RegularExpression(@"^\+?[1-9]\d{1,14}$", ErrorMessage = "Phone number must be in a valid international format.")]
        public string PhoneNumber { get; set; }


        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters.")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,100}$", ErrorMessage = "Password must contain at least one letter, one number, and one special character.")]
        public string? Password { get; set; }

        public string? RoleName { get; set; }

        [Required(ErrorMessage = "Role is required.")]
        public string? RoleId { get; set; }

        public string? DesignationName { get; set; }

        [Required(ErrorMessage = "Designation is required.")]
        public int? DesignationId { get; set; }


        [Display(Name = "Custom Designation")]
        public string? CustomizeDesignation { get; set; }


        public string? DepartmentName { get; set; }

        [Required(ErrorMessage = "Department is required.")]
        public int? DepartmentId { get; set; }

        public string? ProfilePicture { get; set; }

        public bool IsActive { get; set; } = true;

        public bool CanDelete { get; set; } = true;
    }
}
