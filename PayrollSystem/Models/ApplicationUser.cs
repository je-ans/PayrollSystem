using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace PayrollSystem.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }

        public string FullName { get; set; }

        public string? CustomizeName { get; set; }

        public string RoleId { get; set; }

        [ForeignKey("RoleId")]
        public virtual ApplicationRole Role { get; set; }

        public virtual Employee? Employee { get; set; }


        [ForeignKey("Department")]
        public int? DepartmentId { get; set; }
        public virtual Department? Department { get; set; }

        [ForeignKey("Designation")]
        public int? DesignationId { get; set; }
        public virtual Designation? Designation { get; set; }

        public string? ProfilePicture { get; set; }

        public string? CustomizeDesignation { get; set; }

        public DateTime? PasswordChangedOn { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false;
        public string? CreatedById { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? UpdatedById { get; set; }
        public DateTime? UpdatedOn { get; set; }


    }
}
