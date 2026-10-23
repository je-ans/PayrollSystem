using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace PayrollSystem.Controllers
{
    [Authorize]
    public class SystemInfoController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<SystemInfoController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IMemoryCache _cache;

        public SystemInfoController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<SystemInfoController> logger,
            IWebHostEnvironment webHostEnvironment,
            IMemoryCache cache)
        {
            _userManager = userManager;
            _db = db;
            _auditHelper = auditHelper;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
            _cache = cache;
        }

        //=======================================================Private Methods=================================================================

        private async Task LogAuditTrailAsync(string action, object oldData, object newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "SystemInfo", "SystemInfos", oldData, newData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit trail for {Action}. OldData: {OldData}, NewData: {NewData}",
                    action, oldData, newData);
            }
        }

        private JsonResult ValidationErrorResponse()
        {
            var errorMessages = ModelStateHelper.GetModelErrors(ModelState);
            _logger.LogWarning("Validation failed: {Errors}", string.Join("; ", errorMessages));
            return Json(new { success = false, message = string.Join("; ", errorMessages) });
        }

        //=======================================================Get System Info=================================================================

        [Permission("SYSTEMINFO_VIEW")]
        public async Task<ActionResult> Index()
        {
            var systemInfo = await _db.SystemInfos
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .FirstOrDefaultAsync();

            return View(systemInfo);
        }

        //=======================================================Create System Info==============================================================

        [Permission("SYSTEMINFO_VIEW")]
        public ActionResult CreateSystemInfo()
        {
            return PartialView("Partials/CreateSystemInfo");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("SYSTEMINFO_VIEW")]
        public async Task<ActionResult> CreateSystemInfo(SystemInfoViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            try
            {
                var existingSystemInfo = await _db.SystemInfos.FirstOrDefaultAsync(s => !s.IsDeleted);
                if (existingSystemInfo != null)
                {
                    return Json(new { success = false, message = "System information already exists. Only one system info record is allowed." });
                }

                var systemInfo = new SystemInfo
                {
                    SystemName = model.SystemName,
                    ShortName = model.ShortName,
                    Description = model.Description,
                    CreatedById = _userManager.GetUserId(User),
                    CreatedOn = DateTime.UtcNow
                };

                // Handle logo upload
                if (model.LogoFile != null && model.LogoFile.Length > 0)
                {
                    try
                    {
                        var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "system_logos");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var safeSystemName = string.Join("", model.SystemName.Split(Path.GetInvalidFileNameChars()));
                        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
                        var ext = Path.GetExtension(model.LogoFile.FileName);
                        var baseFileName = $"{safeSystemName}_{datePart}";
                        var uniqueFileName = baseFileName + ext;
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        // Save original
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await model.LogoFile.CopyToAsync(fileStream);
                        }

                        // Generate resized icons
                        using (var image = SixLabors.ImageSharp.Image.Load(model.LogoFile.OpenReadStream()))
                        {
                            // 192x192
                            var file192 = Path.Combine(uploadsFolder, baseFileName + "_192x192.png");
                            image.Clone(ctx => ctx.Resize(192, 192)).Save(file192);

                            // 512x512
                            var file512 = Path.Combine(uploadsFolder, baseFileName + "_512x512.png");
                            image.Clone(ctx => ctx.Resize(512, 512)).Save(file512);
                        }

                        systemInfo.LogoUrl = "/uploads/system_logos/" + uniqueFileName;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error saving logo file for system: {Name}", model.SystemName);
                        return Json(new { success = false, message = "Error saving system logo. Please try again." });
                    }
                }

                _db.SystemInfos.Add(systemInfo);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("Failed to save system info: {SystemName}", model.SystemName);
                    return Json(new { success = false, message = "Failed to create system information." });
                }

                _cache.Remove("SystemInfo"); // Invalidate cache
                await LogAuditTrailAsync("Create System Info", null, systemInfo);
                return Json(new { success = true, message = "System information created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating system info: {SystemName}", model.SystemName);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }

        //=======================================================Edit System Info===================================================================

        [HttpGet]
        [Permission("SYSTEMINFO_VIEW")]
        public async Task<ActionResult> EditSystemInfo()
        {
            var systemInfo = await _db.SystemInfos
                .Where(s => !s.IsDeleted)
                .FirstOrDefaultAsync();

            if (systemInfo == null)
            {
                return NotFound();
            }

            var viewModel = new SystemInfoViewModel
            {
                Id = systemInfo.Id,
                SystemName = systemInfo.SystemName,
                ShortName = systemInfo.ShortName,
                Description = systemInfo.Description,
                LogoUrl = systemInfo.LogoUrl
            };

            return PartialView("Partials/EditSystemInfo", viewModel);
        }

        [HttpPost]
        [Permission("SYSTEMINFO_VIEW")]
        public async Task<ActionResult> EditSystemInfo(SystemInfoViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            var systemInfo = await _db.SystemInfos
                .Where(s => !s.IsDeleted)
                .FirstOrDefaultAsync();

            if (systemInfo == null)
            {
                _logger.LogError("System info not found.");
                return Json(new { success = false, message = "System information not found." });
            }

            var oldSystemInfoData = JsonSerializer.Deserialize<SystemInfo>(
                JsonSerializer.Serialize(systemInfo));

            // Manual mapping
            systemInfo.SystemName = model.SystemName;
            systemInfo.ShortName = model.ShortName;
            systemInfo.Description = model.Description;
            systemInfo.UpdatedById = _userManager.GetUserId(User);
            systemInfo.UpdatedOn = DateTime.UtcNow;

            // Handle logo upload
            if (model.LogoFile != null && model.LogoFile.Length > 0)
            {
                // Delete old logo and resized versions if they exist
                if (!string.IsNullOrEmpty(systemInfo.LogoUrl))
                {
                    var oldLogoPath = Path.Combine(_webHostEnvironment.WebRootPath, systemInfo.LogoUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                    var oldBase = oldLogoPath;
                    if (oldBase.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                        oldBase = oldBase.Substring(0, oldBase.Length - 4);
                    var old192 = oldBase + "_192x192.png";
                    var old512 = oldBase + "_512x512.png";
                    try
                    {
                        if (System.IO.File.Exists(oldLogoPath)) System.IO.File.Delete(oldLogoPath);
                        if (System.IO.File.Exists(old192)) System.IO.File.Delete(old192);
                        if (System.IO.File.Exists(old512)) System.IO.File.Delete(old512);
                    }
                    catch { /* Ignore errors */ }
                }
                try
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "system_logos");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var safeSystemName = string.Join("", model.SystemName.Split(Path.GetInvalidFileNameChars()));
                    var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
                    var ext = Path.GetExtension(model.LogoFile.FileName);
                    var baseFileName = $"{safeSystemName}_{datePart}";
                    var uniqueFileName = baseFileName + ext;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // Save original
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.LogoFile.CopyToAsync(fileStream);
                    }

                    // Generate resized icons
                    using (var image = SixLabors.ImageSharp.Image.Load(model.LogoFile.OpenReadStream()))
                    {
                        // 192x192
                        var file192 = Path.Combine(uploadsFolder, baseFileName + "_192x192.png");
                        image.Clone(ctx => ctx.Resize(192, 192)).Save(file192);

                        // 512x512
                        var file512 = Path.Combine(uploadsFolder, baseFileName + "_512x512.png");
                        image.Clone(ctx => ctx.Resize(512, 512)).Save(file512);
                    }

                    systemInfo.LogoUrl = "/uploads/system_logos/" + uniqueFileName;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving logo file for system: {Name}", model.SystemName);
                    return Json(new { success = false, message = "Error saving system logo. Please try again." });
                }
            }

            try
            {
                _db.SystemInfos.Update(systemInfo);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("No records updated for system info ID {Id}", systemInfo.Id);
                    return Json(new { success = false, message = "No changes were made." });
                }

                _cache.Remove("SystemInfo"); // Invalidate cache
                await LogAuditTrailAsync("Update System Info", oldSystemInfoData, systemInfo);
                return Json(new { success = true, message = "System information updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating System Info ID {Id}.", systemInfo.Id);
                return Json(new { success = false, message = "An error occurred while updating." });
            }
        }

        //=======================================================View System Info===================================================================

        [HttpGet]
        [Permission("SYSTEMINFO_VIEW")]
        public async Task<ActionResult> ViewSystemInfo()
        {
            var systemInfo = await _db.SystemInfos
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .FirstOrDefaultAsync();

            if (systemInfo == null)
            {
                _logger.LogWarning("System info not found.");
                return NotFound();
            }

            var viewModel = new SystemInfoViewModel
            {
                Id = systemInfo.Id,
                SystemName = systemInfo.SystemName,
                ShortName = systemInfo.ShortName,
                Description = systemInfo.Description,
                LogoUrl = systemInfo.LogoUrl
            };

            return PartialView("Partials/ViewSystemInfo", viewModel);
        }

        //=======================================================Delete System Info===================================================================

        [HttpGet]
        [Permission("SYSTEMINFO_VIEW")]
        public async Task<ActionResult> DeleteSystemInfo()
        {
            var systemInfo = await _db.SystemInfos
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .FirstOrDefaultAsync();

            if (systemInfo == null)
            {
                _logger.LogWarning("System info not found.");
                return NotFound();
            }

            var deleteViewModel = new SystemInfoViewModel
            {
                Id = systemInfo.Id,
                SystemName = systemInfo.SystemName,
                ShortName = systemInfo.ShortName,
                Description = systemInfo.Description,
                LogoUrl = systemInfo.LogoUrl
            };

            return PartialView("Partials/DeleteSystemInfo", deleteViewModel);
        }

        [HttpPost]
        [Permission("SYSTEMINFO_VIEW")]
        public async Task<ActionResult> DeleteConfirmed()
        {
            var systemInfo = await _db.SystemInfos
                .Where(s => !s.IsDeleted)
                .FirstOrDefaultAsync();

            if (systemInfo == null || systemInfo.IsDeleted)
            {
                _logger.LogWarning("System info not found or already deleted.");
                return Json(new { success = false, message = "System information not found or already deleted." });
            }

            var oldData = new SystemInfoViewModel
            {
                Id = systemInfo.Id,
                SystemName = systemInfo.SystemName,
                ShortName = systemInfo.ShortName,
                Description = systemInfo.Description,
                LogoUrl = systemInfo.LogoUrl
            };

            systemInfo.IsDeleted = true;
            systemInfo.UpdatedById = _userManager.GetUserId(User);
            systemInfo.UpdatedOn = DateTime.UtcNow;

            try
            {
                _db.SystemInfos.Update(systemInfo);
                var result = await _db.SaveChangesAsync();

                if (result > 0)
                {
                    _cache.Remove("SystemInfo"); // Invalidate cache
                    await LogAuditTrailAsync("Delete System Info", oldData, null);
                    return Json(new { success = true, message = "System information deleted successfully." });
                }

                _logger.LogError("Failed to delete system info ID {Id}.", systemInfo.Id);
                return Json(new { success = false, message = "Failed to delete system information." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting system info ID {Id}.", systemInfo.Id);
                return Json(new { success = false, message = "An error occurred while deleting." });
            }
        }

        //==========================================================================================================================
    }
}