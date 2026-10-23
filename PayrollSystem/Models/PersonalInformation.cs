using System;
using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;

namespace PayrollSystem.Models
{
    public class PersonalInformation : UserActivity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        [Display(Name = "Surname")]
        public string Surname { get; set; }

        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Display(Name = "Middle Name")]
        public string? MiddleName { get; set; }

        [Display(Name = "Name Extension (Jr., Sr.)")]
        public string? NameExtension { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date of Birth")]
        public DateTime DateOfBirth { get; set; }

        [Display(Name = "Place of Birth")]
        public string? PlaceOfBirth { get; set; }

        [Display(Name = "Sex")]
        public string? Sex { get; set; } // e.g., "Male", "Female"

        [Display(Name = "Civil Status")]
        public string? CivilStatus { get; set; } // e.g., "Single", "Married", "Widowed", "Separated"

        [Display(Name = "Citizenship")]
        public string? Citizenship { get; set; }

        [Display(Name = "Height (m)")]
        public double? Height { get; set; }

        [Display(Name = "Weight (kg)")]
        public double? Weight { get; set; }

        [Display(Name = "Blood Type")]
        public string? BloodType { get; set; }

        [Display(Name = "GSIS ID No.")]
        public string? GsisIdNo { get; set; }

        [Display(Name = "PAG-IBIG ID No.")]
        public string? PagIbigIdNo { get; set; }

        [Display(Name = "PhilHealth No.")]
        public string? PhilHealthNo { get; set; }

        [Display(Name = "SSS No.")]
        public string? SssNo { get; set; }

        [Display(Name = "TIN No.")]
        public string? TinNo { get; set; }

        [Display(Name = "Agency Employee No.")]
        public string? AgencyEmployeeNo { get; set; }

        // Residential Address
        public Guid? ResidentialAddressId { get; set; } // Foreign key
        public virtual Address? ResidentialAddress { get; set; }

        // Permanent Address
        public Guid? PermanentAddressId { get; set; } // Foreign key
        public virtual Address? PermanentAddress { get; set; }

        [Display(Name = "Telephone No.")]
        public string? TelephoneNo { get; set; }

        [Display(Name = "Mobile No.")]
        public string? MobileNo { get; set; }

        [EmailAddress]
        [Display(Name = "Email Address")]
        public string? EmailAddress { get; set; }

        // Emergency Contact Information
        [MaxLength(100)]
        [Display(Name = "Emergency Contact Name")]
        public string? EmergencyContactName { get; set; }

        [MaxLength(50)]
        [Display(Name = "Emergency Contact Relationship")]
        public string? EmergencyContactRelationship { get; set; }

        [MaxLength(20)]
        [Display(Name = "Emergency Contact Number")]
        public string? EmergencyContactNumber { get; set; }

        // Audit Fields
    }
}
