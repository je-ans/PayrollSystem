using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class PersonalInformationViewModel
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }

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
        [Required(ErrorMessage = "Date of Birth is required")]
        public DateTime DateOfBirth { get; set; }

        [Display(Name = "Place of Birth")]
        public string? PlaceOfBirth { get; set; }

        [Display(Name = "Sex")]
        public string? Sex { get; set; }

        [Display(Name = "Civil Status")]
        public string? CivilStatus { get; set; }

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
        [Display(Name = "House/Block/Lot No. (Residential)")]
        public string? ResidentialHouseBlockLotNo { get; set; }
        [Display(Name = "Street (Residential)")]
        public string? ResidentialStreet { get; set; }
        [Display(Name = "Subdivision/Village (Residential)")]
        public string? ResidentialSubdivisionVillage { get; set; }
        [Display(Name = "Barangay (Residential)")]
        public string? ResidentialBarangay { get; set; }
        [Display(Name = "City/Municipality (Residential)")]
        public string? ResidentialCityMunicipality { get; set; }
        [Display(Name = "Province (Residential)")]
        public string? ResidentialProvince { get; set; }
        [Display(Name = "ZIP Code (Residential)")]
        public string? ResidentialZipCode { get; set; }

        // Permanent Address
        [Display(Name = "House/Block/Lot No. (Permanent)")]
        public string? PermanentHouseBlockLotNo { get; set; }
        [Display(Name = "Street (Permanent)")]
        public string? PermanentStreet { get; set; }
        [Display(Name = "Subdivision/Village (Permanent)")]
        public string? PermanentSubdivisionVillage { get; set; }
        [Display(Name = "Barangay (Permanent)")]
        public string? PermanentBarangay { get; set; }
        [Display(Name = "City/Municipality (Permanent)")]
        public string? PermanentCityMunicipality { get; set; }
        [Display(Name = "Province (Permanent)")]
        public string? PermanentProvince { get; set; }
        [Display(Name = "ZIP Code (Permanent)")]
        public string? PermanentZipCode { get; set; }

        [Display(Name = "Telephone No.")]
        public string? TelephoneNo { get; set; }

        [Display(Name = "Mobile No.")]
        public string? MobileNo { get; set; }

        [EmailAddress]
        [Display(Name = "Email Address")]
        public string? EmailAddress { get; set; }
    }
}
