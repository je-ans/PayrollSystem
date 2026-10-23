using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    public class DeductionTypeController : Controller
    {
        private readonly ApplicationDbContext _db;

        public DeductionTypeController(ApplicationDbContext db)
        {
            _db = db;
        }

        [Permission("DEDUCTIONMASTER_VIEW")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        [Permission("DEDUCTIONMASTER_VIEW")]
        public async Task<IActionResult> GetDeductionTypes()
        {
            var types = await _db.DeductionTypes
                .OrderBy(dt => dt.DisplayOrder)
                .ThenBy(dt => dt.Name)
                .ToListAsync();

            var data = types.Select(dt => new
            {
                dt.Id,
                dt.Code,
                dt.Name,
                dt.Description,
                dt.IsSystem,
                dt.DisplayOrder
            });

            return Json(new { success = true, data });
        }

        [HttpGet]
        [Permission("DEDUCTIONMASTER_VIEW")]
        public IActionResult Create()
        {
            return PartialView("Partials/_CreateDeductionType", new DeductionTypeViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("DEDUCTIONMASTER_VIEW")]
        public async Task<IActionResult> Create(DeductionTypeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Validation failed." });
            }

            if (await _db.DeductionTypes.AnyAsync(dt => dt.Code == model.Code))
            {
                return Json(new { success = false, message = "Code already exists." });
            }

            var entity = new DeductionType
            {
                Code = model.Code,
                Name = model.Name,
                Description = model.Description,
                IsSystem = model.IsSystem,
                DisplayOrder = model.DisplayOrder
            };

            _db.DeductionTypes.Add(entity);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Deduction type created successfully." });
        }

        [HttpGet]
        [Permission("DEDUCTIONMASTER_VIEW")]
        public async Task<IActionResult> Edit(int id)
        {
            var dt = await _db.DeductionTypes.FirstOrDefaultAsync(x => x.Id == id);
            if (dt == null) return NotFound();

            var vm = new DeductionTypeViewModel
            {
                Id = dt.Id,
                Code = dt.Code,
                Name = dt.Name,
                Description = dt.Description,
                IsSystem = dt.IsSystem,
                DisplayOrder = dt.DisplayOrder
            };

            return PartialView("Partials/_EditDeductionType", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("DEDUCTIONMASTER_VIEW")]
        public async Task<IActionResult> Edit(DeductionTypeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Validation failed." });
            }

            var dt = await _db.DeductionTypes.FirstOrDefaultAsync(x => x.Id == model.Id);
            if (dt == null) return Json(new { success = false, message = "Deduction type not found." });

            if (await _db.DeductionTypes.AnyAsync(x => x.Code == model.Code && x.Id != model.Id))
            {
                return Json(new { success = false, message = "Code already exists." });
            }

            dt.Code = model.Code;
            dt.Name = model.Name;
            dt.Description = model.Description;
            dt.IsSystem = model.IsSystem;
            dt.DisplayOrder = model.DisplayOrder;

            _db.DeductionTypes.Update(dt);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Deduction type updated successfully." });
        }

        [HttpGet]
        [Permission("DEDUCTIONMASTER_VIEW")]
        public async Task<IActionResult> Delete(int id)
        {
            var dt = await _db.DeductionTypes.FirstOrDefaultAsync(x => x.Id == id);
            if (dt == null) return NotFound();

            var vm = new DeductionTypeViewModel
            {
                Id = dt.Id,
                Code = dt.Code,
                Name = dt.Name
            };

            return PartialView("Partials/_DeleteDeductionType", vm);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Permission("DEDUCTIONMASTER_VIEW")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var dt = await _db.DeductionTypes.FirstOrDefaultAsync(x => x.Id == id);
            if (dt == null) return Json(new { success = false, message = "Deduction type not found." });

            var reservedCodes = new[] { "INCOME_TAX", "ABSENCE", "LATE_UNDERTIME", "LOAN" };
            if (dt.IsSystem || reservedCodes.Contains(dt.Code, StringComparer.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "This deduction type cannot be deleted. Core types (Income Tax, Absence, Late/Undertime, Loan) are required by the system." });
            }

            var usedInDeductions = await _db.Deductions
                .CountAsync(d => d.DeductionTypeId == id && !d.IsDeleted);
            if (usedInDeductions > 0)
            {
                return Json(new
                {
                    success = false,
                    message = $"Cannot delete this deduction type. It is used by {usedInDeductions} recurring deduction(s). Remove them first."
                });
            }

            var usedInEmployeeLoans = await _db.EmployeeLoans
                .CountAsync(el => el.DeductionTypeId == id && !el.IsDeleted);
            if (usedInEmployeeLoans > 0)
            {
                return Json(new
                {
                    success = false,
                    message = $"Cannot delete this deduction type. It is used by {usedInEmployeeLoans} employee loan(s). Remove or reassign the loans first."
                });
            }

            var usedInPayrollEntries = await _db.PayrollDeductions
                .CountAsync(d => d.DeductionTypeId == id);
            if (usedInPayrollEntries > 0)
            {
                return Json(new
                {
                    success = false,
                    message = $"Cannot delete this deduction type. It is used in {usedInPayrollEntries} payroll deduction record(s). Historical payroll data cannot be modified."
                });
            }

            _db.DeductionTypes.Remove(dt);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Deduction type deleted successfully." });
        }
    }
}

