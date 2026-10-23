using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AccountController.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IPasswordValidator<ApplicationUser> passwordValidator;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _db;

        public AccountController(SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IPasswordValidator<ApplicationUser> passwordValidator,
            IConfiguration configuration,
            ApplicationDbContext db)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
            this.passwordValidator = passwordValidator;
            _configuration = configuration;
            _db = db;
        }

        //------------------------------------------------------------For Login-------------------------------------------------------------------
        public async Task<IActionResult> Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                // Get the current user
                var user = await userManager.GetUserAsync(User);
                if (user != null)
                {
                    var roles = await userManager.GetRolesAsync(user);

                    if (EmployeePortalHelper.HasAdminConsoleAccess(User, roles))
                    {
                        return RedirectToAction("Index", "Home");
                    }

                    return RedirectToAction("Index", "EmployeeDashboard");
                }
            }

            // Get system information for login page
            var systemInfo = await _db.SystemInfos
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .FirstOrDefaultAsync();

            ViewBag.SystemInfo = systemInfo;
            ViewData["SystemInfo"] = systemInfo;
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> Index(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await signInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, false);

                if (result.Succeeded)
                {
                    // Get the logged-in user
                    var user = await userManager.FindByNameAsync(model.UserName);

                    // Get user roles
                    var roles = await userManager.GetRolesAsync(user);

                    // Check if user is Admin (using the exact role name from your database)
                    if (EmployeePortalHelper.HasAdminConsoleAccess(User, roles))
                    {
                        return RedirectToAction("Index", "Home");
                    }

                    return RedirectToAction("Index", "EmployeeDashboard");
                }
                else
                {
                    ModelState.AddModelError("", "Username or password is incorrect.");
                }
            }

            // Get system information for login page (even on validation errors)
            var systemInfo = await _db.SystemInfos
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .FirstOrDefaultAsync();

            ViewBag.SystemInfo = systemInfo;
            ViewData["SystemInfo"] = systemInfo;
            return View(model);
        }

        //------------------------------------------------------------For Logout-------------------------------------------------------------------
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();
            return RedirectToAction("Index", "Account");
        }


        //------------------------------------------------------------For AccessDenied-------------------------------------------------------------------

        public IActionResult AccessDenied()
        {
            return View();
        }


        //------------------------------------------------------------For UserProfile-------------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> UserProfile()
        {
            var user = await userManager.Users
                .Include(u => u.Designation)
                .FirstOrDefaultAsync(u => u.Id == User.FindFirstValue(ClaimTypes.NameIdentifier));

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new UserProfileAndChangePasswordViewModel
            {
                UserProfile = new UserProfileViewModel
                {
                    FirstName = user.FirstName,
                    MiddleName = user.MiddleName,
                    LastName = user.LastName,
                    Email = user.Email,
                    UserName = user.UserName,
                    PhoneNumber = user.PhoneNumber,
                    ProfilePicture = user.ProfilePicture,
                    DesignationName = user.Designation?.DesignationName ?? "No Designation"
                },
                ChangePassword = new ChangePasswordViewModel()
            };

            return View(model);
        }

        //------------------------------------------------------------For EditUserProfile-------------------------------------------------------------------


        [HttpPost]
        public async Task<IActionResult> EditUserProfile(UserProfileAndChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = ModelStateHelper.GetModelErrors(ModelState) });
            }

            var user = await userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            user.FirstName = model.UserProfile.FirstName;
            user.MiddleName = model.UserProfile.MiddleName;
            user.LastName = model.UserProfile.LastName;
            user.Email = model.UserProfile.Email;
            user.UserName = model.UserProfile.UserName;
            user.PhoneNumber = model.UserProfile.PhoneNumber;
            user.UpdatedOn = DateTime.UtcNow;
            user.UpdatedById = user.Id;

            var result = await userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                return Json(new { success = true, message = "Profile updated successfully." });
            }
            else
            {
                return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }
        }


        //------------------------------------------------------------For ChangePassword-------------------------------------------------------------------

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(UserProfileAndChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = ModelStateHelper.GetModelErrors(ModelState) });
            }

            var user = await userManager.GetUserAsync(User);

            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            // Validate the current password
            var passwordCheck = await userManager.CheckPasswordAsync(user, model.ChangePassword.CurrentPassword);
            if (!passwordCheck)
            {
                return Json(new { success = false, message = "Current password is incorrect." });
            }

            // Check if the new password and confirm password are the same
            if (model.ChangePassword.NewPassword != model.ChangePassword.ConfirmNewPassword)
            {
                return Json(new { success = false, message = "New password and confirm password do not match." });
            }

            // If a new password is provided, validate it
            if (!string.IsNullOrEmpty(model.ChangePassword.NewPassword))
            {
                var validationResult = await passwordValidator.ValidateAsync(userManager, user, model.ChangePassword.NewPassword);
                if (!validationResult.Succeeded)
                {
                    return Json(new { success = false, message = string.Join(", ", validationResult.Errors.Select(e => e.Description)) });
                }

                // Check if the new password is the same as the old password
                var oldPasswordHash = user.PasswordHash;
                var newPasswordHash = userManager.PasswordHasher.HashPassword(user, model.ChangePassword.NewPassword);
                if (newPasswordHash == oldPasswordHash)
                {
                    return Json(new { success = false, message = "New password cannot be the same as the old password." });
                }

                // Save the new password hash and other necessary changes
                user.PasswordHash = newPasswordHash;
                user.PasswordChangedOn = DateTime.UtcNow;
                user.UpdatedById = user.Id;

                var updateResult = await userManager.UpdateAsync(user);
                if (updateResult.Succeeded)
                {
                    return Json(new { success = true, message = "Password changed successfully!" });
                }
                else
                {
                    return Json(new { success = false, message = string.Join(", ", updateResult.Errors.Select(e => e.Description)) });
                }
            }

            return Json(new { success = false, message = "New password is required." });
        }

        //------------------------------------------------------------For UpdatingUserProfile-------------------------------------------------------------------

        [HttpPost]
        public async Task<IActionResult> UpdatingUserProfile(UserProfileAndChangePasswordViewModel model, string FormType)
        {
            if (FormType == "Profile")
            {
                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = ModelStateHelper.GetModelErrors(ModelState) });
                }

                var user = await userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound();
                }

                user.FirstName = model.UserProfile.FirstName;
                user.MiddleName = model.UserProfile.MiddleName;
                user.LastName = model.UserProfile.LastName;
                user.Email = model.UserProfile.Email;
                user.UserName = model.UserProfile.UserName;
                user.PhoneNumber = model.UserProfile.PhoneNumber;
                user.UpdatedOn = DateTime.UtcNow;
                user.UpdatedById = user.Id;

                var result = await userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    return Json(new { success = true, message = "Profile updated successfully." });
                }
                else
                {
                    return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
                }
            }
            else if (FormType == "Password")
            {
                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = ModelStateHelper.GetModelErrors(ModelState) });
                }

                var user = await userManager.GetUserAsync(User);

                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Validate the current password
                var passwordCheck = await userManager.CheckPasswordAsync(user, model.ChangePassword.CurrentPassword);
                if (!passwordCheck)
                {
                    return Json(new { success = false, message = "Current password is incorrect." });
                }

                // Check if the new password and confirm password are the same
                if (model.ChangePassword.NewPassword != model.ChangePassword.ConfirmNewPassword)
                {
                    return Json(new { success = false, message = "New password and confirm password do not match." });
                }

                // If a new password is provided, validate it
                if (!string.IsNullOrEmpty(model.ChangePassword.NewPassword))
                {
                    var validationResult = await passwordValidator.ValidateAsync(userManager, user, model.ChangePassword.NewPassword);
                    if (!validationResult.Succeeded)
                    {
                        return Json(new { success = false, message = string.Join(", ", validationResult.Errors.Select(e => e.Description)) });
                    }

                    // Check if the new password is the same as the old password
                    var oldPasswordHash = user.PasswordHash;
                    var newPasswordHash = userManager.PasswordHasher.HashPassword(user, model.ChangePassword.NewPassword);
                    if (newPasswordHash == oldPasswordHash)
                    {
                        return Json(new { success = false, message = "New password cannot be the same as the old password." });
                    }

                    // Save the new password hash and other necessary changes
                    user.PasswordHash = newPasswordHash;
                    user.PasswordChangedOn = DateTime.UtcNow;
                    user.UpdatedById = user.Id;

                    var updateResult = await userManager.UpdateAsync(user);
                    if (updateResult.Succeeded)
                    {
                        return Json(new { success = true, message = "Password changed successfully!" });
                    }
                    else
                    {
                        return Json(new { success = false, message = string.Join(", ", updateResult.Errors.Select(e => e.Description)) });
                    }
                }

                return Json(new { success = false, message = "New password is required." });
            }

            return Json(new { success = false, message = "Invalid form submission." });
        }


        //------------------------------------------------------------For UploadProfilePicture-------------------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> UploadProfilePicture(IFormFile profilePicture)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = ModelStateHelper.GetModelErrors(ModelState) });
            }

            if (profilePicture == null || profilePicture.Length == 0)
            {
                return Json(new { success = false, message = "Please select a valid image." });
            }

            var user = _db.Users.FirstOrDefault(u => u.UserName == User.Identity.Name);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            try
            {
                // Unique file name
                var fileName = $"{user.LastName}_{user.FirstName}_ProfilePicture_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(profilePicture.FileName)}";

                // Get upload path from configuration
                var uploadFolder = _configuration["FileSettings:UploadFolder"];
                if (string.IsNullOrEmpty(uploadFolder))
                {
                    return Json(new { success = false, message = "Upload folder not configured." });
                }

                var filePath = Path.Combine(uploadFolder, fileName);

                // Compress and save the image
                using (var stream = new MemoryStream())
                {
                    await profilePicture.CopyToAsync(stream);
                    stream.Seek(0, SeekOrigin.Begin);

                    using (var image = Image.Load(stream))
                    {
                        var encoder = new JpegEncoder
                        {
                            Quality = 75 // Adjust the quality (0-100) as needed
                        };

                        using (var output = new FileStream(filePath, FileMode.Create))
                        {
                            image.Save(output, encoder);
                        }
                    }
                }

                // Delete old profile picture (except default)
                if (!string.IsNullOrEmpty(user.ProfilePicture) && user.ProfilePicture != "default_user.png")
                {
                    var oldFilePath = Path.Combine(uploadFolder, user.ProfilePicture);
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                // Update user profile picture in database
                user.ProfilePicture = fileName;
                user.UpdatedOn = DateTime.UtcNow;
                user.UpdatedById = user.Id;
                _db.SaveChanges();

                return Json(new { success = true, message = "Profile picture updated successfully!", imageUrl = Url.Content("~/uploads/" + fileName) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error uploading file: " + ex.Message });
            }
        }
    }
}

