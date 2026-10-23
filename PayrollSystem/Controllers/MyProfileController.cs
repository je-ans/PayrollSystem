using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    [Authorize]
    public class MyProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MyProfileController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Get employee data with related information
            var employee = await _context.Employees
                .Include(e => e.PersonalInformation)
                .Include(e => e.Designations.Where(d => d.EffectiveTo == null))
                    .ThenInclude(d => d.Designation)
                .Include(e => e.Allowances.Where(a => a.IsActive && !a.IsDeleted))
                    .ThenInclude(a => a.Allowance)
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUser.Id && e.IsActive);

            if (employee == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var unitNames = await EmployeeOrganizationHelper.GetActiveUnitDisplayNamesAsync(
                _context, new[] { employee.Id });
            ViewBag.OrganizationUnitsDisplay = unitNames.GetValueOrDefault(employee.Id, "—");

            return View(employee);
        }

        public async Task<IActionResult> EditPersonalInfo()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Get employee personal information
            var employee = await _context.Employees
                .Include(e => e.PersonalInformation)
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUser.Id && e.IsActive);

            if (employee == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var viewModel = new PersonalInfoViewModel
            {
                EmployeeId = employee.Id,
                TelephoneNo = employee.TelephoneNo,
                MobileNo = employee.MobileNo,
                EmailAddress = employee.EmailAddress,
                EmergencyContactName = employee.PersonalInformation?.EmergencyContactName,
                EmergencyContactRelationship = employee.PersonalInformation?.EmergencyContactRelationship,
                EmergencyContactNumber = employee.PersonalInformation?.EmergencyContactNumber,
                Address = employee.PersonalInformation?.ResidentialAddress?.CompleteAddress
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPersonalInfo(PersonalInfoViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Get employee data
            var employee = await _context.Employees
                .Include(e => e.PersonalInformation)
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUser.Id && e.IsActive);

            if (employee == null)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Update employee contact information
            employee.TelephoneNo = model.TelephoneNo;
            employee.MobileNo = model.MobileNo;
            employee.EmailAddress = model.EmailAddress;
            employee.UpdatedById = currentUser.Id;
            employee.UpdatedOn = DateTime.Now;

            // Update personal information if exists
            if (employee.PersonalInformation != null)
            {
                employee.PersonalInformation.EmergencyContactName = model.EmergencyContactName;
                employee.PersonalInformation.EmergencyContactRelationship = model.EmergencyContactRelationship;
                employee.PersonalInformation.EmergencyContactNumber = model.EmergencyContactNumber;
                employee.PersonalInformation.UpdatedById = currentUser.Id;
                employee.PersonalInformation.UpdatedOn = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            TempData["success"] = "Personal information updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var result = await _userManager.ChangePasswordAsync(currentUser, model.CurrentPassword, model.NewPassword);

            if (result.Succeeded)
            {
                TempData["success"] = "Password changed successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }
    }

    public class PersonalInfoViewModel
    {
        public Guid EmployeeId { get; set; }

        [Display(Name = "Telephone Number")]
        public string? TelephoneNo { get; set; }

        [Display(Name = "Mobile Number")]
        public string? MobileNo { get; set; }

        [EmailAddress]
        [Display(Name = "Email Address")]
        public string? EmailAddress { get; set; }

        [Display(Name = "Emergency Contact Name")]
        public string? EmergencyContactName { get; set; }

        [Display(Name = "Emergency Contact Relationship")]
        public string? EmergencyContactRelationship { get; set; }

        [Display(Name = "Emergency Contact Number")]
        public string? EmergencyContactNumber { get; set; }

        [Display(Name = "Residential Address")]
        public string? Address { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }
}