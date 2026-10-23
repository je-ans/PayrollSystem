using Microsoft.AspNetCore.Identity;

namespace PayrollSystem.Models
{
    public class ApplicationRole : IdentityRole<string>
    {
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false;
        public string Remarks { get; set; }
    }
}