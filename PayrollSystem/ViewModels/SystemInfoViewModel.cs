using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PayrollSystem.ViewModels
{
    public class SystemInfoViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "System Name is required")]
        [Display(Name = "System Name")]
        [StringLength(200, ErrorMessage = "System Name cannot exceed 200 characters")]
        public string SystemName { get; set; }

        [Required(ErrorMessage = "Short Name is required")]
        [Display(Name = "Short Name")]
        [StringLength(100, ErrorMessage = "Short Name cannot exceed 100 characters")]
        public string ShortName { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [Display(Name = "Description")]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; }

        [Display(Name = "Logo")]
        public IFormFile? LogoFile { get; set; }

        public string? LogoUrl { get; set; }
    }
}