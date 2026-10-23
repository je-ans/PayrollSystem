using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Services;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    public class PayrollPeriodController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<PayrollPeriodController> _logger;
        private readonly IPayrollService _payrollService;
        private readonly IPayrollTimeCardFromDtrService _timeCardFromDtrService;
        private readonly IOvertimeRequestService _overtimeRequestService;

        public PayrollPeriodController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<PayrollPeriodController> logger,
            IPayrollService payrollService,
            IPayrollTimeCardFromDtrService timeCardFromDtrService,
            IOvertimeRequestService overtimeRequestService)
        {
            _userManager = userManager;
            _db = db;
            _auditHelper = auditHelper;
            _logger = logger;
            _payrollService = payrollService;
            _timeCardFromDtrService = timeCardFromDtrService;
            _overtimeRequestService = overtimeRequestService;
        }

        //=======================================================Private Methods=================================================================

        private async Task LogAuditTrailAsync(string action, object oldData, object newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "PayrollPeriod", "PayrollPeriods", oldData, newData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit trail for {Action}. OldData: {OldData}, NewData: {NewData}",
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

        private async Task<List<DayTypeSelectItem>> GetActiveDayTypesAsync()
        {
            return await _db.DayTypes
                .Where(dt => dt.IsActive)
                .OrderBy(dt => dt.Name)
                .Select(dt => new DayTypeSelectItem { Id = dt.Id, Name = dt.Name })
                .ToListAsync();
        }

        private static List<TimeCardDetailRowViewModel> BuildDetailRows(
            PayrollPeriodTimeCardEntry? tc,
            List<DayTypeSelectItem> dayTypes)
        {
            return dayTypes.Select(dt =>
            {
                var detail = tc?.Details.FirstOrDefault(d => d.DayTypeId == dt.Id);
                return new TimeCardDetailRowViewModel
                {
                    DayTypeId = dt.Id,
                    DayTypeName = dt.Name,
                    RegularHours = detail?.RegularHours ?? 0,
                    OvertimeHours = detail?.OvertimeHours ?? 0,
                    NightDifferentialHours = detail?.NightDifferentialHours ?? 0,
                    NightDifferentialOvertimeHours = detail?.NightDifferentialOvertimeHours ?? 0
                };
            }).ToList();
        }

        //=======================================================Public Methods=================================================================

        [Permission("PAYROLL_PERIOD_VIEW")]
        public IActionResult Index()
        {
            var viewModel = new PayrollPeriodViewModel();
            return View(viewModel);
        }

        [HttpGet]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> ViewPayrollPeriod(int id)
        {
            var payrollPeriod = await _db.PayrollPeriods
                .FirstOrDefaultAsync(pp => pp.Id == id && !pp.IsDeleted);

            if (payrollPeriod == null)
                return NotFound();

            return PartialView("Partials/_ViewPayrollPeriod", payrollPeriod);
        }

        [HttpGet]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public IActionResult Create()
        {
            return PartialView("Partials/_CreatePayrollPeriod", new PayrollPeriodViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> Create(PayrollPeriodViewModel viewModel)
        {
            if (string.IsNullOrWhiteSpace(viewModel.Name))
                ModelState.AddModelError("Name", "The Period Name field is required.");

            if (ModelState.IsValid)
            {
                try
                {
                    var payrollPeriod = new PayrollPeriod
                    {
                        Name = viewModel.Name,
                        Description = viewModel.Description,
                        PeriodStartDate = viewModel.PeriodStartDate ?? default,
                        PeriodEndDate = viewModel.PeriodEndDate ?? default,
                        PayDate = viewModel.PayDate ?? default,
                        IsActive = viewModel.IsActive,
                        IsLocked = viewModel.IsLocked
                    };

                    var currentUser = await _userManager.GetUserAsync(User);
                    payrollPeriod.CreatedById = currentUser?.Id;
                    payrollPeriod.CreatedOn = DateTime.Now;

                    _db.PayrollPeriods.Add(payrollPeriod);
                    await _db.SaveChangesAsync();
                    await LogAuditTrailAsync("CREATE", null, payrollPeriod);

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = true, message = "Payroll period created successfully." });

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating payroll period");
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "An error occurred while creating the payroll period." });
                    ModelState.AddModelError("", "An error occurred while creating the payroll period.");
                }
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return ValidationErrorResponse();

            return View(viewModel);
        }

        [HttpGet]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> Edit(int id)
        {
            var payrollPeriod = await _db.PayrollPeriods
                .FirstOrDefaultAsync(pp => pp.Id == id && !pp.IsDeleted);

            if (payrollPeriod == null)
                return NotFound();

            var viewModel = new PayrollPeriodViewModel
            {
                Id = payrollPeriod.Id,
                Name = payrollPeriod.Name,
                Description = payrollPeriod.Description,
                PeriodStartDate = payrollPeriod.PeriodStartDate,
                PeriodEndDate = payrollPeriod.PeriodEndDate,
                PayDate = payrollPeriod.PayDate,
                IsActive = payrollPeriod.IsActive,
                IsLocked = payrollPeriod.IsLocked
            };

            return PartialView("Partials/_EditPayrollPeriod", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> Edit(PayrollPeriodViewModel viewModel)
        {
            if (string.IsNullOrWhiteSpace(viewModel.Name))
                ModelState.AddModelError("Name", "The Period Name field is required.");

            if (ModelState.IsValid)
            {
                try
                {
                    var payrollPeriod = await _db.PayrollPeriods
                        .FirstOrDefaultAsync(pp => pp.Id == viewModel.Id && !pp.IsDeleted);

                    if (payrollPeriod == null)
                    {
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                            return Json(new { success = false, message = "Payroll period not found." });
                        return NotFound();
                    }

                    payrollPeriod.Name = viewModel.Name;
                    payrollPeriod.Description = viewModel.Description;
                    payrollPeriod.PeriodStartDate = viewModel.PeriodStartDate ?? default;
                    payrollPeriod.PeriodEndDate = viewModel.PeriodEndDate ?? default;
                    payrollPeriod.PayDate = viewModel.PayDate ?? default;
                    payrollPeriod.IsActive = viewModel.IsActive;
                    payrollPeriod.IsLocked = viewModel.IsLocked;

                    var currentUser = await _userManager.GetUserAsync(User);
                    payrollPeriod.UpdatedById = currentUser?.Id;
                    payrollPeriod.UpdatedOn = DateTime.Now;

                    _db.PayrollPeriods.Update(payrollPeriod);
                    await _db.SaveChangesAsync();
                    await LogAuditTrailAsync("UPDATE", null, payrollPeriod);

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = true, message = "Payroll period updated successfully." });

                    TempData["success"] = "Payroll period updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating payroll period");
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "An error occurred while updating the payroll period." });
                    ModelState.AddModelError("", "An error occurred while updating the payroll period.");
                }
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return ValidationErrorResponse();

            return View(viewModel);
        }

        [HttpGet]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> Delete(int id)
        {
            var payrollPeriod = await _db.PayrollPeriods
                .FirstOrDefaultAsync(pp => pp.Id == id && !pp.IsDeleted);

            if (payrollPeriod == null)
                return NotFound();

            var viewModel = new PayrollPeriodViewModel
            {
                Id = payrollPeriod.Id,
                Name = payrollPeriod.Name,
                Description = payrollPeriod.Description,
                PeriodStartDate = payrollPeriod.PeriodStartDate,
                PeriodEndDate = payrollPeriod.PeriodEndDate,
                PayDate = payrollPeriod.PayDate,
                IsActive = payrollPeriod.IsActive,
                IsLocked = payrollPeriod.IsLocked
            };

            return PartialView("Partials/_DeletePayrollPeriod", viewModel);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var payrollPeriod = await _db.PayrollPeriods
                    .FirstOrDefaultAsync(pp => pp.Id == id && !pp.IsDeleted);

                if (payrollPeriod == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "Payroll period not found." });
                    return NotFound();
                }

                var existingPeriod = JsonSerializer.Serialize(payrollPeriod);

                var currentUser = await _userManager.GetUserAsync(User);
                payrollPeriod.UpdatedById = currentUser?.Id;
                payrollPeriod.UpdatedOn = DateTime.Now;
                payrollPeriod.IsDeleted = true;

                _db.PayrollPeriods.Update(payrollPeriod);
                await _db.SaveChangesAsync();
                await LogAuditTrailAsync("DELETE", existingPeriod, null);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true, message = "Payroll period deleted successfully." });

                TempData["success"] = "Payroll period deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting payroll period");
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "An error occurred while deleting the payroll period." });
                TempData["error"] = "An error occurred while deleting the payroll period.";
                return RedirectToAction(nameof(Delete), new { id });
            }
        }

        //=======================================================AJAX Methods=================================================================

        [HttpGet]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> GetPayrollPeriods()
        {
            try
            {
                var payrollPeriods = await _db.PayrollPeriods
                    .Where(pp => !pp.IsDeleted)
                    .OrderByDescending(pp => pp.PeriodStartDate)
                    .Select(pp => new
                    {
                        pp.Id,
                        pp.Name,
                        PeriodStartDate = pp.PeriodStartDate.ToString("yyyy-MM-dd"),
                        PeriodEndDate = pp.PeriodEndDate.ToString("yyyy-MM-dd"),
                        PayDate = pp.PayDate.ToString("yyyy-MM-dd"),
                        DaysInPeriod = pp.DaysInPeriod,
                        DetectedFrequency = pp.DaysInPeriod == 7 ? "Weekly" :
                                            pp.DaysInPeriod == 14 ? "Bi-Weekly" :
                                            pp.DaysInPeriod >= 15 && pp.DaysInPeriod <= 16 ? "Semi-Monthly" :
                                            pp.DaysInPeriod >= 28 && pp.DaysInPeriod <= 31 ? "Monthly" :
                                            "Custom",
                        IsFirstHalf = pp.PeriodStartDate.Day <= 15 && pp.PeriodEndDate.Day <= 15,
                        IsSecondHalf = pp.PeriodStartDate.Day >= 16 && pp.PeriodEndDate.Day >= 16,
                        pp.IsActive,
                        pp.IsLocked,
                        HasCompletedPayrollRun = _db.PayrollRuns
                            .Any(pr => pr.PayrollPeriodId == pp.Id && pr.Status == PayrollStatus.Completed)
                    })
                    .ToListAsync();

                return Json(new { success = true, data = payrollPeriods });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payroll periods");
                return Json(new { success = false, message = "Error retrieving payroll periods" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> RunPayroll(int id)
        {
            try
            {
                var payrollPeriodExists = await _db.PayrollPeriods
                    .AnyAsync(pp => pp.Id == id && !pp.IsDeleted && !pp.IsLocked);

                if (!payrollPeriodExists)
                    return Json(new { success = false, message = "Payroll period not found or is locked." });

                var hasAssignedEmployees = await _db.PayrollPeriodEmployees
                    .AnyAsync(ppe => ppe.PayrollPeriodId == id);

                if (!hasAssignedEmployees)
                    return Json(new { success = false, message = "No employees assigned to this payroll period. Please assign employees before running payroll." });

                var assignedEmployeeIds = await _db.PayrollPeriodEmployees
                    .Where(ppe => ppe.PayrollPeriodId == id)
                    .Select(ppe => ppe.EmployeeId)
                    .ToListAsync();

                var timeCardEmployeeIds = await _db.PayrollPeriodTimeCards
                    .Where(tc => tc.PayrollPeriodId == id)
                    .Select(tc => tc.EmployeeId)
                    .ToListAsync();

                var missingTimeCardIds = assignedEmployeeIds
                    .Where(eid => !timeCardEmployeeIds.Contains(eid))
                    .ToList();

                if (missingTimeCardIds.Any())
                {
                    var missingNames = await _db.Employees
                        .Where(e => missingTimeCardIds.Contains(e.Id))
                        .Select(e => e.FullName)
                        .ToListAsync();

                    return Json(new
                    {
                        success = false,
                        message = "The following employees are missing time card entries: " +
                                  string.Join(", ", missingNames) +
                                  ". Please complete all time cards before running payroll."
                    });
                }

                var completedRunExists = await _db.PayrollRuns
                    .AnyAsync(pr => pr.PayrollPeriodId == id && pr.Status == PayrollStatus.Completed);

                if (completedRunExists)
                    return Json(new { success = false, message = "A completed payroll run already exists for this period. Delete it first to re-run." });

                var existingPayrollRun = await _db.PayrollRuns
                    .FirstOrDefaultAsync(pr => pr.PayrollPeriodId == id && pr.Status != PayrollStatus.Completed);

                if (existingPayrollRun != null)
                {
                    _logger.LogInformation(
                        "Deleting existing non-completed payroll run {RunId} for period {PeriodId}.",
                        existingPayrollRun.Id, id);

                    var existingEntries = await _db.PayrollEntries
                        .Where(pe => pe.PayrollRunId == existingPayrollRun.Id)
                        .Include(pe => pe.GovernmentContributions)
                        .ToListAsync();

                    foreach (var entry in existingEntries)
                    {
                        if (entry.GovernmentContributions.Any())
                            _db.Set<PayrollEntryGovernmentContribution>()
                               .RemoveRange(entry.GovernmentContributions);
                    }

                    if (existingEntries.Any())
                        _db.PayrollEntries.RemoveRange(existingEntries);

                    _db.PayrollRuns.Remove(existingPayrollRun);
                    await _db.SaveChangesAsync();
                }

                var payrollPeriod = await _db.PayrollPeriods
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pp => pp.Id == id && !pp.IsDeleted && !pp.IsLocked);

                if (payrollPeriod == null)
                    return Json(new { success = false, message = "Payroll period not found or is locked." });

                var currentUser = await _userManager.GetUserAsync(User);

                var payrollRun = new PayrollRun
                {
                    PayrollPeriodId = payrollPeriod.Id,
                    Name = $"Payroll Run for {payrollPeriod.Name}",
                    Description = $"Auto-generated payroll run for {payrollPeriod.Name}",
                    Status = PayrollStatus.Draft,
                    CreatedById = currentUser?.Id,
                    CreatedOn = DateTime.Now
                };

                _db.PayrollRuns.Add(payrollRun);
                await _db.SaveChangesAsync();

                // Run calculations
                await _payrollService.ProcessPayrollCalculationsAsync(payrollRun);

                // Update PayrollRun summary totals
                var processedEntries = await _db.PayrollEntries
                    .Where(pe => pe.PayrollRunId == payrollRun.Id)
                    .Include(pe => pe.GovernmentContributions)
                    .ToListAsync();

                payrollRun.TotalEmployees = processedEntries.Count;
                payrollRun.TotalGrossPay = processedEntries.Sum(pe => pe.GrossPay);
                payrollRun.TotalBenefits = processedEntries.Sum(pe => pe.TotalBenefits);

                payrollRun.TotalDeductions = processedEntries.Sum(pe =>
                    pe.TotalDeductions +
                    pe.GovernmentContributions.Sum(gc => gc.EmployeeShare));

                payrollRun.TotalNetPay = processedEntries.Sum(pe => pe.NetPay);
                payrollRun.Status = PayrollStatus.Completed;
                payrollRun.ProcessedByUserId = currentUser?.Id;
                payrollRun.ProcessedDate = DateTime.Now;
                payrollRun.UpdatedById = currentUser?.Id;
                payrollRun.UpdatedOn = DateTime.Now;

                _db.PayrollRuns.Update(payrollRun);
                await _db.SaveChangesAsync();

                await LogAuditTrailAsync("RUN_PAYROLL", null, payrollRun);

                return Json(new
                {
                    success = true,
                    message = "Payroll run created and processed successfully.",
                    payrollRunId = payrollRun.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running payroll for period {Id}", id);
                return Json(new { success = false, message = $"An error occurred while running payroll: {ex.Message}" });
            }
        }

        //=======================================================Assign Employees & Time Card=================================================================

        [HttpGet]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> AssignEmployeesAndTimeCardModal(int id)
        {
            var viewModel = await GetAssignEmployeesAndTimeCardViewModelAsync(id);
            if (viewModel == null) return NotFound();
            return PartialView("Partials/_AssignEmployeesAndTimeCardList", viewModel);
        }

        [HttpGet]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> AssignEmployeesAndTimeCard(int id)
        {
            var viewModel = await GetAssignEmployeesAndTimeCardBatchSummaryAsync(id);
            if (viewModel == null) return NotFound();
            return View("AssignEmployeesAndTimeCard", viewModel);
        }

        private async Task<AssignEmployeesAndTimeCardViewModel?> GetAssignEmployeesAndTimeCardBatchSummaryAsync(int id)
        {
            var payrollPeriod = await _db.PayrollPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(pp => pp.Id == id && !pp.IsDeleted);

            if (payrollPeriod == null)
                return null;

            var hasCompletedRun = await _db.PayrollRuns
                .AnyAsync(pr => pr.PayrollPeriodId == id && pr.Status == PayrollStatus.Completed);

            var batches = await _db.PayrollPeriodBatches
                .AsNoTracking()
                .Where(b => b.PayrollPeriodId == id && !b.IsDeleted)
                .OrderBy(b => b.DisplayOrder).ThenBy(b => b.Id)
                .Select(b => new PayrollPeriodBatchSummaryRowViewModel
                {
                    BatchId = b.Id,
                    OrganizationUnitName = b.DepartmentDisplay,
                    EmploymentTypeName = b.EmploymentTypeDisplay,
                    EmployeeCount = b.EmployeeCount,
                    DisplayOrder = b.DisplayOrder
                })
                .ToListAsync();

            return new AssignEmployeesAndTimeCardViewModel
            {
                PayrollPeriodId = payrollPeriod.Id,
                PayrollPeriodName = payrollPeriod.Name,
                PeriodRange = payrollPeriod.PeriodRange,
                IsLocked = payrollPeriod.IsLocked,
                HasCompletedPayrollRun = hasCompletedRun,
                Batches = batches
            };
        }

        private async Task<PayrollPeriodTimeCardViewModel?> GetAssignEmployeesAndTimeCardViewModelAsync(int id)
        {
            var payrollPeriod = await _db.PayrollPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(pp => pp.Id == id && !pp.IsDeleted);

            if (payrollPeriod == null)
                return null;

            var dayTypes = await GetActiveDayTypesAsync();

            var assignedEmployees = await _db.PayrollPeriodEmployees
                .Where(ppe => ppe.PayrollPeriodId == id)
                .Include(ppe => ppe.Employee)
                .Select(ppe => ppe.Employee)
                .ToListAsync();

            var assignedEmployeeIds = assignedEmployees.Select(e => e.Id).ToList();
            var unitInfo = await EmployeeOrganizationHelper.GetActiveUnitInfoAsync(_db, assignedEmployeeIds);

            var timeCards = await _db.PayrollPeriodTimeCards
                .Where(tc => tc.PayrollPeriodId == id && assignedEmployeeIds.Contains(tc.EmployeeId))
                .Include(tc => tc.Details)
                .ToListAsync();

            var timeCardByEmployee = timeCards.ToDictionary(tc => tc.EmployeeId, tc => tc);

            return new PayrollPeriodTimeCardViewModel
            {
                PayrollPeriodId = payrollPeriod.Id,
                PayrollPeriodName = payrollPeriod.Name,
                PeriodRange = payrollPeriod.PeriodRange,
                IsLocked = payrollPeriod.IsLocked,
                DayTypes = dayTypes,
                Rows = assignedEmployees.Select(e =>
                {
                    timeCardByEmployee.TryGetValue(e.Id, out var tc);
                    unitInfo.TryGetValue(e.Id, out var info);
                    return new PayrollPeriodTimeCardRowViewModel
                    {
                        EmployeeId = e.Id,
                        EmployeeName = e.FullName,
                        OrganizationStructureId = info.FirstUnitId,
                        OrganizationUnitName = info.Names ?? "",
                        IsAssigned = true,
                        LateMinutes = tc?.LateMinutes ?? 0,
                        UndertimeMinutes = tc?.UndertimeMinutes ?? 0,
                        AbsentHours = tc?.AbsentHours ?? 0,
                        Notes = tc?.Notes,
                        Details = BuildDetailRows(tc, dayTypes)
                    };
                }).OrderBy(r => r.OrganizationUnitName).ThenBy(r => r.EmployeeName).ToList()
            };
        }

        [HttpGet]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> EditAssignEmployeesAndTimeCard(int id)
        {
            var payrollPeriod = await _db.PayrollPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(pp => pp.Id == id && !pp.IsDeleted);

            if (payrollPeriod == null)
                return NotFound();

            var dayTypes = await GetActiveDayTypesAsync();

            var allEmployees = await _db.Employees
                .Where(e => e.IsActive && !e.IsDeleted)
                .OrderBy(e => e.Surname)
                .ThenBy(e => e.FirstName)
                .ToListAsync();

            var allUnitInfo = await EmployeeOrganizationHelper.GetActiveUnitInfoAsync(
                _db, allEmployees.Select(e => e.Id));

            var assignedEmployeeIds = await _db.PayrollPeriodEmployees
                .Where(ppe => ppe.PayrollPeriodId == id)
                .Select(ppe => ppe.EmployeeId)
                .ToListAsync();

            var timeCards = await _db.PayrollPeriodTimeCards
                .Where(tc => tc.PayrollPeriodId == id)
                .Include(tc => tc.Details)
                .ToListAsync();

            var byEmployee = timeCards.ToDictionary(tc => tc.EmployeeId, tc => tc);

            var organizationUnits = await _db.OrganizationStructures
                .Where(o => o.IsActive)
                .OrderBy(o => o.Name)
                .Select(o => new SelectListItem { Value = o.Id.ToString(), Text = o.Name })
                .ToListAsync();

            var vm = new PayrollPeriodTimeCardViewModel
            {
                PayrollPeriodId = payrollPeriod.Id,
                PayrollPeriodName = payrollPeriod.Name,
                PeriodRange = payrollPeriod.PeriodRange,
                IsLocked = payrollPeriod.IsLocked,
                OrganizationUnits = organizationUnits,
                DayTypes = dayTypes,
                Rows = allEmployees.Select(e =>
                {
                    var isAssigned = assignedEmployeeIds.Contains(e.Id);
                    byEmployee.TryGetValue(e.Id, out var tc);
                    allUnitInfo.TryGetValue(e.Id, out var info);
                    return new PayrollPeriodTimeCardRowViewModel
                    {
                        EmployeeId = e.Id,
                        EmployeeName = e.FullName,
                        OrganizationStructureId = info.FirstUnitId,
                        OrganizationUnitName = info.Names ?? "",
                        IsAssigned = isAssigned,
                        LateMinutes = tc?.LateMinutes ?? 0,
                        UndertimeMinutes = tc?.UndertimeMinutes ?? 0,
                        AbsentHours = tc?.AbsentHours ?? 0,
                        Notes = tc?.Notes,
                        Details = BuildDetailRows(tc, dayTypes)
                    };
                }).ToList()
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("Partials/_EditAssignEmployeesAndTimeCard", vm);

            return View("EditAssignEmployeesAndTimeCard", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> SaveAssignEmployeesAndTimeCard(PayrollPeriodTimeCardViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Validation failed." });

            var payrollPeriod = await _db.PayrollPeriods
                .FirstOrDefaultAsync(pp => pp.Id == model.PayrollPeriodId && !pp.IsDeleted);

            if (payrollPeriod == null)
                return Json(new { success = false, message = "Payroll period not found." });

            if (payrollPeriod.IsLocked)
                return Json(new { success = false, message = "Payroll period is locked." });

            var currentUser = await _userManager.GetUserAsync(User);

            var assignedRows = model.Rows.Where(r => r.IsAssigned).ToList();
            if (!assignedRows.Any())
                return Json(new { success = false, message = "Select at least one employee to assign." });

            // Validate: all assigned employees must have the same employment type (Regular, Contractual, Casual)
            var assignedEmployeeIds = assignedRows.Select(r => r.EmployeeId).ToList();
            var employeesWithBasis = await _db.Employees
                .Where(e => assignedEmployeeIds.Contains(e.Id))
                .Select(e => new { e.Id, e.FullName, EmploymentType = e.EmploymentBasis.ToString() })
                .ToListAsync();
            var distinctEmploymentTypes = employeesWithBasis.Select(e => e.EmploymentType).Distinct().ToList();
            if (distinctEmploymentTypes.Count > 1)
            {
                var typesList = string.Join(", ", distinctEmploymentTypes);
                return Json(new
                {
                    success = false,
                    message = "Cannot mix employees with different employment types. " +
                              "Regular employees cannot be grouped with Contractual or Casual employees (and vice versa). " +
                              $"Selected employees include: {typesList}. All assigned employees must have the same employment type."
                });
            }

            var duplicateDayTypeErrors = GetDuplicateDayTypeErrors(assignedRows);
            if (duplicateDayTypeErrors.Count > 0)
                return Json(new { success = false, message = "Duplicate day type per employee is not allowed. " + string.Join(" ", duplicateDayTypeErrors) });
            var existingAssignments = await _db.PayrollPeriodEmployees
                .Where(ppe => ppe.PayrollPeriodId == model.PayrollPeriodId)
                .ToListAsync();

            var existingAssignedIds = existingAssignments.Select(ppe => ppe.EmployeeId).ToList();

            var employeesToAdd = assignedEmployeeIds
                .Where(eid => !existingAssignedIds.Contains(eid))
                .Select(empId => new PayrollPeriodEmployee
                {
                    PayrollPeriodId = model.PayrollPeriodId,
                    EmployeeId = empId,
                    CreatedById = currentUser?.Id,
                    CreatedOn = DateTime.Now
                }).ToList();

            if (employeesToAdd.Any())
                _db.PayrollPeriodEmployees.AddRange(employeesToAdd);

            var employeesToRemove = existingAssignments
                .Where(ppe => !assignedEmployeeIds.Contains(ppe.EmployeeId))
                .ToList();

            if (employeesToRemove.Any())
                _db.PayrollPeriodEmployees.RemoveRange(employeesToRemove);

            await _db.SaveChangesAsync();

            var employeeIds = assignedRows.Select(r => r.EmployeeId).ToList();

            var existingTimeCards = await _db.PayrollPeriodTimeCards
                .Where(tc => tc.PayrollPeriodId == model.PayrollPeriodId && employeeIds.Contains(tc.EmployeeId))
                .Include(tc => tc.Details)
                .ToListAsync();

            foreach (var row in assignedRows)
            {
                var tc = existingTimeCards.FirstOrDefault(x => x.EmployeeId == row.EmployeeId);
                if (tc == null)
                {
                    tc = new PayrollPeriodTimeCardEntry
                    {
                        PayrollPeriodId = model.PayrollPeriodId,
                        EmployeeId = row.EmployeeId,
                        CreatedById = currentUser?.Id,
                        CreatedOn = DateTime.Now
                    };
                    _db.PayrollPeriodTimeCards.Add(tc);
                }
                else
                {
                    tc.UpdatedById = currentUser?.Id;
                    tc.UpdatedOn = DateTime.Now;
                }

                tc.LateMinutes = row.LateMinutes;
                tc.UndertimeMinutes = row.UndertimeMinutes;
                tc.AbsentHours = row.AbsentHours;
                tc.Notes = row.Notes;

                SyncDetails(tc, row.Details, currentUser?.Id);
            }

            var timeCardsToRemove = await _db.PayrollPeriodTimeCards
                .Where(tc => tc.PayrollPeriodId == model.PayrollPeriodId
                          && !assignedEmployeeIds.Contains(tc.EmployeeId))
                .ToListAsync();

            if (timeCardsToRemove.Any())
                _db.PayrollPeriodTimeCards.RemoveRange(timeCardsToRemove);

            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Employee assignments and time card saved successfully." });
        }

        //=======================================================Multi-batch=================================================================

        [HttpGet]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> EditBatch(int id, int? batchId, string? organizationStructureIds, int? employmentBasis)
        {
            var payrollPeriod = await _db.PayrollPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(pp => pp.Id == id && !pp.IsDeleted);

            if (payrollPeriod == null)
                return NotFound();

            var hasCompletedRun = await _db.PayrollRuns
                .AnyAsync(pr => pr.PayrollPeriodId == id && pr.Status == PayrollStatus.Completed);

            if (hasCompletedRun)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Content("<div class='alert alert-danger m-3'><i class='fas fa-lock mr-1'></i> A completed payroll run exists for this period. Employee groups cannot be added or edited.</div>");
                return RedirectToAction(nameof(AssignEmployeesAndTimeCard), new { id });
            }

            var dayTypes = await GetActiveDayTypesAsync();

            var organizationUnits = await _db.OrganizationStructures
                .Where(o => o.IsActive)
                .OrderBy(o => o.Name)
                .Select(o => new SelectListItem { Value = o.Id.ToString(), Text = o.Name })
                .ToListAsync();

            var employmentTypes = Enum.GetValues(typeof(EmploymentBasis))
                .Cast<EmploymentBasis>()
                .Select(e => new SelectListItem { Value = ((int)e).ToString(), Text = e.ToString() })
                .ToList();

            var regularIndex = dayTypes.FindIndex(dt =>
                dt.Name.Contains("Regular", StringComparison.OrdinalIgnoreCase));

            var vm = new AssignEmployeesAndTimeCardBatchEditViewModel
            {
                PayrollPeriodId = payrollPeriod.Id,
                BatchId = batchId,
                PayrollPeriodName = payrollPeriod.Name,
                PeriodRange = payrollPeriod.PeriodRange,
                IsLocked = payrollPeriod.IsLocked,
                OrganizationUnits = organizationUnits,
                EmploymentTypes = employmentTypes,
                DayTypes = dayTypes,
                RegularDayTypeIndex = regularIndex >= 0 ? regularIndex : -1,
                Rows = new List<AssignEmployeesAndTimeCardRowViewModel>()
            };

            var orgUnitIdList = new List<int>();
            if (!string.IsNullOrWhiteSpace(organizationStructureIds))
                foreach (var s in organizationStructureIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (int.TryParse(s.Trim(), out var orgUnitId))
                        orgUnitIdList.Add(orgUnitId);

            if (batchId.HasValue)
            {
                var batch = await _db.PayrollPeriodBatches
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == batchId.Value && b.PayrollPeriodId == id && !b.IsDeleted);

                if (batch != null)
                {
                    var timeCardsInBatch = await _db.PayrollPeriodTimeCards
                        .Where(tc => tc.PayrollPeriodId == id && tc.PayrollPeriodBatchId == batchId)
                        .Include(tc => tc.Details)
                        .ToListAsync();

                    var empIds = timeCardsInBatch.Select(tc => tc.EmployeeId).ToList();

                    var employees = await _db.Employees
                        .Where(e => e.IsActive && !e.IsDeleted && empIds.Contains(e.Id))
                        .OrderBy(e => e.Surname).ThenBy(e => e.FirstName)
                        .ToListAsync();

                    var batchUnitInfo = await EmployeeOrganizationHelper.GetActiveUnitInfoAsync(
                        _db, employees.Select(e => e.Id));
                    var tcByEmp = timeCardsInBatch.ToDictionary(tc => tc.EmployeeId);

                    vm.Rows = employees.Select(e =>
                    {
                        tcByEmp.TryGetValue(e.Id, out var tc);
                        batchUnitInfo.TryGetValue(e.Id, out var info);
                        return new AssignEmployeesAndTimeCardRowViewModel
                        {
                            EmployeeId = e.Id,
                            EmployeeName = e.FullName,
                            OrganizationUnitName = info.Names ?? "",
                            EmploymentTypeName = e.EmploymentBasis.ToString(),
                            IsSelected = true,
                            LateMinutes = tc?.LateMinutes ?? 0,
                            UndertimeMinutes = tc?.UndertimeMinutes ?? 0,
                            AbsentHours = tc?.AbsentHours ?? 0,
                            Notes = tc?.Notes,
                            Details = BuildDetailRows(tc, dayTypes)
                        };
                    }).ToList();
                }
            }
            else if (orgUnitIdList.Any() || employmentBasis.HasValue)
            {
                vm.SelectedOrganizationStructureIds = orgUnitIdList;
                vm.SelectedEmploymentBasis = employmentBasis;

                var query = BuildBatchEmployeePickerQuery(id, orgUnitIdList, employmentBasis);
                var alreadyAssignedIds = await GetAlreadyAssignedEmployeeIdsAsync(id);
                var totalMatchingIds = await query.Select(e => e.Id).ToListAsync();
                vm.ExcludedCount = totalMatchingIds.Count(eid => alreadyAssignedIds.Contains(eid));
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("Partials/_EditBatch", vm);

            return View("EditBatch", vm);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> GetBatchEmployeePickerJson(int payrollPeriodId)
        {
            var draw = HttpContext.Request.Form["draw"].FirstOrDefault() ?? "1";
            var start = int.TryParse(HttpContext.Request.Form["start"].FirstOrDefault(), out var st) ? st : 0;
            var length = int.TryParse(HttpContext.Request.Form["length"].FirstOrDefault(), out var ln) ? ln : 25;
            var sortColIdx = HttpContext.Request.Form["order[0][column]"].FirstOrDefault() ?? "1";
            var sortColumn = HttpContext.Request.Form[$"columns[{sortColIdx}][data]"].FirstOrDefault();
            var sortDir = HttpContext.Request.Form["order[0][dir]"].FirstOrDefault() ?? "asc";
            var searchValue = HttpContext.Request.Form["search[value]"].FirstOrDefault();

            var orgUnitIdsRaw = HttpContext.Request.Form["organizationStructureIds"].FirstOrDefault();
            var employmentBasisRaw = HttpContext.Request.Form["employmentBasis"].FirstOrDefault();

            var orgUnitIdList = ParseOrgUnitIds(orgUnitIdsRaw);
            int? employmentBasis = int.TryParse(employmentBasisRaw, out var eb) ? eb : null;

            var payrollPeriod = await _db.PayrollPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(pp => pp.Id == payrollPeriodId && !pp.IsDeleted);

            if (payrollPeriod == null)
            {
                return Json(new
                {
                    draw,
                    recordsTotal = 0,
                    recordsFiltered = 0,
                    data = Array.Empty<object>()
                });
            }

            var query = BuildBatchEmployeePickerQuery(payrollPeriodId, orgUnitIdList, employmentBasis);
            var recordsTotal = await query.CountAsync();

            if (!string.IsNullOrWhiteSpace(searchValue))
            {
                var s = searchValue.ToLower();
                query = query.Where(e =>
                    e.FirstName.ToLower().Contains(s) ||
                    e.Surname.ToLower().Contains(s) ||
                    (e.MiddleName != null && e.MiddleName.ToLower().Contains(s)) ||
                    (e.FirstName + " " + e.Surname).ToLower().Contains(s) ||
                    (e.FirstName + " " + e.MiddleName + " " + e.Surname).ToLower().Contains(s));
            }

            var recordsFiltered = await query.CountAsync();

            query = sortColumn switch
            {
                "employmentTypeName" => sortDir == "asc"
                    ? query.OrderBy(e => e.EmploymentBasis)
                    : query.OrderByDescending(e => e.EmploymentBasis),
                "organizationUnitName" => sortDir == "asc"
                    ? query.OrderBy(e => e.Surname).ThenBy(e => e.FirstName)
                    : query.OrderByDescending(e => e.Surname).ThenByDescending(e => e.FirstName),
                _ => sortDir == "asc"
                    ? query.OrderBy(e => e.Surname).ThenBy(e => e.FirstName)
                    : query.OrderByDescending(e => e.Surname).ThenByDescending(e => e.FirstName)
            };

            var pageEmployees = await query
                .Skip(start)
                .Take(length > 0 ? length : 25)
                .ToListAsync();

            var unitInfo = await EmployeeOrganizationHelper.GetActiveUnitInfoAsync(
                _db, pageEmployees.Select(e => e.Id));

            var data = pageEmployees.Select(e =>
            {
                unitInfo.TryGetValue(e.Id, out var info);
                return new
                {
                    id = e.Id,
                    fullName = e.FullName,
                    organizationUnitName = info.Names ?? "",
                    employmentTypeName = e.EmploymentBasis.ToString()
                };
            });

            return Json(new
            {
                draw,
                recordsTotal,
                recordsFiltered,
                data
            });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> GetBatchEmployeePickerAllIds(int payrollPeriodId, string? organizationStructureIds, int? employmentBasis)
        {
            var orgUnitIdList = ParseOrgUnitIds(organizationStructureIds);
            var query = BuildBatchEmployeePickerQuery(payrollPeriodId, orgUnitIdList, employmentBasis);
            var ids = await query.Select(e => e.Id).ToListAsync();
            return Json(new { success = true, count = ids.Count, employeeIds = ids });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> CreateBatchQuick(CreateBatchQuickRequest request)
        {
            var employeeIds = ParseEmployeeIds(request.EmployeeIds);
            if (!employeeIds.Any())
                return Json(new { success = false, message = "Select at least one employee for this group." });

            var payrollPeriod = await _db.PayrollPeriods
                .FirstOrDefaultAsync(pp => pp.Id == request.PayrollPeriodId && !pp.IsDeleted);

            if (payrollPeriod == null)
                return Json(new { success = false, message = "Payroll period not found." });

            if (payrollPeriod.IsLocked)
                return Json(new { success = false, message = "Payroll period is locked." });

            var hasCompletedRun = await _db.PayrollRuns
                .AnyAsync(pr => pr.PayrollPeriodId == request.PayrollPeriodId &&
                                pr.Status == PayrollStatus.Completed);

            if (hasCompletedRun)
                return Json(new { success = false, message = "A completed payroll run exists for this period. Employee groups cannot be modified." });

            var employees = await _db.Employees
                .Where(e => employeeIds.Contains(e.Id) && e.IsActive && !e.IsDeleted)
                .ToListAsync();

            if (employees.Count != employeeIds.Count)
                return Json(new { success = false, message = "One or more selected employees are invalid or inactive." });

            var distinctEmploymentTypes = employees.Select(e => e.EmploymentBasis.ToString()).Distinct().ToList();
            if (distinctEmploymentTypes.Count > 1)
            {
                return Json(new
                {
                    success = false,
                    message = "An employee group cannot mix employees with different employment types. " +
                              $"This selection includes: {string.Join(", ", distinctEmploymentTypes)}."
                });
            }

            var alreadyInBatch = await _db.PayrollPeriodTimeCards
                .Where(tc => tc.PayrollPeriodId == request.PayrollPeriodId
                          && employeeIds.Contains(tc.EmployeeId)
                          && tc.PayrollPeriodBatchId.HasValue)
                .Select(tc => tc.EmployeeId)
                .ToListAsync();

            if (alreadyInBatch.Any())
            {
                var dupNames = await _db.Employees
                    .Where(e => alreadyInBatch.Contains(e.Id))
                    .Select(e => e.FullName)
                    .Take(5)
                    .ToListAsync();
                var suffix = alreadyInBatch.Count > 5 ? $" and {alreadyInBatch.Count - 5} more" : "";
                return Json(new
                {
                    success = false,
                    message = "Some employees are already assigned to another group: " +
                              string.Join(", ", dupNames) + suffix + "."
                });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var displayUnitInfo = await EmployeeOrganizationHelper.GetActiveUnitInfoAsync(_db, employeeIds);
            var departmentDisplay = string.Join(", ", displayUnitInfo.Values
                .Select(v => v.Names)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .OrderBy(x => x)
                .Take(5));
            if (departmentDisplay.Length > 200) departmentDisplay = departmentDisplay[..197] + "...";
            var employmentDisplay = distinctEmploymentTypes.FirstOrDefault() ?? "Regular";

            var dayTypes = await GetActiveDayTypesAsync();
            Dictionary<Guid, EmployeeTimeCardFromDtrResult>? dtrByEmployee = null;
            IReadOnlyDictionary<Guid, IReadOnlyDictionary<int, PendingOvertimeImportBucket>>? otByEmployee = null;

            if (request.ImportFromDtr)
            {
                var dtrResults = await _timeCardFromDtrService.BuildForEmployeesAsync(
                    employeeIds,
                    payrollPeriod.PeriodStartDate,
                    payrollPeriod.PeriodEndDate);
                dtrByEmployee = dtrResults.ToDictionary(r => r.EmployeeId);

                otByEmployee = await _overtimeRequestService.GetPendingImportHoursAsync(
                    employeeIds,
                    payrollPeriod.PeriodStartDate,
                    payrollPeriod.PeriodEndDate);
            }

            var nextOrder = await _db.PayrollPeriodBatches
                .Where(b => b.PayrollPeriodId == request.PayrollPeriodId && !b.IsDeleted)
                .CountAsync();

            var newBatch = new PayrollPeriodBatch
            {
                PayrollPeriodId = request.PayrollPeriodId,
                DepartmentDisplay = departmentDisplay,
                EmploymentTypeDisplay = employmentDisplay,
                DisplayOrder = nextOrder,
                EmployeeCount = employeeIds.Count,
                IsDeleted = false,
                CreatedById = currentUser?.Id,
                CreatedOn = DateTime.Now
            };
            _db.PayrollPeriodBatches.Add(newBatch);
            await _db.SaveChangesAsync();

            foreach (var employee in employees)
            {
                EmployeeTimeCardFromDtrResult? dtr = null;
                if (dtrByEmployee != null)
                    dtrByEmployee.TryGetValue(employee.Id, out dtr);

                IReadOnlyDictionary<int, PendingOvertimeImportBucket>? otHours = null;
                if (otByEmployee != null)
                    otByEmployee.TryGetValue(employee.Id, out otHours);

                var details = dtr != null
                    ? BuildDetailRowsFromDtr(dtr, dayTypes, otHours)
                    : BuildDetailRows(null, dayTypes);

                if (dtr == null && otHours != null)
                {
                    foreach (var (dayTypeId, bucket) in otHours)
                        ApplyOtBucket(details, dayTypeId, bucket);
                }

                var notes = dtr?.Notes;
                if (otHours != null && otHours.Values.Sum(b => b.TotalHours) > 0)
                {
                    var otNote = $"OT import {otHours.Values.Sum(b => b.TotalHours):0.##}h";
                    notes = string.IsNullOrWhiteSpace(notes) ? otNote : $"{notes} · {otNote}";
                }

                var tc = new PayrollPeriodTimeCardEntry
                {
                    PayrollPeriodId = request.PayrollPeriodId,
                    EmployeeId = employee.Id,
                    PayrollPeriodBatchId = newBatch.Id,
                    LateMinutes = dtr?.LateMinutes ?? 0,
                    UndertimeMinutes = dtr?.UndertimeMinutes ?? 0,
                    AbsentHours = dtr?.AbsentHours ?? 0,
                    Notes = notes,
                    CreatedById = currentUser?.Id,
                    CreatedOn = DateTime.Now
                };
                SyncDetails(tc, details, currentUser?.Id);
                _db.PayrollPeriodTimeCards.Add(tc);

                var hasPpe = await _db.PayrollPeriodEmployees
                    .AnyAsync(p => p.PayrollPeriodId == request.PayrollPeriodId && p.EmployeeId == employee.Id);
                if (!hasPpe)
                {
                    _db.PayrollPeriodEmployees.Add(new PayrollPeriodEmployee
                    {
                        PayrollPeriodId = request.PayrollPeriodId,
                        EmployeeId = employee.Id,
                        CreatedById = currentUser?.Id,
                        CreatedOn = DateTime.Now
                    });
                }
            }

            await _db.SaveChangesAsync();

            if (request.ImportFromDtr && otByEmployee != null && otByEmployee.Count > 0)
            {
                await _overtimeRequestService.MarkImportedForPeriodAsync(
                    request.PayrollPeriodId,
                    employeeIds,
                    payrollPeriod.PeriodStartDate,
                    payrollPeriod.PeriodEndDate,
                    currentUser?.UserName ?? "system");
            }

            var importNote = request.ImportFromDtr
                ? " Time card data was imported from DTR — review the group if needed."
                : " Enter time card data by editing the group.";

            return Json(new
            {
                success = true,
                message = $"Employee group created with {employeeIds.Count} employee(s).{importNote}",
                batchId = newBatch.Id
            });
        }

        private async Task<HashSet<Guid>> GetAlreadyAssignedEmployeeIdsAsync(int payrollPeriodId)
        {
            var ids = await _db.PayrollPeriodTimeCards
                .Where(tc => tc.PayrollPeriodId == payrollPeriodId && tc.PayrollPeriodBatchId.HasValue)
                .Select(tc => tc.EmployeeId)
                .Distinct()
                .ToListAsync();
            return ids.ToHashSet();
        }

        private IQueryable<Employee> BuildBatchEmployeePickerQuery(
            int payrollPeriodId,
            List<int> orgUnitIdList,
            int? employmentBasis)
        {
            var query = _db.Employees.Where(e => e.IsActive && !e.IsDeleted);

            if (orgUnitIdList.Any())
                query = query.FilterByActiveOrganizationUnits(_db, orgUnitIdList);

            if (employmentBasis.HasValue)
                query = query.Where(e => (int)e.EmploymentBasis == employmentBasis.Value);

            var alreadyAssignedIds = _db.PayrollPeriodTimeCards
                .Where(tc => tc.PayrollPeriodId == payrollPeriodId && tc.PayrollPeriodBatchId.HasValue)
                .Select(tc => tc.EmployeeId);

            return query.Where(e => !alreadyAssignedIds.Contains(e.Id));
        }

        private static List<int> ParseOrgUnitIds(string? raw)
        {
            var list = new List<int>();
            if (string.IsNullOrWhiteSpace(raw)) return list;
            foreach (var s in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(s.Trim(), out var id))
                    list.Add(id);
            return list;
        }

        private static List<Guid> ParseEmployeeIds(string? raw)
        {
            var list = new List<Guid>();
            if (string.IsNullOrWhiteSpace(raw)) return list;
            foreach (var s in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (Guid.TryParse(s.Trim(), out var id))
                    list.Add(id);
            return list.Distinct().ToList();
        }

        private static List<TimeCardDetailRowViewModel> BuildDetailRowsFromDtr(
            EmployeeTimeCardFromDtrResult dtr,
            List<DayTypeSelectItem> dayTypes,
            IReadOnlyDictionary<int, PendingOvertimeImportBucket>? overtimeByDayTypeId = null)
        {
            var details = BuildDetailRows(null, dayTypes);

            void SetRegular(int dayTypeId, decimal hours)
            {
                if (hours <= 0) return;
                var row = details.FirstOrDefault(d => d.DayTypeId == dayTypeId);
                if (row != null) row.RegularHours = hours;
            }

            void SetNd(int dayTypeId, decimal hours)
            {
                if (hours <= 0) return;
                var row = details.FirstOrDefault(d => d.DayTypeId == dayTypeId);
                if (row != null) row.NightDifferentialHours = hours;
            }

            if (dtr.HoursByDayTypeId.Count > 0)
            {
                foreach (var (dayTypeId, hours) in dtr.HoursByDayTypeId)
                    SetRegular(dayTypeId, hours);
            }
            else
            {
                // Fallback for legacy weekday buckets
                var regularDayType = dayTypes.FirstOrDefault(dt =>
                    dt.Name.Contains("Regular", StringComparison.OrdinalIgnoreCase) &&
                    !dt.Name.Contains("Holiday", StringComparison.OrdinalIgnoreCase));
                var saturdayDayType = dayTypes.FirstOrDefault(dt =>
                    dt.Name.Equals("Saturday", StringComparison.OrdinalIgnoreCase));
                var sundayDayType = dayTypes.FirstOrDefault(dt =>
                    dt.Name.Equals("Sunday", StringComparison.OrdinalIgnoreCase));

                if (regularDayType != null) SetRegular(regularDayType.Id, dtr.RegularHours);
                if (saturdayDayType != null) SetRegular(saturdayDayType.Id, dtr.SaturdayHours);
                if (sundayDayType != null) SetRegular(sundayDayType.Id, dtr.SundayHours);
            }

            foreach (var (dayTypeId, hours) in dtr.NightDifferentialHoursByDayTypeId)
                SetNd(dayTypeId, hours);

            if (overtimeByDayTypeId != null)
            {
                foreach (var (dayTypeId, bucket) in overtimeByDayTypeId)
                    ApplyOtBucket(details, dayTypeId, bucket);
            }

            // Absent hours stay on the timecard header (AbsentHours → ABSENCE deduction),
            // not as a DayType earnings line.
            return details;
        }

        private static void ApplyOtBucket(
            List<TimeCardDetailRowViewModel> details,
            int dayTypeId,
            PendingOvertimeImportBucket bucket)
        {
            if (bucket.TotalHours <= 0)
                return;

            var row = details.FirstOrDefault(d => d.DayTypeId == dayTypeId);
            if (row == null)
            {
                // Fallback onto Regular Day so OT is never discarded when DayType ids drift.
                row = details.FirstOrDefault(d =>
                    d.DayTypeName.Contains("Regular", StringComparison.OrdinalIgnoreCase) &&
                    !d.DayTypeName.Contains("Holiday", StringComparison.OrdinalIgnoreCase));
            }

            if (row == null)
                return;

            if (bucket.OvertimeHours > 0)
                row.OvertimeHours += bucket.OvertimeHours;
            if (bucket.NightDifferentialOvertimeHours > 0)
                row.NightDifferentialOvertimeHours += bucket.NightDifferentialOvertimeHours;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> SaveBatch(AssignEmployeesAndTimeCardBatchEditViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Validation failed." });

            var payrollPeriod = await _db.PayrollPeriods
                .FirstOrDefaultAsync(pp => pp.Id == model.PayrollPeriodId && !pp.IsDeleted);

            if (payrollPeriod == null)
                return Json(new { success = false, message = "Payroll period not found." });

            if (payrollPeriod.IsLocked)
                return Json(new { success = false, message = "Payroll period is locked." });

            var hasCompletedRun = await _db.PayrollRuns
                .AnyAsync(pr => pr.PayrollPeriodId == model.PayrollPeriodId
                             && pr.Status == PayrollStatus.Completed);

            if (hasCompletedRun)
                return Json(new { success = false, message = "A completed payroll run exists for this period. Employee groups cannot be modified." });

            var currentUser = await _userManager.GetUserAsync(User);
            var selected = model.Rows.Where(r => r.IsSelected).ToList();

            if (!selected.Any())
                return Json(new { success = false, message = "Select at least one employee for this group." });

            // Validate: all selected employees must have the same employment type (Regular, Contractual, Casual)
            var distinctEmploymentTypes = selected.Select(r => r.EmploymentTypeName).Distinct().ToList();
            if (distinctEmploymentTypes.Count > 1)
            {
                var typesList = string.Join(", ", distinctEmploymentTypes);
                return Json(new
                {
                    success = false,
                    message = "An employee group cannot mix employees with different employment types. " +
                              "Regular employees cannot be grouped with Contractual or Casual employees (and vice versa). " +
                              $"This selection includes: {typesList}. Each group must contain only one employment type."
                });
            }

            var duplicateDayTypeErrors = GetDuplicateDayTypeErrors(selected);
            if (duplicateDayTypeErrors.Count > 0)
                return Json(new { success = false, message = "Duplicate day type per employee is not allowed. " + string.Join(" ", duplicateDayTypeErrors) });

            var employeeIds = selected.Select(r => r.EmployeeId).ToList();

            // Organization unit names and employment types are not posted in the form (display-only);
            // look them up from the database to populate DepartmentDisplay and EmploymentTypeDisplay.
            var displayUnitInfo = await EmployeeOrganizationHelper.GetActiveUnitInfoAsync(_db, employeeIds);
            var employeesForDisplay = await _db.Employees
                .Where(e => employeeIds.Contains(e.Id))
                .Select(e => new { e.Id, EmploymentType = e.EmploymentBasis.ToString() })
                .ToListAsync();
            var departmentDisplay = string.Join(", ", displayUnitInfo.Values
                .Select(v => v.Names)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .OrderBy(x => x)
                .Take(5));
            if (departmentDisplay.Length > 200) departmentDisplay = departmentDisplay[..197] + "...";
            var employmentDisplay = employeesForDisplay.Select(e => e.EmploymentType).FirstOrDefault() ?? "Regular";

            if (model.BatchId.HasValue)
            {
                var batch = await _db.PayrollPeriodBatches
                    .FirstOrDefaultAsync(b => b.Id == model.BatchId.Value
                                         && b.PayrollPeriodId == model.PayrollPeriodId
                                         && !b.IsDeleted);

                if (batch == null)
                    return Json(new { success = false, message = "Employee group not found." });

                var existingInBatch = await _db.PayrollPeriodTimeCards
                    .Where(tc => tc.PayrollPeriodId == model.PayrollPeriodId
                              && tc.PayrollPeriodBatchId == model.BatchId)
                    .Include(tc => tc.Details)
                    .ToListAsync();

                var toRemove = existingInBatch.Where(tc => !employeeIds.Contains(tc.EmployeeId)).ToList();
                foreach (var tc in toRemove)
                    _db.PayrollPeriodTimeCards.Remove(tc);

                await _db.SaveChangesAsync();

                foreach (var tc in toRemove)
                {
                    var anyOther = await _db.PayrollPeriodTimeCards
                        .AnyAsync(t => t.PayrollPeriodId == model.PayrollPeriodId && t.EmployeeId == tc.EmployeeId);
                    if (!anyOther)
                    {
                        var ppe = await _db.PayrollPeriodEmployees
                            .FirstOrDefaultAsync(p => p.PayrollPeriodId == model.PayrollPeriodId && p.EmployeeId == tc.EmployeeId);
                        if (ppe != null)
                            _db.PayrollPeriodEmployees.Remove(ppe);
                    }
                }

                foreach (var row in selected)
                {
                    var tc = await _db.PayrollPeriodTimeCards
                        .Include(t => t.Details)
                        .FirstOrDefaultAsync(t => t.PayrollPeriodId == model.PayrollPeriodId && t.EmployeeId == row.EmployeeId);

                    if (tc == null)
                    {
                        tc = new PayrollPeriodTimeCardEntry
                        {
                            PayrollPeriodId = model.PayrollPeriodId,
                            EmployeeId = row.EmployeeId,
                            PayrollPeriodBatchId = batch.Id,
                            LateMinutes = row.LateMinutes,
                            UndertimeMinutes = row.UndertimeMinutes,
                            AbsentHours = row.AbsentHours,
                            Notes = row.Notes,
                            CreatedById = currentUser?.Id,
                            CreatedOn = DateTime.Now
                        };
                        SyncDetails(tc, row.Details, currentUser?.Id);
                        _db.PayrollPeriodTimeCards.Add(tc);

                        var hasPpe = await _db.PayrollPeriodEmployees
                            .AnyAsync(p => p.PayrollPeriodId == model.PayrollPeriodId && p.EmployeeId == row.EmployeeId);
                        if (!hasPpe)
                            _db.PayrollPeriodEmployees.Add(new PayrollPeriodEmployee
                            {
                                PayrollPeriodId = model.PayrollPeriodId,
                                EmployeeId = row.EmployeeId,
                                CreatedById = currentUser?.Id,
                                CreatedOn = DateTime.Now
                            });
                    }
                    else
                    {
                        tc.PayrollPeriodBatchId = batch.Id;
                        tc.LateMinutes = row.LateMinutes;
                        tc.UndertimeMinutes = row.UndertimeMinutes;
                        tc.AbsentHours = row.AbsentHours;
                        tc.Notes = row.Notes;
                        tc.UpdatedById = currentUser?.Id;
                        tc.UpdatedOn = DateTime.Now;
                        SyncDetails(tc, row.Details, currentUser?.Id);
                        _db.PayrollPeriodTimeCards.Update(tc);
                    }
                }

                batch.DepartmentDisplay = departmentDisplay;
                batch.EmploymentTypeDisplay = employmentDisplay;
                batch.EmployeeCount = selected.Count;
                _db.PayrollPeriodBatches.Update(batch);

                await _db.SaveChangesAsync();
                await MarkPendingOvertimeImportedAsync(
                    model.PayrollPeriodId,
                    selected.Select(r => r.EmployeeId),
                    selected.Any(r => r.Details.Any(d =>
                        d.OvertimeHours > 0 || d.NightDifferentialOvertimeHours > 0)),
                    currentUser?.UserName ?? "system");
                return Json(new { success = true, message = "Employee group updated successfully." });
            }

            // CREATE NEW BATCH
            var alreadyInBatch = await _db.PayrollPeriodTimeCards
                .Where(tc => tc.PayrollPeriodId == model.PayrollPeriodId
                          && employeeIds.Contains(tc.EmployeeId)
                          && tc.PayrollPeriodBatchId.HasValue)
                .Select(tc => new { tc.EmployeeId, tc.PayrollPeriodBatchId })
                .ToListAsync();

            if (alreadyInBatch.Any())
            {
                var dupIds = alreadyInBatch.Select(x => x.EmployeeId).ToList();
                var dupNames = await _db.Employees
                    .Where(e => dupIds.Contains(e.Id))
                    .Select(e => e.FullName)
                    .ToListAsync();

                return Json(new
                {
                    success = false,
                    message = "The following employees are already assigned to another group: " +
                              string.Join(", ", dupNames) +
                              ". Please edit the existing group instead."
                });
            }

            var nextOrder = await _db.PayrollPeriodBatches
                .Where(b => b.PayrollPeriodId == model.PayrollPeriodId && !b.IsDeleted)
                .CountAsync();

            var newBatch = new PayrollPeriodBatch
            {
                PayrollPeriodId = model.PayrollPeriodId,
                DepartmentDisplay = departmentDisplay,
                EmploymentTypeDisplay = employmentDisplay,
                DisplayOrder = nextOrder,
                EmployeeCount = selected.Count,
                IsDeleted = false,
                CreatedById = currentUser?.Id,
                CreatedOn = DateTime.Now
            };
            _db.PayrollPeriodBatches.Add(newBatch);
            await _db.SaveChangesAsync();

            foreach (var row in selected)
            {
                var tc = await _db.PayrollPeriodTimeCards
                    .Include(t => t.Details)
                    .FirstOrDefaultAsync(t => t.PayrollPeriodId == model.PayrollPeriodId && t.EmployeeId == row.EmployeeId);

                if (tc == null)
                {
                    tc = new PayrollPeriodTimeCardEntry
                    {
                        PayrollPeriodId = model.PayrollPeriodId,
                        EmployeeId = row.EmployeeId,
                        PayrollPeriodBatchId = newBatch.Id,
                        LateMinutes = row.LateMinutes,
                        UndertimeMinutes = row.UndertimeMinutes,
                        AbsentHours = row.AbsentHours,
                        Notes = row.Notes,
                        CreatedById = currentUser?.Id,
                        CreatedOn = DateTime.Now
                    };
                    SyncDetails(tc, row.Details, currentUser?.Id);
                    _db.PayrollPeriodTimeCards.Add(tc);
                }
                else
                {
                    tc.PayrollPeriodBatchId = newBatch.Id;
                    tc.LateMinutes = row.LateMinutes;
                    tc.UndertimeMinutes = row.UndertimeMinutes;
                    tc.AbsentHours = row.AbsentHours;
                    tc.Notes = row.Notes;
                    tc.UpdatedById = currentUser?.Id;
                    tc.UpdatedOn = DateTime.Now;
                    SyncDetails(tc, row.Details, currentUser?.Id);
                    _db.PayrollPeriodTimeCards.Update(tc);
                }

                var hasPpe = await _db.PayrollPeriodEmployees
                    .AnyAsync(p => p.PayrollPeriodId == model.PayrollPeriodId && p.EmployeeId == row.EmployeeId);
                if (!hasPpe)
                    _db.PayrollPeriodEmployees.Add(new PayrollPeriodEmployee
                    {
                        PayrollPeriodId = model.PayrollPeriodId,
                        EmployeeId = row.EmployeeId,
                        CreatedById = currentUser?.Id,
                        CreatedOn = DateTime.Now
                    });
            }

            await _db.SaveChangesAsync();
            await MarkPendingOvertimeImportedAsync(
                model.PayrollPeriodId,
                selected.Select(r => r.EmployeeId),
                selected.Any(r => r.Details.Any(d =>
                    d.OvertimeHours > 0 || d.NightDifferentialOvertimeHours > 0)),
                currentUser?.UserName ?? "system");
            return Json(new { success = true, message = "Employee group saved successfully." });
        }

        private async Task MarkPendingOvertimeImportedAsync(
            int payrollPeriodId,
            IEnumerable<Guid> employeeIds,
            bool hasOvertimeHoursOnCard,
            string processedBy)
        {
            if (!hasOvertimeHoursOnCard)
                return;

            var period = await _db.PayrollPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(pp => pp.Id == payrollPeriodId && !pp.IsDeleted);
            if (period == null)
                return;

            await _overtimeRequestService.MarkImportedForPeriodAsync(
                payrollPeriodId,
                employeeIds,
                period.PeriodStartDate,
                period.PeriodEndDate,
                processedBy);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> ImportTimeCardFromDtr([FromBody] ImportTimeCardFromDtrRequest request)
        {
            if (request.EmployeeIds == null || request.EmployeeIds.Count == 0)
                return Json(new { success = false, message = "Select at least one employee to import from DTR." });

            var payrollPeriod = await _db.PayrollPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(pp => pp.Id == request.PayrollPeriodId && !pp.IsDeleted);

            if (payrollPeriod == null)
                return Json(new { success = false, message = "Payroll period not found." });

            if (payrollPeriod.IsLocked)
                return Json(new { success = false, message = "Payroll period is locked." });

            var hasCompletedRun = await _db.PayrollRuns
                .AnyAsync(pr => pr.PayrollPeriodId == request.PayrollPeriodId &&
                                pr.Status == PayrollStatus.Completed);

            if (hasCompletedRun)
                return Json(new { success = false, message = "A completed payroll run exists for this period." });

            var dayTypes = await GetActiveDayTypesAsync();
            var dayTypeById = dayTypes.ToDictionary(dt => dt.Id);

            var dtrResults = await _timeCardFromDtrService.BuildForEmployeesAsync(
                request.EmployeeIds,
                payrollPeriod.PeriodStartDate,
                payrollPeriod.PeriodEndDate);

            var otByEmployee = await _overtimeRequestService.GetPendingImportHoursAsync(
                request.EmployeeIds,
                payrollPeriod.PeriodStartDate,
                payrollPeriod.PeriodEndDate);

            var rows = dtrResults.Select(r =>
            {
                var hoursByDayType = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                var ndByDayType = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                var otByDayType = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                var ndOtByDayType = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

                if (r.HoursByDayTypeId.Count > 0)
                {
                    foreach (var (dayTypeId, hours) in r.HoursByDayTypeId)
                    {
                        if (hours <= 0 || !dayTypeById.TryGetValue(dayTypeId, out var dt))
                            continue;
                        hoursByDayType[dt.Name] = hours;
                    }
                }
                else
                {
                    var regularDayType = dayTypes.FirstOrDefault(dt =>
                        dt.Name.Contains("Regular", StringComparison.OrdinalIgnoreCase) &&
                        !dt.Name.Contains("Holiday", StringComparison.OrdinalIgnoreCase));
                    var saturdayDayType = dayTypes.FirstOrDefault(dt =>
                        dt.Name.Equals("Saturday", StringComparison.OrdinalIgnoreCase));
                    var sundayDayType = dayTypes.FirstOrDefault(dt =>
                        dt.Name.Equals("Sunday", StringComparison.OrdinalIgnoreCase));

                    if (regularDayType != null && r.RegularHours > 0)
                        hoursByDayType[regularDayType.Name] = r.RegularHours;
                    if (saturdayDayType != null && r.SaturdayHours > 0)
                        hoursByDayType[saturdayDayType.Name] = r.SaturdayHours;
                    if (sundayDayType != null && r.SundayHours > 0)
                        hoursByDayType[sundayDayType.Name] = r.SundayHours;
                }

                foreach (var (dayTypeId, hours) in r.NightDifferentialHoursByDayTypeId)
                {
                    if (hours <= 0 || !dayTypeById.TryGetValue(dayTypeId, out var dt))
                        continue;
                    ndByDayType[dt.Name] = hours;
                }

                if (otByEmployee.TryGetValue(r.EmployeeId, out var otMap))
                {
                    foreach (var (dayTypeId, bucket) in otMap)
                    {
                        if (bucket.TotalHours <= 0)
                            continue;

                        DayTypeSelectItem? dt = null;
                        if (!dayTypeById.TryGetValue(dayTypeId, out dt))
                        {
                            // Fallback: attach to Regular Day so OT is never dropped silently.
                            dt = dayTypes.FirstOrDefault(x =>
                                x.Name.Contains("Regular", StringComparison.OrdinalIgnoreCase) &&
                                !x.Name.Contains("Holiday", StringComparison.OrdinalIgnoreCase));
                        }

                        if (dt == null)
                            continue;

                        if (bucket.OvertimeHours > 0)
                        {
                            otByDayType.TryGetValue(dt.Name, out var existingOt);
                            otByDayType[dt.Name] = existingOt + bucket.OvertimeHours;
                        }

                        if (bucket.NightDifferentialOvertimeHours > 0)
                        {
                            ndOtByDayType.TryGetValue(dt.Name, out var existingNdOt);
                            ndOtByDayType[dt.Name] = existingNdOt + bucket.NightDifferentialOvertimeHours;
                        }
                    }
                }

                var notes = r.Notes;
                var importedOtTotal = otByDayType.Values.Sum() + ndOtByDayType.Values.Sum();
                if (importedOtTotal > 0)
                {
                    var otNote = $"Pending OT import {importedOtTotal:0.##}h";
                    notes = string.IsNullOrWhiteSpace(notes) ? otNote : $"{notes} · {otNote}";
                }

                return new ImportTimeCardFromDtrRowResult
                {
                    EmployeeId = r.EmployeeId,
                    LateMinutes = r.LateMinutes,
                    UndertimeMinutes = r.UndertimeMinutes,
                    AbsentHours = r.AbsentHours,
                    Notes = notes,
                    HoursByDayTypeName = hoursByDayType,
                    NightDifferentialHoursByDayTypeName = ndByDayType,
                    OvertimeHoursByDayTypeName = otByDayType,
                    NightDifferentialOvertimeHoursByDayTypeName = ndOtByDayType
                };
            }).ToList();

            var otEmployeeCount = otByEmployee.Count;
            var otHoursTotal = otByEmployee.Values.Sum(m => m.Values.Sum(b => b.TotalHours));

            return Json(new
            {
                success = true,
                message = otEmployeeCount > 0
                    ? $"Imported DTR data for {rows.Count} employee(s), including {otHoursTotal:0.##}h approved Pending OT for {otEmployeeCount} employee(s). Review and save the group."
                    : $"Imported DTR data for {rows.Count} employee(s). No approved Pending OT found in this period for the selected employees. Review and save the group.",
                rows,
                dayTypes,
                pendingOvertimeEmployees = otEmployeeCount,
                pendingOvertimeHours = otHoursTotal
            });
        }

        private static List<string> GetDuplicateDayTypeErrors(IEnumerable<AssignEmployeesAndTimeCardRowViewModel> rows)
            => GetDuplicateDayTypeErrorsCore(rows, r => r.EmployeeName, r => r.Details);

        private static List<string> GetDuplicateDayTypeErrors(IEnumerable<PayrollPeriodTimeCardRowViewModel> rows)
            => GetDuplicateDayTypeErrorsCore(rows, r => r.EmployeeName, r => r.Details);

        private static List<string> GetDuplicateDayTypeErrorsCore<T>(
            IEnumerable<T> rows,
            Func<T, string> getName,
            Func<T, List<TimeCardDetailRowViewModel>?> getDetails)
        {
            var errors = new List<string>();
            foreach (var row in rows)
            {
                var details = getDetails(row);
                if (details == null || details.Count == 0) continue;
                var duplicates = details.GroupBy(d => d.DayTypeId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                if (duplicates.Any())
                    errors.Add($"'{getName(row)}' has duplicate day type(s) (IDs: {string.Join(", ", duplicates)}).");
            }
            return errors;
        }

        private static void SyncDetails(
            PayrollPeriodTimeCardEntry tc,
            List<TimeCardDetailRowViewModel> vmDetails,
            string? userId)
        {
            if (vmDetails == null || !vmDetails.Any())
                return;

            var merged = vmDetails
                .GroupBy(v => v.DayTypeId)
                .Select(g => new TimeCardDetailRowViewModel
                {
                    DayTypeId = g.Key,
                    DayTypeName = g.First().DayTypeName,
                    RegularHours = g.Sum(x => x.RegularHours),
                    OvertimeHours = g.Sum(x => x.OvertimeHours),
                    NightDifferentialHours = g.Sum(x => x.NightDifferentialHours),
                    NightDifferentialOvertimeHours = g.Sum(x => x.NightDifferentialOvertimeHours)
                })
                .ToList();

            foreach (var vm in merged)
            {
                var existing = tc.Details.FirstOrDefault(d => d.DayTypeId == vm.DayTypeId);

                bool allZero = vm.RegularHours == 0
                            && vm.OvertimeHours == 0
                            && vm.NightDifferentialHours == 0
                            && vm.NightDifferentialOvertimeHours == 0;

                if (allZero)
                {
                    if (existing != null) tc.Details.Remove(existing);
                    continue;
                }

                if (existing == null)
                {
                    existing = new PayrollPeriodTimeCardDetail { DayTypeId = vm.DayTypeId };
                    tc.Details.Add(existing);
                }

                existing.RegularHours = vm.RegularHours;
                existing.OvertimeHours = vm.OvertimeHours;
                existing.NightDifferentialHours = vm.NightDifferentialHours;
                existing.NightDifferentialOvertimeHours = vm.NightDifferentialOvertimeHours;
            }

            var submittedDayTypeIds = merged.Select(v => v.DayTypeId).ToHashSet();
            var stale = tc.Details.Where(d => !submittedDayTypeIds.Contains(d.DayTypeId)).ToList();
            foreach (var s in stale)
                tc.Details.Remove(s);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> DeleteBatch(int batchId)
        {
            var batch = await _db.PayrollPeriodBatches
                .FirstOrDefaultAsync(b => b.Id == batchId && !b.IsDeleted);

            if (batch == null)
                return Json(new { success = false, message = "Employee group not found." });

            var period = await _db.PayrollPeriods
                .FirstOrDefaultAsync(pp => pp.Id == batch.PayrollPeriodId && !pp.IsDeleted);

            if (period == null)
                return Json(new { success = false, message = "Payroll period not found." });

            if (period.IsLocked)
                return Json(new { success = false, message = "Payroll period is locked." });

            var hasCompletedRun = await _db.PayrollRuns
                .AnyAsync(pr => pr.PayrollPeriodId == batch.PayrollPeriodId
                             && pr.Status == PayrollStatus.Completed);

            if (hasCompletedRun)
                return Json(new { success = false, message = "A completed payroll run exists for this period. Employee groups cannot be deleted." });

            var timeCardsInBatch = await _db.PayrollPeriodTimeCards
                .Where(tc => tc.PayrollPeriodId == batch.PayrollPeriodId && tc.PayrollPeriodBatchId == batchId)
                .ToListAsync();

            var affectedEmployeeIds = timeCardsInBatch.Select(tc => tc.EmployeeId).ToList();

            if (timeCardsInBatch.Any())
                _db.PayrollPeriodTimeCards.RemoveRange(timeCardsInBatch);

            await _db.SaveChangesAsync();

            foreach (var empId in affectedEmployeeIds)
            {
                var inOtherBatch = await _db.PayrollPeriodTimeCards
                    .AnyAsync(t => t.PayrollPeriodId == batch.PayrollPeriodId && t.EmployeeId == empId);
                if (!inOtherBatch)
                {
                    var ppe = await _db.PayrollPeriodEmployees
                        .FirstOrDefaultAsync(p => p.PayrollPeriodId == batch.PayrollPeriodId && p.EmployeeId == empId);
                    if (ppe != null)
                        _db.PayrollPeriodEmployees.Remove(ppe);
                }
            }

            batch.IsDeleted = true;
            _db.PayrollPeriodBatches.Update(batch);

            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Employee group deleted successfully." });
        }

        [HttpGet]
        [Permission("PAYROLL_PERIOD_VIEW")]
        public async Task<IActionResult> ViewAllEmployees(int id)
        {
            var payrollPeriod = await _db.PayrollPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(pp => pp.Id == id && !pp.IsDeleted);

            if (payrollPeriod == null)
                return NotFound();

            var timeCards = await _db.PayrollPeriodTimeCards
                .Where(tc => tc.PayrollPeriodId == id)
                .Include(tc => tc.Employee)
                .Include(tc => tc.Details).ThenInclude(d => d.DayType)
                .ToListAsync();

            var viewUnitInfo = await EmployeeOrganizationHelper.GetActiveUnitInfoAsync(
                _db, timeCards.Select(tc => tc.EmployeeId));

            var batches = await _db.PayrollPeriodBatches
                .Where(b => b.PayrollPeriodId == id && !b.IsDeleted)
                .AsNoTracking()
                .ToListAsync();

            var batchDict = batches.ToDictionary(b => b.Id);

            var vm = new PayrollPeriodEmployeeListViewModel
            {
                PayrollPeriodId = payrollPeriod.Id,
                PayrollPeriodName = payrollPeriod.Name,
                PeriodRange = payrollPeriod.PeriodRange,
                IsLocked = payrollPeriod.IsLocked,
                Rows = timeCards.Select(tc =>
                {
                    batchDict.TryGetValue(tc.PayrollPeriodBatchId ?? 0, out var batch);
                    viewUnitInfo.TryGetValue(tc.EmployeeId, out var info);
                    return new PayrollPeriodEmployeeListRowViewModel
                    {
                        EmployeeId = tc.EmployeeId,
                        EmployeeName = tc.Employee?.FullName ?? "",
                        OrganizationUnitName = info.Names ?? "",
                        EmploymentTypeName = tc.Employee?.EmploymentBasis.ToString() ?? "",
                        BatchId = tc.PayrollPeriodBatchId ?? 0,
                        BatchLabel = batch != null
                            ? $"Group {batches.IndexOf(batch) + 1} — {batch.DepartmentDisplay}" : "—",
                        LateMinutes = tc.LateMinutes,
                        UndertimeMinutes = tc.UndertimeMinutes,
                        AbsentHours = tc.AbsentHours,
                        Notes = tc.Notes,
                        Details = tc.Details
                            .OrderBy(d => d.DayType?.Name)
                            .Select(d => new TimeCardDetailRowViewModel
                            {
                                DayTypeId = d.DayTypeId,
                                DayTypeName = d.DayType?.Name ?? "",
                                RegularHours = d.RegularHours,
                                OvertimeHours = d.OvertimeHours,
                                NightDifferentialHours = d.NightDifferentialHours,
                                NightDifferentialOvertimeHours = d.NightDifferentialOvertimeHours
                            }).ToList()
                    };
                })
                .OrderBy(r => r.OrganizationUnitName).ThenBy(r => r.EmployeeName)
                .ToList()
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("Partials/_ViewAllEmployees", vm);

            return View("ViewAllEmployees", vm);
        }
    }
}