using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    [Authorize]
    public class GovernmentContributionTableController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<GovernmentContributionTableController> _logger;

        public GovernmentContributionTableController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<GovernmentContributionTableController> logger)
        {
            _context = context;
            _userManager = userManager;
            _auditHelper = auditHelper;
            _logger = logger;
        }

        // -- Private Helpers -------------------------------------------------------

        private async Task LogAuditTrailAsync(string action, object? oldData, object? newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "GovernmentContributionTable",
                    "GovernmentContributionTables", oldData, newData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error logging audit trail for {Action}. OldData: {OldData}, NewData: {NewData}",
                    action, oldData, newData);
            }
        }

        private JsonResult ValidationErrorResponse()
        {
            var errorMessages = ModelStateHelper.GetModelErrors(ModelState);
            _logger.LogWarning("Validation failed: {Errors}", string.Join("; ", errorMessages));
            return Json(new { success = false, message = string.Join("; ", errorMessages) });
        }

        private static GovernmentContributionTableViewModel ToViewModel(GovernmentContributionTable g) => new()
        {
            Id = g.Id,
            GovernmentContributionTypeId = g.GovernmentContributionTypeId,
            GovernmentContributionType = g.GovernmentContributionType,
            MinSalary = g.MinSalary,
            MaxSalary = g.MaxSalary,
            EmployeeShare = g.EmployeeShare,
            EmployerShare = g.EmployerShare,
            TotalContribution = g.TotalContribution,
            EmployeeSharePercentage = g.EmployeeSharePercentage,
            EmployerSharePercentage = g.EmployerSharePercentage,
            MaxEmployeeContribution = g.MaxEmployeeContribution,
            MaxEmployerContribution = g.MaxEmployerContribution,
            MinEmployeeContribution = g.MinEmployeeContribution,
            EffectiveStartDate = g.EffectiveStartDate,
            EffectiveEndDate = g.EffectiveEndDate,
            IsActive = g.IsActive,
            Notes = g.Notes
        };

        [Permission("GOVERNMENTCONTRIBUTIONTABLE_VIEW")]
        public async Task<IActionResult> Index(int? contributionTypeId)
        {
            IQueryable<GovernmentContributionTable> query = _context.GovernmentContributionTables
                .Include(g => g.GovernmentContributionType)
                .Where(g => !g.IsDeleted);

            if (contributionTypeId.HasValue)
            {
                query = query.Where(g => g.GovernmentContributionTypeId == contributionTypeId.Value);
                var typeName = (await _context.GovernmentContributionTypes.FindAsync(contributionTypeId.Value))?.Name;
                ViewData["Title"] = $"Government Contribution Tables for {typeName}";
            }
            else
            {
                ViewData["Title"] = "All Government Contribution Tables";
            }

            return View(await query.Select(g => ToViewModel(g)).ToListAsync());
        }

        [Permission("GOVERNMENTCONTRIBUTIONTABLE_VIEW")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var entity = await _context.GovernmentContributionTables
                .Include(g => g.GovernmentContributionType)
                .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

            if (entity == null) return NotFound();

            return PartialView("Partials/_ViewGovernmentContributionTable", ToViewModel(entity));
        }

        [Permission("GOVERNMENTCONTRIBUTIONTABLE_VIEW")]
        public IActionResult Create()
        {
            ViewData["GovernmentContributionTypeId"] =
                new SelectList(_context.GovernmentContributionTypes, "Id", "Name");
            return PartialView("Partials/_CreateGovernmentContributionTable",
                new GovernmentContributionTableViewModel());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("GOVERNMENTCONTRIBUTIONTABLE_VIEW")]
        public async Task<IActionResult> Create(
            [Bind("GovernmentContributionTypeId,MinSalary,MaxSalary," +
                  "EmployeeShare,EmployerShare,TotalContribution," +
                  "EmployeeSharePercentage,EmployerSharePercentage," +
                  "MaxEmployeeContribution,MaxEmployerContribution,MinEmployeeContribution," +
                  "EffectiveStartDate,EffectiveEndDate,IsActive,Notes")]
            GovernmentContributionTableViewModel viewModel)
        {
            if (!ModelState.IsValid)
                return ValidationErrorResponse();

            try
            {
                bool duplicate = await _context.GovernmentContributionTables.AnyAsync(g =>
                    g.GovernmentContributionTypeId == viewModel.GovernmentContributionTypeId &&
                    g.MinSalary == viewModel.MinSalary &&
                    g.EffectiveStartDate == viewModel.EffectiveStartDate &&
                    !g.IsDeleted);

                if (duplicate)
                    return Json(new
                    {
                        success = false,
                        message = "An entry with the same contribution type, salary floor, and effective date already exists."
                    });

                var entity = new GovernmentContributionTable
                {
                    GovernmentContributionTypeId = viewModel.GovernmentContributionTypeId,
                    MinSalary = viewModel.MinSalary,
                    MaxSalary = viewModel.MaxSalary,
                    EmployeeShare = viewModel.EmployeeShare,
                    EmployerShare = viewModel.EmployerShare,
                    TotalContribution = viewModel.TotalContribution,
                    EmployeeSharePercentage = viewModel.EmployeeSharePercentage,
                    EmployerSharePercentage = viewModel.EmployerSharePercentage,
                    MaxEmployeeContribution = viewModel.MaxEmployeeContribution,
                    MaxEmployerContribution = viewModel.MaxEmployerContribution,
                    MinEmployeeContribution = viewModel.MinEmployeeContribution,
                    EffectiveStartDate = viewModel.EffectiveStartDate,
                    EffectiveEndDate = viewModel.EffectiveEndDate,
                    IsActive = viewModel.IsActive,
                    Notes = viewModel.Notes,
                    CreatedById = _userManager.GetUserId(User),
                    CreatedOn = DateTime.UtcNow
                };

                _context.Add(entity);
                var result = await _context.SaveChangesAsync();

                if (result <= 0)
                    return Json(new { success = false, message = "Failed to create entry." });

                await LogAuditTrailAsync("Create Government Contribution Table", null, entity);
                return Json(new { success = true, message = "Government Contribution Table entry created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Government Contribution Table entry.");
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }


        [Permission("GOVERNMENTCONTRIBUTIONTABLE_VIEW")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var entity = await _context.GovernmentContributionTables
                .Include(g => g.GovernmentContributionType)
                .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

            if (entity == null) return NotFound();

            ViewData["GovernmentContributionTypeId"] =
                new SelectList(_context.GovernmentContributionTypes, "Id", "Name",
                    entity.GovernmentContributionTypeId);

            return PartialView("Partials/_EditGovernmentContributionTable", ToViewModel(entity));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("GOVERNMENTCONTRIBUTIONTABLE_VIEW")]
        public async Task<IActionResult> Edit(int id,
            [Bind("Id,GovernmentContributionTypeId,MinSalary,MaxSalary," +
                  "EmployeeShare,EmployerShare,TotalContribution," +
                  "EmployeeSharePercentage,EmployerSharePercentage," +
                  "MaxEmployeeContribution,MaxEmployerContribution,MinEmployeeContribution," +
                  "EffectiveStartDate,EffectiveEndDate,IsActive,Notes")]
            GovernmentContributionTableViewModel viewModel)
        {
            if (!ModelState.IsValid)
                return ValidationErrorResponse();

            var entity = await _context.GovernmentContributionTables.FindAsync(id);
            if (entity == null)
                return Json(new { success = false, message = "Entry not found." });

            bool duplicate = await _context.GovernmentContributionTables.AnyAsync(g =>
                g.GovernmentContributionTypeId == viewModel.GovernmentContributionTypeId &&
                g.MinSalary == viewModel.MinSalary &&
                g.EffectiveStartDate == viewModel.EffectiveStartDate &&
                g.Id != id && !g.IsDeleted);

            if (duplicate)
                return Json(new { success = false, message = "Another entry with the same type, salary floor, and effective date already exists." });

            var oldData = JsonSerializer.Deserialize<GovernmentContributionTable>(
                JsonSerializer.Serialize(entity));

            entity.GovernmentContributionTypeId = viewModel.GovernmentContributionTypeId;
            entity.MinSalary = viewModel.MinSalary;
            entity.MaxSalary = viewModel.MaxSalary;
            entity.EmployeeShare = viewModel.EmployeeShare;
            entity.EmployerShare = viewModel.EmployerShare;
            entity.TotalContribution = viewModel.TotalContribution;
            entity.EmployeeSharePercentage = viewModel.EmployeeSharePercentage;
            entity.EmployerSharePercentage = viewModel.EmployerSharePercentage;
            entity.MaxEmployeeContribution = viewModel.MaxEmployeeContribution;
            entity.MaxEmployerContribution = viewModel.MaxEmployerContribution;
            entity.MinEmployeeContribution = viewModel.MinEmployeeContribution;
            entity.EffectiveStartDate = viewModel.EffectiveStartDate;
            entity.EffectiveEndDate = viewModel.EffectiveEndDate;
            entity.IsActive = viewModel.IsActive;
            entity.Notes = viewModel.Notes;
            entity.UpdatedById = _userManager.GetUserId(User);
            entity.UpdatedOn = DateTime.UtcNow;

            try
            {
                _context.Update(entity);
                var result = await _context.SaveChangesAsync();

                if (result <= 0)
                    return Json(new { success = false, message = "No changes were made." });

                await LogAuditTrailAsync("Update Government Contribution Table", oldData, entity);
                return Json(new { success = true, message = "Entry updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Government Contribution Table entry ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while updating." });
            }
        }


        [Permission("GOVERNMENTCONTRIBUTIONTABLE_VIEW")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var entity = await _context.GovernmentContributionTables
                .Include(g => g.GovernmentContributionType)
                .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

            if (entity == null) return NotFound();

            return PartialView("Partials/_DeleteWarningGovernmentContributionTable", ToViewModel(entity));
        }


        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Permission("GOVERNMENTCONTRIBUTIONTABLE_VIEW")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var entity = await _context.GovernmentContributionTables.FindAsync(id);
            if (entity == null || entity.IsDeleted)
                return Json(new { success = false, message = "Entry not found or already deleted." });

            var oldData = JsonSerializer.Deserialize<GovernmentContributionTable>(
                JsonSerializer.Serialize(entity));

            entity.IsDeleted = true;
            entity.UpdatedById = _userManager.GetUserId(User);
            entity.UpdatedOn = DateTime.UtcNow;

            try
            {
                _context.Update(entity);
                var result = await _context.SaveChangesAsync();

                if (result > 0)
                {
                    await LogAuditTrailAsync("Delete Government Contribution Table", oldData, null);
                    return Json(new { success = true, message = "Entry deleted successfully." });
                }

                return Json(new { success = false, message = "Failed to delete entry." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Government Contribution Table entry ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while deleting." });
            }
        }
    }
}