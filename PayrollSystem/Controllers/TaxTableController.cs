using System.Text.Json;
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
    public class TaxTableController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<TaxTableController> _logger;

        public TaxTableController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<TaxTableController> logger)
        {
            _userManager = userManager;
            _db = db;
            _auditHelper = auditHelper;
            _logger = logger;
        }

        private async Task LogAuditTrailAsync(string action, object? oldData, object? newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "TaxTable", "TaxTables", oldData, newData);
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
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return Json(new { success = false, message = "Validation failed", errors });
        }

        [Permission("TAXTABLE_VIEW")]
        public IActionResult Index()
        {
            return View(new TaxTableViewModel());
        }

        [HttpGet]
        [Permission("TAXTABLE_VIEW")]
        public IActionResult Create()
        {
            return PartialView("Partials/_CreateTaxTable");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("TAXTABLE_VIEW")]
        public async Task<IActionResult> Create(TaxTableViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return ValidationErrorResponse();
                return View(viewModel);
            }

            try
            {
                var taxTable = new TaxTable
                {
                    Name = viewModel.Name,
                    Description = viewModel.Description,
                    TaxYear = viewModel.TaxYear,
                    BracketType = viewModel.BracketType,
                    IncomeBasis = viewModel.IncomeBasis,
                    MinIncome = viewModel.MinIncome,
                    MaxIncome = viewModel.MaxIncome,
                    TaxRate = viewModel.TaxRate,
                    FixedAmount = viewModel.FixedAmount,
                    BaseTaxAmount = viewModel.BaseTaxAmount,
                    BracketOrder = viewModel.BracketOrder,
                    EffectiveStartDate = viewModel.EffectiveStartDate,
                    EffectiveEndDate = viewModel.EffectiveEndDate,
                    IsActive = viewModel.IsActive,
                    Reference = viewModel.Reference,
                    CreatedById = _userManager.GetUserId(User),
                    CreatedOn = DateTime.Now
                };

                _db.TaxTables.Add(taxTable);
                await _db.SaveChangesAsync();

                await LogAuditTrailAsync("CREATE", null, taxTable);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true, message = "Tax table entry created successfully." });

                TempData["success"] = "Tax table entry created successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tax table entry.");
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "An error occurred while creating the tax table entry." });
                ModelState.AddModelError("", "An error occurred while creating the tax table entry.");
                return View(viewModel);
            }
        }

        [HttpGet]
        [Permission("TAXTABLE_VIEW")]
        public async Task<IActionResult> Edit(int id)
        {
            var taxTable = await _db.TaxTables
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (taxTable == null) return NotFound();

            var viewModel = new TaxTableViewModel
            {
                Id = taxTable.Id,
                Name = taxTable.Name,
                Description = taxTable.Description,
                TaxYear = taxTable.TaxYear,
                BracketType = taxTable.BracketType,
                IncomeBasis = taxTable.IncomeBasis,
                MinIncome = taxTable.MinIncome,
                MaxIncome = taxTable.MaxIncome,
                TaxRate = taxTable.TaxRate,
                FixedAmount = taxTable.FixedAmount,
                BaseTaxAmount = taxTable.BaseTaxAmount,
                BracketOrder = taxTable.BracketOrder,
                EffectiveStartDate = taxTable.EffectiveStartDate,
                EffectiveEndDate = taxTable.EffectiveEndDate,
                IsActive = taxTable.IsActive,
                Reference = taxTable.Reference
            };

            return PartialView("Partials/_EditTaxTable", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("TAXTABLE_VIEW")]
        public async Task<IActionResult> Edit(TaxTableViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return ValidationErrorResponse();
                return View(viewModel);
            }

            try
            {
                var existingTaxTable = await _db.TaxTables
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == viewModel.Id);

                if (existingTaxTable == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "Tax table entry not found." });
                    return NotFound();
                }

                var taxTable = new TaxTable
                {
                    Id = viewModel.Id,
                    Name = viewModel.Name,
                    Description = viewModel.Description,
                    TaxYear = viewModel.TaxYear,
                    BracketType = viewModel.BracketType,
                    IncomeBasis = viewModel.IncomeBasis,
                    MinIncome = viewModel.MinIncome,
                    MaxIncome = viewModel.MaxIncome,
                    TaxRate = viewModel.TaxRate,
                    FixedAmount = viewModel.FixedAmount,
                    BaseTaxAmount = viewModel.BaseTaxAmount,
                    BracketOrder = viewModel.BracketOrder,
                    EffectiveStartDate = viewModel.EffectiveStartDate,
                    EffectiveEndDate = viewModel.EffectiveEndDate,
                    IsActive = viewModel.IsActive,
                    Reference = viewModel.Reference,
                    CreatedById = existingTaxTable.CreatedById,
                    CreatedOn = existingTaxTable.CreatedOn,
                    UpdatedById = _userManager.GetUserId(User),
                    UpdatedOn = DateTime.Now
                };

                _db.TaxTables.Update(taxTable);
                await _db.SaveChangesAsync();

                await LogAuditTrailAsync("UPDATE", existingTaxTable, taxTable);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true, message = "Tax table entry updated successfully." });

                TempData["success"] = "Tax table entry updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tax table entry.");
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "An error occurred while updating the tax table entry." });
                ModelState.AddModelError("", "An error occurred while updating the tax table entry.");
                return View(viewModel);
            }
        }

        [HttpGet]
        [Permission("TAXTABLE_VIEW")]
        public async Task<IActionResult> Details(int id)
        {
            var taxTable = await _db.TaxTables
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (taxTable == null) return NotFound();

            var viewModel = new TaxTableViewModel
            {
                Id = taxTable.Id,
                Name = taxTable.Name,
                Description = taxTable.Description,
                TaxYear = taxTable.TaxYear,
                BracketType = taxTable.BracketType,
                IncomeBasis = taxTable.IncomeBasis,
                BracketOrder = taxTable.BracketOrder,
                MinIncome = taxTable.MinIncome,
                MaxIncome = taxTable.MaxIncome,
                TaxRate = taxTable.TaxRate,
                FixedAmount = taxTable.FixedAmount,
                BaseTaxAmount = taxTable.BaseTaxAmount,
                EffectiveStartDate = taxTable.EffectiveStartDate,
                Reference = taxTable.Reference,
                IsActive = taxTable.IsActive
            };

            return PartialView("Partials/_ViewTaxTable", viewModel);
        }

        [HttpGet]
        [Permission("TAXTABLE_VIEW")]
        public async Task<IActionResult> Delete(int id)
        {
            var taxTable = await _db.TaxTables
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (taxTable == null) return NotFound();

            var viewModel = new TaxTableViewModel
            {
                Id = taxTable.Id,
                Name = taxTable.Name,
                Description = taxTable.Description,
                TaxYear = taxTable.TaxYear,
                BracketType = taxTable.BracketType,
                IncomeBasis = taxTable.IncomeBasis,
                MinIncome = taxTable.MinIncome,
                MaxIncome = taxTable.MaxIncome,
                TaxRate = taxTable.TaxRate,
                FixedAmount = taxTable.FixedAmount,
                BaseTaxAmount = taxTable.BaseTaxAmount,
                BracketOrder = taxTable.BracketOrder,
                EffectiveStartDate = taxTable.EffectiveStartDate,
                EffectiveEndDate = taxTable.EffectiveEndDate,
                IsActive = taxTable.IsActive,
                Reference = taxTable.Reference
            };

            return PartialView("Partials/_DeleteTaxTable", viewModel);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Permission("TAXTABLE_VIEW")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var taxTable = await _db.TaxTables
                    .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

                if (taxTable == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "Tax table entry not found." });
                    return NotFound();
                }

                var snapshot = JsonSerializer.Serialize(taxTable);

                taxTable.UpdatedById = _userManager.GetUserId(User);
                taxTable.UpdatedOn = DateTime.Now;
                taxTable.IsDeleted = true;

                _db.TaxTables.Update(taxTable);
                await _db.SaveChangesAsync();

                await LogAuditTrailAsync("DELETE", snapshot, null);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true, message = "Tax table entry deleted successfully." });

                TempData["success"] = "Tax table entry deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tax table entry.");
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "An error occurred while deleting the tax table entry." });
                TempData["error"] = "An error occurred while deleting the tax table entry.";
                return RedirectToAction(nameof(Delete), new { id });
            }
        }

        [HttpGet]
        [Permission("TAXTABLE_VIEW")]
        public async Task<IActionResult> GetTaxTables()
        {
            try
            {
                var taxTables = await _db.TaxTables
                    .Where(t => !t.IsDeleted)
                    .OrderBy(t => t.BracketOrder)
                    .Select(t => new
                    {
                        t.Id,
                        t.Name,
                        BracketType = t.BracketType.ToString(),
                        IncomeBasis = t.IncomeBasis.ToString(),
                        IncomeRange = t.IncomeRange,
                        RateDisplay = t.RateDisplay,
                        t.BracketOrder,
                        t.IsActive,
                        EffectiveStartDate = t.EffectiveStartDate.ToString("yyyy-MM-dd"),
                        t.IsCurrent
                    })
                    .ToListAsync();

                return Json(new { success = true, data = taxTables });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tax tables.");
                return Json(new { success = false, message = "Error retrieving tax tables." });
            }
        }
    }
}