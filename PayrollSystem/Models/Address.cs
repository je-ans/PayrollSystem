using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class Address : UserActivity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [MaxLength(255)]
        [Display(Name = "House/Block/Lot No.")]
        public string? HouseBlockLotNo { get; set; }

        [MaxLength(255)]
        [Display(Name = "Street")]
        public string? Street { get; set; }

        [MaxLength(255)]
        [Display(Name = "Subdivision/Village")]
        public string? SubdivisionVillage { get; set; }

        [MaxLength(255)]
        [Display(Name = "Barangay")]
        public string? Barangay { get; set; }

        [MaxLength(255)]
        [Display(Name = "City/Municipality")]
        public string? CityMunicipality { get; set; }

        [MaxLength(255)]
        [Display(Name = "Province")]
        public string? Province { get; set; }

        [MaxLength(20)]
        [Display(Name = "ZIP Code")]
        public string? ZipCode { get; set; }

        // Computed property for full address
        [NotMapped]
        public string CompleteAddress
        {
            get
            {
                var parts = new[]
                {
                    HouseBlockLotNo,
                    Street,
                    SubdivisionVillage,
                    Barangay,
                    CityMunicipality,
                    Province,
                    ZipCode
                }.Where(p => !string.IsNullOrWhiteSpace(p));

                return string.Join(", ", parts);
            }
        }
    }
}