using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    [Authorize]
    public class PayrollConfigurationController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<PayrollConfigurationController> _logger;
        private readonly IWebHostEnvironment _env;

        // Allowed logo MIME types
        private static readonly string[] AllowedLogoTypes = ["image/jpeg", "image/png", "image/webp", "image/gif"];
        private const long MaxLogoBytes = 2 * 1024 * 1024; // 2 MB

        public PayrollConfigurationController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<PayrollConfigurationController> logger,
            IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _auditHelper = auditHelper;
            _logger = logger;
            _env = env;
        }

        //=======================================================Private Methods=================================================================

        private async Task LogAuditTrailAsync(string action, object? oldData, object newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "PayrollConfiguration", "PayrollConfigurations", oldData, newData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit trail for {Action}.", action);
            }
        }

        private JsonResult ValidationErrorResponse()
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return Json(new { success = false, message = "Validation failed", errors });
        }

        // Saves an uploaded logo to /wwwroot/uploads/logos/ and returns the relative path.
        // Returns null if no file was uploaded. Returns the existing path on validation failure.
        private async Task<(string? path, string? error)> SaveLogoAsync(
            IFormFile? file, string? existingPath)
        {
            if (file == null || file.Length == 0)
                return (existingPath, null);

            if (!AllowedLogoTypes.Contains(file.ContentType.ToLower()))
                return (existingPath, "Logo must be a JPG, PNG, WEBP, or GIF image.");

            if (file.Length > MaxLogoBytes)
                return (existingPath, "Logo must be smaller than 2 MB.");

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "logos");
            Directory.CreateDirectory(uploadsFolder);

            var ext = Path.GetExtension(file.FileName);
            var fileName = $"company_logo_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            // Delete old logo file if it exists
            if (!string.IsNullOrWhiteSpace(existingPath))
            {
                var oldPath = Path.Combine(_env.WebRootPath, existingPath.TrimStart('/'));
                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
            }

            return ($"/uploads/logos/{fileName}", null);
        }

        private static void MapViewModelToConfig(PayrollConfigurationViewModel vm, PayrollConfiguration config)
        {
            config.Name = vm.Name;
            config.WorkingDaysPerMonth = vm.WorkingDaysPerMonth;
            config.WorkingHoursPerDay = vm.WorkingHoursPerDay;
            config.DefaultWorkStartTime = vm.DefaultWorkStartTime;
            config.DefaultWorkEndTime = vm.DefaultWorkEndTime;
            config.DefaultGracePeriodMinutes = vm.DefaultGracePeriodMinutes;
            config.DefaultBreakDurationMinutes = vm.DefaultBreakDurationMinutes;
            config.DefaultHalfDayHours = vm.DefaultHalfDayHours;
            config.DefaultAutomaticPairingWindowMinutes = vm.DefaultAutomaticPairingWindowMinutes;
            config.UseWeekendAsDayOff = vm.UseWeekendAsDayOff;
            config.NightDifferentialStartTime = vm.NightDifferentialStartTime;
            config.NightDifferentialEndTime = vm.NightDifferentialEndTime;
            config.MinimumOvertimeMinutes = vm.MinimumOvertimeMinutes;
            config.OvertimeRoundingRule = vm.OvertimeRoundingRule;
            config.RequireOvertimeApproval = vm.RequireOvertimeApproval;
            config.TaxTableYear = vm.TaxTableYear;
            config.SSSTableYear = vm.SSSTableYear;
            config.PhilHealthTableYear = vm.PhilHealthTableYear;
            config.PagIbigTableYear = vm.PagIbigTableYear;
            // Company Profile
            config.CompanyName = vm.CompanyName ?? "";
            config.CompanyShortName = vm.CompanyShortName;
            config.CompanyDescription = vm.CompanyDescription;
            config.CompanyAddress1 = vm.CompanyAddress1;
            config.CompanyAddress2 = vm.CompanyAddress2;
            config.CompanyCity = vm.CompanyCity;
            config.CompanyProvince = vm.CompanyProvince;
            config.CompanyZipCode = vm.CompanyZipCode;
            config.CompanyContactNumber = vm.CompanyContactNumber;
            config.CompanyEmail = vm.CompanyEmail;
            config.CompanyWebsite = vm.CompanyWebsite;
            // Government Numbers
            config.CompanyTIN = vm.CompanyTIN;
            config.CompanySSSNumber = vm.CompanySSSNumber;
            config.CompanyPhilHealthNumber = vm.CompanyPhilHealthNumber;
            config.CompanyPagIbigNumber = vm.CompanyPagIbigNumber;
            // Audit
            config.EffectiveDate = vm.EffectiveDate;
            config.IsActive = vm.IsActive;
        }

        private static PayrollConfigurationViewModel MapConfigToViewModel(PayrollConfiguration config)
        {
            return new PayrollConfigurationViewModel
            {
                Id = config.Id,
                Name = config.Name,
                WorkingDaysPerMonth = config.WorkingDaysPerMonth,
                WorkingHoursPerDay = config.WorkingHoursPerDay,
                DefaultWorkStartTime = config.DefaultWorkStartTime,
                DefaultWorkEndTime = config.DefaultWorkEndTime,
                DefaultGracePeriodMinutes = config.DefaultGracePeriodMinutes,
                DefaultBreakDurationMinutes = config.DefaultBreakDurationMinutes,
                DefaultHalfDayHours = config.DefaultHalfDayHours,
                DefaultAutomaticPairingWindowMinutes = config.DefaultAutomaticPairingWindowMinutes,
                UseWeekendAsDayOff = config.UseWeekendAsDayOff,
                NightDifferentialStartTime = config.NightDifferentialStartTime,
                NightDifferentialEndTime = config.NightDifferentialEndTime,
                MinimumOvertimeMinutes = config.MinimumOvertimeMinutes,
                OvertimeRoundingRule = config.OvertimeRoundingRule,
                RequireOvertimeApproval = config.RequireOvertimeApproval,
                TaxTableYear = config.TaxTableYear,
                SSSTableYear = config.SSSTableYear,
                PhilHealthTableYear = config.PhilHealthTableYear,
                PagIbigTableYear = config.PagIbigTableYear,
                CompanyName = config.CompanyName,
                CompanyShortName = config.CompanyShortName,
                CompanyDescription = config.CompanyDescription,
                CompanyLogoPath = config.CompanyLogoPath,
                CompanyAddress1 = config.CompanyAddress1,
                CompanyAddress2 = config.CompanyAddress2,
                CompanyCity = config.CompanyCity,
                CompanyProvince = config.CompanyProvince,
                CompanyZipCode = config.CompanyZipCode,
                CompanyContactNumber = config.CompanyContactNumber,
                CompanyEmail = config.CompanyEmail,
                CompanyWebsite = config.CompanyWebsite,
                CompanyTIN = config.CompanyTIN,
                CompanySSSNumber = config.CompanySSSNumber,
                CompanyPhilHealthNumber = config.CompanyPhilHealthNumber,
                CompanyPagIbigNumber = config.CompanyPagIbigNumber,
                EffectiveDate = config.EffectiveDate,
                IsActive = config.IsActive
            };
        }

        //=======================================================Index=================================================================

        [Permission("PAYROLLCONFIG_VIEW")]
        public async Task<IActionResult> Index()
        {
            ViewBag.ActiveLink = "PayrollConfiguration";

            var config = await _db.PayrollConfigurations
                .Where(pc => pc.IsActive)
                .OrderByDescending(pc => pc.EffectiveDate)
                .FirstOrDefaultAsync();

            if (config == null)
            {
                config = new PayrollConfiguration
                {
                    Name = "Default Payroll Configuration",
                    EffectiveDate = DateTime.Now,
                    IsActive = true
                };
                _db.PayrollConfigurations.Add(config);
                await _db.SaveChangesAsync();
            }

            return View(config);
        }

        //=======================================================Create=================================================================

        [HttpGet]
        [Permission("PAYROLLCONFIG_VIEW")]
        public IActionResult Create()
        {
            var viewModel = new PayrollConfigurationViewModel
            {
                EffectiveDate = DateTime.Now,
                IsActive = true
            };
            return PartialView("Partials/_CreatePayrollConfiguration", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("PAYROLLCONFIG_VIEW")]
        public async Task<IActionResult> Create(PayrollConfigurationViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    ? ValidationErrorResponse()
                    : PartialView("Partials/_CreatePayrollConfiguration", viewModel);
            }

            try
            {
                var (logoPath, logoError) = await SaveLogoAsync(viewModel.CompanyLogoFile, null);
                if (logoError != null)
                    ModelState.AddModelError(nameof(viewModel.CompanyLogoFile), logoError);

                if (!ModelState.IsValid)
                    return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                        ? ValidationErrorResponse()
                        : PartialView("Partials/_CreatePayrollConfiguration", viewModel);

                var config = new PayrollConfiguration();
                MapViewModelToConfig(viewModel, config);
                config.CompanyLogoPath = logoPath;

                var currentUser = await _userManager.GetUserAsync(User);
                config.CreatedById = currentUser?.Id;
                config.CreatedOn = DateTime.Now;

                _db.PayrollConfigurations.Add(config);
                await _db.SaveChangesAsync();
                await LogAuditTrailAsync("CREATE", null, config);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true, message = "Payroll configuration created successfully." });

                TempData["success"] = "Payroll configuration created successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payroll configuration");
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "An error occurred while creating the payroll configuration." });

                ModelState.AddModelError("", "An error occurred while creating the payroll configuration.");
            }

            return PartialView("Partials/_CreatePayrollConfiguration", viewModel);
        }

        //=======================================================Edit=================================================================

        [HttpGet]
        [Permission("PAYROLLCONFIG_VIEW")]
        public async Task<IActionResult> Edit(int id)
        {
            var config = await _db.PayrollConfigurations
                .FirstOrDefaultAsync(pc => pc.Id == id && !pc.IsDeleted);

            if (config == null) return NotFound();

            return PartialView("Partials/_EditPayrollConfiguration", MapConfigToViewModel(config));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("PAYROLLCONFIG_VIEW")]
        public async Task<IActionResult> Edit(PayrollConfigurationViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    ? ValidationErrorResponse()
                    : PartialView("Partials/_EditPayrollConfiguration", viewModel);
            }

            try
            {
                var config = await _db.PayrollConfigurations
                    .FirstOrDefaultAsync(pc => pc.Id == viewModel.Id && !pc.IsDeleted);

                if (config == null)
                    return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                        ? Json(new { success = false, message = "Payroll configuration not found." })
                        : NotFound();

                var oldData = JsonSerializer.Serialize(config);

                var (logoPath, logoError) = await SaveLogoAsync(viewModel.CompanyLogoFile, config.CompanyLogoPath);
                if (logoError != null)
                    ModelState.AddModelError(nameof(viewModel.CompanyLogoFile), logoError);

                if (!ModelState.IsValid)
                    return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                        ? ValidationErrorResponse()
                        : PartialView("Partials/_EditPayrollConfiguration", viewModel);

                MapViewModelToConfig(viewModel, config);
                config.CompanyLogoPath = logoPath;

                var currentUser = await _userManager.GetUserAsync(User);
                config.UpdatedById = currentUser?.Id;
                config.UpdatedOn = DateTime.Now;

                _db.PayrollConfigurations.Update(config);
                await _db.SaveChangesAsync();
                await LogAuditTrailAsync("UPDATE", oldData, config);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true, message = "Payroll configuration updated successfully." });

                TempData["success"] = "Payroll configuration updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payroll configuration");
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "An error occurred while updating the payroll configuration." });

                ModelState.AddModelError("", "An error occurred while updating the payroll configuration.");
            }

            return PartialView("Partials/_EditPayrollConfiguration", viewModel);
        }

        //=======================================================Details=================================================================

        [HttpGet]
        [Permission("PAYROLLCONFIG_VIEW")]
        public async Task<IActionResult> Details(int id)
        {
            var config = await _db.PayrollConfigurations
                .FirstOrDefaultAsync(pc => pc.Id == id && !pc.IsDeleted);

            if (config == null) return NotFound();

            return PartialView("Details", config);
        }

        //=======================================================AJAX Methods=================================================================

        [HttpGet]
        [Permission("PAYROLLCONFIG_VIEW")]
        public async Task<IActionResult> GetPayrollConfigurations()
        {
            try
            {
                var configs = await _db.PayrollConfigurations
                    .Where(pc => !pc.IsDeleted)
                    .OrderByDescending(pc => pc.EffectiveDate)
                    .Select(pc => new
                    {
                        pc.Id,
                        pc.Name,
                        pc.WorkingDaysPerMonth,
                        pc.WorkingHoursPerDay,
                        pc.TaxTableYear,
                        pc.SSSTableYear,
                        pc.PhilHealthTableYear,
                        pc.PagIbigTableYear,
                        pc.CompanyName,
                        pc.CompanyShortName,
                        pc.CompanyLogoPath,
                        pc.CompanyTIN,
                        pc.IsActive,
                        EffectiveDate = pc.EffectiveDate.ToString("yyyy-MM-dd"),
                        CreatedOn = pc.CreatedOn.HasValue ? pc.CreatedOn.Value.ToString("yyyy-MM-dd HH:mm") : null
                    })
                    .ToListAsync();

                return Json(new { success = true, data = configs });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payroll configurations");
                return Json(new { success = false, message = "Error retrieving payroll configurations" });
            }
        }
    }
}