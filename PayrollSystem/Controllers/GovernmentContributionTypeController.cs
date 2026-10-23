using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
    public class GovernmentContributionTypeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<GovernmentContributionTypeController> _logger;

        public GovernmentContributionTypeController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<GovernmentContributionTypeController> logger)
        {
            _context = context;
            _userManager = userManager;
            _auditHelper = auditHelper;
            _logger = logger;
        }

        //=======================================================Private Method=================================================================

        private async Task LogAuditTrailAsync(string action, object oldData, object newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "GovernmentContributionType", "GovernmentContributionTypes", oldData, newData);
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

        [Permission("GOVERNMENTCONTRIBUTIONTYPE_VIEW")]
        public async Task<IActionResult> Index()
        {
            var contributionTypes = await _context.GovernmentContributionTypes.ToListAsync();
            return View(contributionTypes);
        }

        [Permission("GOVERNMENTCONTRIBUTIONTYPE_VIEW")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var governmentContributionType = await _context.GovernmentContributionTypes
                .Select(g => new GovernmentContributionTypeViewModel
                {
                    Id = g.Id,
                    Code = g.Code,
                    Name = g.Name,
                    Description = g.Description,
                    IsMandatory = g.IsMandatory,
                    HasEmployerShare = g.HasEmployerShare,
                    CalculationMethod = g.CalculationMethod,
                    DisplayOrder = g.DisplayOrder,
                    IsActive = g.IsActive
                })
                .FirstOrDefaultAsync(m => m.Id == id);
            if (governmentContributionType == null)
            {
                return NotFound();
            }

            return PartialView("Partials/_ViewGovernmentContributionType", governmentContributionType);
        }

        [Permission("GOVERNMENTCONTRIBUTIONTYPE_VIEW")]
        public IActionResult Create()
        {
            return PartialView("Partials/_CreateGovernmentContributionType", new GovernmentContributionTypeViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("GOVERNMENTCONTRIBUTIONTYPE_VIEW")]
        public async Task<IActionResult> Create([Bind("Code,Name,Description,IsMandatory,HasEmployerShare,CalculationMethod,DisplayOrder,IsActive")] GovernmentContributionTypeViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            try
            {
                if (await _context.GovernmentContributionTypes.AnyAsync(g =>
                    g.Code == viewModel.Code && !g.IsDeleted))
                {
                    return Json(new { success = false, message = "Government Contribution Type with this code already exists." });
                }

                var governmentContributionType = new GovernmentContributionType
                {
                    Code = viewModel.Code,
                    Name = viewModel.Name,
                    Description = viewModel.Description,
                    IsMandatory = viewModel.IsMandatory,
                    HasEmployerShare = viewModel.HasEmployerShare,
                    CalculationMethod = viewModel.CalculationMethod,
                    DisplayOrder = viewModel.DisplayOrder,
                    IsActive = viewModel.IsActive,
                    CreatedById = _userManager.GetUserId(User),
                    CreatedOn = DateTime.UtcNow
                };
                _context.Add(governmentContributionType);
                var result = await _context.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("Failed to save Government Contribution Type: {Code}", viewModel.Code);
                    return Json(new { success = false, message = "Failed to create government contribution type." });
                }

                await LogAuditTrailAsync("Create Government Contribution Type", null, governmentContributionType);
                return Json(new { success = true, message = "Government Contribution Type created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Government Contribution Type: {Code}", viewModel.Code);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }

        [Permission("GOVERNMENTCONTRIBUTIONTYPE_VIEW")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var governmentContributionType = await _context.GovernmentContributionTypes
                .Select(g => new GovernmentContributionTypeViewModel
                {
                    Id = g.Id,
                    Code = g.Code,
                    Name = g.Name,
                    Description = g.Description,
                    IsMandatory = g.IsMandatory,
                    HasEmployerShare = g.HasEmployerShare,
                    CalculationMethod = g.CalculationMethod,
                    DisplayOrder = g.DisplayOrder,
                    IsActive = g.IsActive
                })
                .FirstOrDefaultAsync(m => m.Id == id);
            if (governmentContributionType == null)
            {
                return NotFound();
            }
            return PartialView("Partials/_EditGovernmentContributionType", governmentContributionType);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("GOVERNMENTCONTRIBUTIONTYPE_VIEW")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Code,Name,Description,IsMandatory,HasEmployerShare,CalculationMethod,DisplayOrder,IsActive")] GovernmentContributionTypeViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            var governmentContributionType = await _context.GovernmentContributionTypes.FindAsync(id);
            if (governmentContributionType == null)
            {
                _logger.LogError("Government Contribution Type with ID {Id} not found.", id);
                return Json(new { success = false, message = "Government Contribution Type not found." });
            }

            if (governmentContributionType.Code != viewModel.Code &&
                await _context.GovernmentContributionTypes.AnyAsync(g =>
                    g.Code == viewModel.Code && !g.IsDeleted && g.Id != id))
            {
                return Json(new { success = false, message = "Another government contribution type with the same code already exists." });
            }

            var oldData = JsonSerializer.Deserialize<GovernmentContributionType>(
                JsonSerializer.Serialize(governmentContributionType));

            governmentContributionType.Code = viewModel.Code;
            governmentContributionType.Name = viewModel.Name;
            governmentContributionType.Description = viewModel.Description;
            governmentContributionType.IsMandatory = viewModel.IsMandatory;
            governmentContributionType.HasEmployerShare = viewModel.HasEmployerShare;
            governmentContributionType.CalculationMethod = viewModel.CalculationMethod;
            governmentContributionType.DisplayOrder = viewModel.DisplayOrder;
            governmentContributionType.IsActive = viewModel.IsActive;
            governmentContributionType.UpdatedById = _userManager.GetUserId(User);
            governmentContributionType.UpdatedOn = DateTime.UtcNow;

            try
            {
                _context.Update(governmentContributionType);
                var result = await _context.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("No records updated for Government Contribution Type ID {Id}", id);
                    return Json(new { success = false, message = "No changes were made." });
                }

                await LogAuditTrailAsync("Update Government Contribution Type", oldData, governmentContributionType);
                return Json(new { success = true, message = "Government Contribution Type updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Government Contribution Type ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while updating." });
            }
        }

        [Permission("GOVERNMENTCONTRIBUTIONTYPE_VIEW")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var governmentContributionType = await _context.GovernmentContributionTypes
                .AsNoTracking()
                .Where(g => g.Id == id && !g.IsDeleted)
                .Select(g => new GovernmentContributionTypeViewModel
                {
                    Id = g.Id,
                    Code = g.Code,
                    Name = g.Name
                })
                .FirstOrDefaultAsync(m => m.Id == id);
            if (governmentContributionType == null)
            {
                _logger.LogWarning("Government Contribution Type with ID {Id} not found.", id);
                return NotFound();
            }

            // Check for associated contribution tables
            var hasTables = await _context.GovernmentContributionTables.AnyAsync(t => t.GovernmentContributionTypeId == governmentContributionType.Id && !t.IsDeleted);

            // If any restrictions exist, show warning
            if (hasTables)
            {
                ViewBag.HasTables = hasTables;
                return PartialView("Partials/_DeleteWarningGovernmentContributionType", governmentContributionType);
            }

            // If no restrictions, show normal delete confirmation
            return PartialView("Partials/_DeleteGovernmentContributionType", governmentContributionType);
        }


        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Permission("GOVERNMENTCONTRIBUTIONTYPE_VIEW")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var governmentContributionType = await _context.GovernmentContributionTypes.FindAsync(id);
            if (governmentContributionType == null || governmentContributionType.IsDeleted)
            {
                _logger.LogWarning("Government Contribution Type with ID {Id} not found or already deleted.", id);
                return Json(new { success = false, message = "Government Contribution Type not found or already deleted." });
            }

            // Check for associated contribution tables
            var hasTables = await _context.GovernmentContributionTables.AnyAsync(t => t.GovernmentContributionTypeId == governmentContributionType.Id && !t.IsDeleted);
            if (hasTables)
            {
                _logger.LogWarning("Cannot delete government contribution type with associated tables: {Code}", governmentContributionType.Code);
                return Json(new
                {
                    success = false,
                    message = "Cannot delete government contribution type because it has associated tables. Please delete associated tables first."
                });
            }

            var oldData = JsonSerializer.Deserialize<GovernmentContributionType>(
                JsonSerializer.Serialize(governmentContributionType));

            governmentContributionType.IsDeleted = true;
            governmentContributionType.UpdatedById = _userManager.GetUserId(User);
            governmentContributionType.UpdatedOn = DateTime.UtcNow;

            try
            {
                _context.GovernmentContributionTypes.Update(governmentContributionType);
                var result = await _context.SaveChangesAsync();

                if (result > 0)
                {
                    await LogAuditTrailAsync("Delete Government Contribution Type", oldData, null);
                    return Json(new { success = true, message = "Government Contribution Type deleted successfully." });
                }

                _logger.LogError("Failed to delete Government Contribution Type ID {Id}.", id);
                return Json(new { success = false, message = "Failed to delete government contribution type." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Government Contribution Type ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while deleting." });
            }
        }
    }
}
