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
    public class PayrollController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<PayrollController> _logger;
        private readonly IPayrollService _payrollService;
        private readonly IPayrollComputationService _payrollComputation;

        public PayrollController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<PayrollController> logger,
            IPayrollService payrollService,
            IPayrollComputationService payrollComputation)
        {
            _userManager = userManager;
            _db = db;
            _auditHelper = auditHelper;
            _logger = logger;
            _payrollService = payrollService;
            _payrollComputation = payrollComputation;
        }

        //=======================================================Private Helper Methods=================================================================

        // Builds day-type columns from used time cards and populates per-employee breakdown
        private static (IList<DayTypeColumnInfo> columns, Dictionary<Guid, List<(PayrollPeriodTimeCardDetail Detail, DayType DayType)>> detailByEmployee)
            BuildDayTypeColumnsAndDetails(
                List<PayrollPeriodTimeCardEntry> timeCardsWithDetails,
                List<DayType> dayTypes)
        {
            var dayTypeById = dayTypes.ToDictionary(dt => dt.Id);
            var usedDayTypeIds = timeCardsWithDetails
                .SelectMany(tc => tc.Details.Select(d => d.DayTypeId))
                .Distinct()
                .ToList();

            var dayTypeInfos = usedDayTypeIds
                .Where(dayTypeById.ContainsKey)
                .Select(id => dayTypeById[id])
                .Select(dt => new DayTypeColumnInfo
                {
                    DayTypeId = dt.Id,
                    DayTypeName = dt.Name,
                    IsRegularDay = dt.Name.Contains("Regular", StringComparison.OrdinalIgnoreCase)
                                   && dt.BasicMultiplier == 1.0M
                })
                .OrderByDescending(dt => dt.IsRegularDay)
                .ThenBy(dt => dt.DayTypeName)
                .ToList();

            var detailByEmployee = new Dictionary<Guid, List<(PayrollPeriodTimeCardDetail Detail, DayType DayType)>>();
            foreach (var tc in timeCardsWithDetails)
            {
                var list = tc.Details
                    .Where(d => dayTypeById.ContainsKey(d.DayTypeId))
                    .Select(d => (d, dayTypeById[d.DayTypeId]))
                    .ToList();
                detailByEmployee[tc.EmployeeId] = list;
            }

            return (dayTypeInfos, detailByEmployee);
        }

        private async Task LogAuditTrailAsync(string action, object? oldData, object? newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "PayrollRun", "PayrollRuns", oldData, newData);
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

        // Loads all processed PayrollEntries for a run — including GovernmentContributions —
        // and writes the correct summary totals back onto payrollRun.

        // WHY THIS EXISTS:
        //   PayrollEntry.TotalDeductions now stores only tax + attendance deductions.
        //   Government contributions live in PayrollEntryGovernmentContributions.
        //   The PayrollRun summary must reflect the FULL picture so that:
        //     TotalNetPay  =  TotalGrossPay + TotalBenefits - TotalDeductions (run-level)
        //   where TotalDeductions at run-level = tax + attendance + govt-contribution employee shares.
        private async Task ApplyPayrollRunSummaryAsync(PayrollRun payrollRun)
        {
            var entries = await _db.PayrollEntries
                .Where(pe => pe.PayrollRunId == payrollRun.Id)
                .Include(pe => pe.GovernmentContributions)
                .ToListAsync();

            payrollRun.TotalEmployees = entries.Count;
            payrollRun.TotalGrossPay = entries.Sum(pe => pe.GrossPay);
            payrollRun.TotalBenefits = entries.Sum(pe => pe.TotalBenefits);

            // Run-level TotalDeductions = per-entry (tax + absence + late/undertime)
            //                           + all govt-contribution employee shares
            payrollRun.TotalDeductions = entries.Sum(pe =>
                pe.TotalDeductions +
                pe.GovernmentContributions.Sum(gc => gc.EmployeeShare));

            payrollRun.TotalNetPay = entries.Sum(pe => pe.NetPay);
        }

        //=======================================================Public Methods=================================================================

        [Permission("PAYROLL_VIEW")]
        public IActionResult Index()
        {
            var viewModel = new PayrollRunViewModel();
            return View(viewModel);
        }

        [HttpGet]
        [Permission("PAYROLL_VIEW")]
        public async Task<IActionResult> Details(int id)
        {
            var payrollRun = await _db.PayrollRuns
                .Include(pr => pr.PayrollPeriod)
                .Include(pr => pr.ProcessedByUser)
                .Include(pr => pr.PayrollEntries)
                    .ThenInclude(pe => pe.Employee)
                .FirstOrDefaultAsync(pr => pr.Id == id && !pr.IsDeleted);

            if (payrollRun == null)
                return NotFound();

            var viewModel = new PayrollRunViewModel
            {
                Id = payrollRun.Id,
                Name = payrollRun.Name,
                Description = payrollRun.Description,
                Status = payrollRun.Status,
                ProcessedDate = payrollRun.ProcessedDate,
                ProcessedByUserName = payrollRun.ProcessedByUser?.FullName,
                TotalEmployees = payrollRun.TotalEmployees,
                TotalGrossPay = payrollRun.TotalGrossPay,
                TotalDeductions = payrollRun.TotalDeductions,
                TotalNetPay = payrollRun.TotalNetPay,
                TotalBenefits = payrollRun.TotalBenefits,
                PayrollPeriodId = payrollRun.PayrollPeriodId,
                PayrollPeriodName = payrollRun.PayrollPeriod?.Name,
                Notes = payrollRun.Notes,
                IsFinalized = payrollRun.IsFinalized,
                PayrollEntries = payrollRun.PayrollEntries?.Select(pe => new PayrollEntryViewModel
                {
                    Id = pe.Id,
                    EmployeeName = pe.Employee?.FullName,
                    GrossPay = pe.GrossPay,
                    NetPay = pe.NetPay,
                    TotalBenefits = pe.TotalBenefits,
                    TotalDeductions = pe.TotalDeductions
                }).ToList()
            };

            return PartialView("Partials/_Details", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("PAYROLL_VIEW")]
        public async Task<IActionResult> ProcessPayroll(int id)
        {
            try
            {
                var payrollRun = await _db.PayrollRuns
                    .Include(pr => pr.PayrollPeriod)
                        .ThenInclude(pp => pp.PayrollPeriodEmployees)
                    .FirstOrDefaultAsync(pr => pr.Id == id && !pr.IsDeleted);

                if (payrollRun == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "Payroll run not found." });
                    TempData["error"] = "Payroll run not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (payrollRun.Status != PayrollStatus.Draft)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "Only draft payroll runs can be processed." });
                    TempData["error"] = "Only draft payroll runs can be processed.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var existingRunSnapshot = JsonSerializer.Serialize(new
                {
                    payrollRun.Id,
                    payrollRun.Name,
                    payrollRun.Status,
                    PayrollPeriodId = payrollRun.PayrollPeriodId,
                    payrollRun.CreatedOn
                });

                payrollRun.Status = PayrollStatus.Processing;
                payrollRun.UpdatedById = (await _userManager.GetUserAsync(User))?.Id;
                payrollRun.UpdatedOn = DateTime.Now;

                _db.PayrollRuns.Update(payrollRun);
                await _db.SaveChangesAsync();

                // ── Run calculations (creates PayrollEntry + GovernmentContributions rows) ──
                await _payrollService.ProcessPayrollCalculationsAsync(payrollRun);

                // ── Update PayrollRun summary totals ──────────────────────────────────────
                // MUST load GovernmentContributions here; a plain Sum(pe.TotalDeductions)
                // would miss govt-contribution employee shares and understate TotalDeductions.
                await ApplyPayrollRunSummaryAsync(payrollRun);

                var currentUser = await _userManager.GetUserAsync(User);
                payrollRun.Status = PayrollStatus.Completed;
                payrollRun.ProcessedByUserId = currentUser?.Id;
                payrollRun.ProcessedDate = DateTime.Now;
                payrollRun.UpdatedById = currentUser?.Id;
                payrollRun.UpdatedOn = DateTime.Now;

                _db.PayrollRuns.Update(payrollRun);
                await _db.SaveChangesAsync();

                await LogAuditTrailAsync("PROCESS_START", existingRunSnapshot, payrollRun);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true, message = "Payroll processing completed successfully." });

                TempData["success"] = "Payroll processing completed successfully.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payroll run {Id}", id);
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = $"An error occurred while processing the payroll: {ex.Message}" });
                TempData["error"] = "An error occurred while processing the payroll.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("PAYROLL_VIEW")]
        public async Task<IActionResult> FinalizePayroll(int id)
        {
            try
            {
                var payrollRun = await _db.PayrollRuns
                    .FirstOrDefaultAsync(pr => pr.Id == id && !pr.IsDeleted);

                if (payrollRun == null)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "Payroll run not found." });
                    TempData["error"] = "Payroll run not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (payrollRun.Status != PayrollStatus.Completed)
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = "Only completed payroll runs can be finalized." });
                    TempData["error"] = "Only completed payroll runs can be finalized.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var existingRunSnapshot = JsonSerializer.Serialize(new
                {
                    payrollRun.Id,
                    payrollRun.Name,
                    payrollRun.Status,
                    payrollRun.IsFinalized,
                    PayrollPeriodId = payrollRun.PayrollPeriodId,
                    payrollRun.CreatedOn
                });

                var currentUser = await _userManager.GetUserAsync(User);
                payrollRun.IsFinalized = true;
                payrollRun.ProcessedDate = DateTime.Now;
                payrollRun.ProcessedByUserId = currentUser?.Id;
                payrollRun.UpdatedById = currentUser?.Id;
                payrollRun.UpdatedOn = DateTime.Now;

                _db.PayrollRuns.Update(payrollRun);
                await _db.SaveChangesAsync();

                await LogAuditTrailAsync("FINALIZE", existingRunSnapshot, payrollRun);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true, message = "Payroll run finalized successfully." });

                TempData["success"] = "Payroll run finalized successfully.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finalizing payroll run {Id}", id);
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "An error occurred while finalizing the payroll." });
                TempData["error"] = "An error occurred while finalizing the payroll.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        //=======================================================Edit Hours=================================================================

        [HttpGet]
        [Permission("PAYROLL_VIEW")]
        public async Task<IActionResult> EditHours(int id)
        {
            var payrollRun = await _db.PayrollRuns
                .Include(pr => pr.PayrollPeriod)
                .Include(pr => pr.PayrollEntries)
                    .ThenInclude(pe => pe.Employee)
                .FirstOrDefaultAsync(pr => pr.Id == id && !pr.IsDeleted);

            if (payrollRun == null)
                return NotFound();

            if (payrollRun.Status != PayrollStatus.Draft)
                return BadRequest("Only draft payroll runs can be edited.");

            var orgUnitItems = await _db.OrganizationStructures
                .Where(o => o.IsActive)
                .OrderBy(o => o.Name)
                .Select(o => new SelectListItem
                {
                    Value = o.Id.ToString(),
                    Text = o.Name
                })
                .ToListAsync();

            var entryEmployeeIds = payrollRun.PayrollEntries.Select(pe => pe.EmployeeId).ToList();
            var entryUnitInfo = await EmployeeOrganizationHelper.GetActiveUnitInfoAsync(_db, entryEmployeeIds);

            var rows = payrollRun.PayrollEntries
                .OrderBy(pe => pe.Employee?.Surname)
                .ThenBy(pe => pe.Employee?.FirstName)
                .Select(pe =>
                {
                    entryUnitInfo.TryGetValue(pe.EmployeeId, out var info);
                    return new PayrollHoursRowViewModel
                    {
                        PayrollEntryId = pe.Id,
                        EmployeeName = pe.Employee?.FullName ?? string.Empty,
                        OrganizationStructureId = info.FirstUnitId,
                        OrganizationUnitName = info.Names ?? string.Empty,
                        RegularHours = pe.RegularHours,
                        OvertimeHours = pe.OvertimeHours
                    };
                })
                .ToList();

            var vm = new PayrollHoursEditViewModel
            {
                PayrollRunId = payrollRun.Id,
                PayrollRunName = payrollRun.Name,
                OrganizationUnits = orgUnitItems,
                Rows = rows
            };

            return PartialView("Partials/_EditHours", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("PAYROLL_VIEW")]
        public async Task<IActionResult> SaveHours(PayrollHoursEditViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Validation failed." });

            var payrollRun = await _db.PayrollRuns
                .FirstOrDefaultAsync(pr => pr.Id == model.PayrollRunId && !pr.IsDeleted);

            if (payrollRun == null)
                return Json(new { success = false, message = "Payroll run not found." });

            if (payrollRun.Status != PayrollStatus.Draft)
                return Json(new { success = false, message = "Only draft payroll runs can be edited." });

            var entryIds = model.Rows.Select(r => r.PayrollEntryId).ToList();
            var entries = await _db.PayrollEntries
                .Where(pe => pe.PayrollRunId == payrollRun.Id && entryIds.Contains(pe.Id))
                .ToListAsync();

            foreach (var row in model.Rows)
            {
                var entry = entries.FirstOrDefault(e => e.Id == row.PayrollEntryId);
                if (entry == null) continue;
                entry.RegularHours = row.RegularHours;
                entry.OvertimeHours = row.OvertimeHours;
            }

            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Hours updated successfully." });
        }

        //=======================================================ViewPayroll=================================================================

        [HttpGet]
        [Permission("PAYROLL_VIEW")]
        public async Task<IActionResult> ViewPayroll(int id, Guid? employeeId = null)
        {
            var payrollRun = await _db.PayrollRuns
                .Include(pr => pr.PayrollPeriod)
                .Include(pr => pr.PayrollEntries)
                    .ThenInclude(pe => pe.Employee)
                        .ThenInclude(e => e!.Designations.Where(d => d.EffectiveTo == null))
                        .ThenInclude(d => d.Designation)
                .Include(pr => pr.PayrollEntries)
                    .ThenInclude(pe => pe.Employee)
                        .ThenInclude(e => e.DefaultRateType)
                .Include(pr => pr.PayrollEntries)
                    .ThenInclude(pe => pe.Deductions)
                .Include(pr => pr.PayrollEntries)
                    .ThenInclude(pe => pe.GovernmentContributions)
                .AsSplitQuery()
                .FirstOrDefaultAsync(pr => pr.Id == id && !pr.IsDeleted);

            if (payrollRun == null)
                return NotFound();

            var viewModel = await BuildPayrollSummaryViewModelAsync(payrollRun, employeeId);
            return View("ViewPayroll", viewModel);
        }

        [HttpGet]
        [Permission("PAYROLL_VIEW")]
        public async Task<IActionResult> GetPayrollDocument(int id, Guid? employeeId = null)
        {
            var payrollRun = await _db.PayrollRuns
                .Include(pr => pr.PayrollPeriod)
                .Include(pr => pr.PayrollEntries)
                    .ThenInclude(pe => pe.Employee)
                        .ThenInclude(e => e!.Designations.Where(d => d.EffectiveTo == null))
                        .ThenInclude(d => d.Designation)
                .Include(pr => pr.PayrollEntries)
                    .ThenInclude(pe => pe.Employee)
                        .ThenInclude(e => e.DefaultRateType)
                .Include(pr => pr.PayrollEntries)
                    .ThenInclude(pe => pe.Deductions)
                .Include(pr => pr.PayrollEntries)
                    .ThenInclude(pe => pe.GovernmentContributions)
                .AsSplitQuery()
                .FirstOrDefaultAsync(pr => pr.Id == id && !pr.IsDeleted);

            if (payrollRun == null)
                return NotFound();

            var viewModel = await BuildPayrollSummaryViewModelAsync(payrollRun, employeeId);
            return PartialView("Partials/_PayrollDocumentContent", viewModel);
        }

        private async Task<PayrollSummaryViewModel> BuildPayrollSummaryViewModelAsync(
                    PayrollRun payrollRun, Guid? filterEmployeeId)
        {
            var config = await _db.PayrollConfigurations
                .Where(pc => pc.IsActive && !pc.IsDeleted)
                .OrderByDescending(pc => pc.EffectiveDate)
                .FirstOrDefaultAsync();

            static string BuildAddress(PayrollConfiguration? c)
            {
                if (c == null) return string.Empty;
                var parts = new[]
                {
                    c.CompanyAddress1,
                    c.CompanyAddress2,
                    c.CompanyCity,
                    c.CompanyProvince,
                    c.CompanyZipCode
                }.Where(s => !string.IsNullOrWhiteSpace(s));
                return string.Join(", ", parts);
            }

            var employeeIds = payrollRun.PayrollEntries.Select(pe => pe.EmployeeId).Distinct().ToList();
            var unitNames = await EmployeeOrganizationHelper.GetActiveUnitDisplayNamesAsync(_db, employeeIds);

            var timeCards = await _db.PayrollPeriodTimeCards
                .Where(tc => tc.PayrollPeriodId == payrollRun.PayrollPeriodId && employeeIds.Contains(tc.EmployeeId))
                .Select(tc => new { tc.EmployeeId, LateUndertime = tc.LateMinutes + tc.UndertimeMinutes })
                .ToDictionaryAsync(tc => tc.EmployeeId, tc => tc.LateUndertime);

            var timeCardsWithDetails = await _db.PayrollPeriodTimeCards
                .Where(tc => tc.PayrollPeriodId == payrollRun.PayrollPeriodId && employeeIds.Contains(tc.EmployeeId))
                .Include(tc => tc.Details).ThenInclude(d => d.DayType)
                .ToListAsync();

            var dayTypes = await _db.DayTypes
                .AsNoTracking()
                .Where(dt => !dt.IsDeleted)
                .ToListAsync();
            var (dayTypeColumns, detailByEmployee) = BuildDayTypeColumnsAndDetails(timeCardsWithDetails, dayTypes);

            var allowances = await _db.EmployeeAllowances
                .Include(a => a.Allowance)
                .Where(a => employeeIds.Contains(a.EmployeeId) && a.IsActive && !a.IsDeleted &&
                            a.Allowance != null && a.Allowance.IsActive && !a.Allowance.IsDeleted)
                .ToListAsync();

            var allowanceColumns = allowances
                .Select(a => a.Allowance!.AllowanceName)
                .Distinct().OrderBy(n => n).ToList();

            var deductionNames = payrollRun.PayrollEntries
                .SelectMany(pe => pe.Deductions)
                .Where(d => !d.IsDeleted)
                .Select(d => string.IsNullOrWhiteSpace(d.Description) ? d.DeductionType.ToString() : d.Description)
                .Distinct().ToList();
            var deductionOrder = new[] { "Withholding Tax", "Absence Deductions", "Late/Undertime Deductions" };
            var deductionColumns = deductionOrder.Where(deductionNames.Contains).Concat(deductionNames.Except(deductionOrder).OrderBy(n => n)).ToList();

            var govtNames = payrollRun.PayrollEntries
                .SelectMany(pe => pe.GovernmentContributions)
                .Select(gc => gc.ContributionName)
                .Distinct().ToList();
            var govtOrder = new[] { "Pag-IBIG", "PhilHealth", "SSS" };
            var govtContribColumns = govtOrder.Where(govtNames.Contains).Concat(govtNames.Except(govtOrder).OrderBy(n => n)).ToList();

            var entriesQuery = payrollRun.PayrollEntries.Where(pe => !pe.IsDeleted && pe.IsProcessed);
            if (filterEmployeeId.HasValue)
                entriesQuery = entriesQuery.Where(pe => pe.EmployeeId == filterEmployeeId.Value);

            var rows = new List<PayrollSummaryRowViewModel>();

            foreach (var pe in entriesQuery.OrderBy(pe => pe.Employee.FullName))
            {
                timeCards.TryGetValue(pe.EmployeeId, out var lateUndertime);
                var hourlyRate = pe.HourlyRate;
                var dailyRate = hourlyRate * 8M;
                var monthlyRate = dailyRate * 22M;

                var totalGovtShare = pe.GovernmentContributions.Sum(gc => gc.EmployeeShare);
                var totalAllDeductions = pe.TotalDeductions + totalGovtShare;

                var row = new PayrollSummaryRowViewModel
                {
                    PayrollEntryId = pe.Id,
                    EmployeeIdNumber = pe.Employee.EmployeeIdNumber,
                    EmployeeName = pe.Employee.FullName,
                    EmploymentStatus = pe.Employee.EmploymentBasis.ToString(),
                    Position = pe.Employee.Designations
                        .Where(d => d.EffectiveTo == null)
                        .Select(d => d.Designation!.DesignationName)
                        .FirstOrDefault() ?? "N/A",
                    OrganizationUnit = unitNames.GetValueOrDefault(pe.EmployeeId, "N/A"),
                    DateFrom = payrollRun.PayrollPeriod.PeriodStartDate,
                    DateTo = payrollRun.PayrollPeriod.PeriodEndDate,
                    MonthlyRate = monthlyRate,
                    SemiMonthlyRate = monthlyRate / 2M,
                    DailyRate = dailyRate,
                    HourlyRate = hourlyRate,
                    TotalHours = pe.TotalHours,
                    RegularHours = pe.RegularHours,
                    RegularPay = pe.RegularPay,
                    GrossPay = pe.GrossPay,
                    TotalBenefits = pe.TotalBenefits,
                    LateUndertimeMinutes = lateUndertime,
                    TotalDeductions = totalAllDeductions,
                    NetPay = pe.NetPay
                };

                if (detailByEmployee.TryGetValue(pe.EmployeeId, out var details))
                {
                    var dayTypeById = details
                        .Select(d => d.DayType)
                        .GroupBy(dt => dt.Id)
                        .ToDictionary(g => g.Key, g => g.First());
                    var detailList = details.Select(d => d.Detail).ToList();
                    var pay = _payrollComputation.ComputeFromTimeCard(hourlyRate, detailList, dayTypeById);

                    foreach (var breakdown in pay.DayTypeBreakdowns)
                    {
                        row.DayTypeBreakdown[breakdown.DayTypeId] = new DayTypeRowBreakdown
                        {
                            Hours = breakdown.RegularHours,
                            Amount = breakdown.BasicAndNdAmount,
                            OvertimeHours = breakdown.OvertimeHours + breakdown.NightDifferentialOvertimeHours,
                            OvertimeAmount = breakdown.OvertimeAndNdOtAmount
                        };
                    }
                }

                foreach (var grp in allowances
                    .Where(a => a.EmployeeId == pe.EmployeeId)
                    .GroupBy(a => a.Allowance!.AllowanceName))
                {
                    row.AllowanceBreakdown[grp.Key] = grp.Sum(a => a.Allowance!.Amount);
                }

                foreach (var grp in pe.Deductions
                    .Where(d => !d.IsDeleted)
                    .GroupBy(d => string.IsNullOrWhiteSpace(d.Description)
                        ? d.DeductionType.ToString() : d.Description))
                {
                    row.DeductionBreakdown[grp.Key] = grp.Sum(d => d.Amount);
                }

                foreach (var gc in pe.GovernmentContributions)
                    row.GovtContributionBreakdown[gc.ContributionName] = gc.EmployeeShare;

                rows.Add(row);
            }

            var allEmployees = payrollRun.PayrollEntries
                .Where(pe => !pe.IsDeleted && pe.IsProcessed)
                .OrderBy(pe => pe.Employee.FullName)
                .Select(pe => (pe.EmployeeId, pe.Employee.FullName))
                .Distinct().ToList();

            var vm = new PayrollSummaryViewModel
            {
                PayrollRunId = payrollRun.Id,
                PayrollRunName = payrollRun.Name,
                PayrollPeriodName = payrollRun.PayrollPeriod?.Name ?? "N/A",
                PeriodStartDate = payrollRun.PayrollPeriod.PeriodStartDate,
                PeriodEndDate = payrollRun.PayrollPeriod.PeriodEndDate,
                DayTypeColumns = dayTypeColumns,
                AllowanceColumns = allowanceColumns,
                DeductionColumns = deductionColumns,
                GovtContribColumns = govtContribColumns,
                Rows = rows,
                FilterEmployeeId = filterEmployeeId,
                AllEmployees = allEmployees,

                CompanyName = config?.CompanyName ?? string.Empty,
                CompanyShortName = config?.CompanyShortName,
                CompanyLogoPath = config?.CompanyLogoPath,
                CompanyAddress = BuildAddress(config),
                CompanyContactNumber = config?.CompanyContactNumber,
                CompanyEmail = config?.CompanyEmail,
                CompanyWebsite = config?.CompanyWebsite,
            };

            return vm;
        }

        //=======================================================AJAX Methods=================================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("PAYROLL_VIEW")]
        public async Task<IActionResult> FixPayrollPeriodAssignments()
        {
            try
            {
                var payrollPeriodEmployees = await _db.PayrollPeriodEmployees.ToListAsync();
                var employees = await _db.Employees.Where(e => e.IsActive && !e.IsDeleted).ToListAsync();
                var payrollPeriods = await _db.PayrollPeriods.Where(p => p.IsActive && !p.IsLocked).ToListAsync();

                if (!payrollPeriodEmployees.Any() && payrollPeriods.Any() && employees.Any())
                {
                    var activePeriod = payrollPeriods.First();
                    _logger.LogInformation(
                        "Fixing payroll period assignments for period {PeriodId} with {EmployeeCount} employees",
                        activePeriod.Id, employees.Count);

                    var currentUser = await _userManager.GetUserAsync(User);

                    var newAssignments = employees.Select(e => new PayrollPeriodEmployee
                    {
                        PayrollPeriodId = activePeriod.Id,
                        EmployeeId = e.Id,
                        CreatedById = currentUser?.Id,
                        CreatedOn = DateTime.Now
                    }).ToList();

                    _db.PayrollPeriodEmployees.AddRange(newAssignments);
                    await _db.SaveChangesAsync();

                    return Json(new { success = true, message = $"Fixed payroll period assignments. Added {newAssignments.Count} employee assignments to payroll period '{activePeriod.Name}'." });
                }

                return Json(new { success = true, message = "Payroll period assignments are already correct." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing payroll period assignments");
                return Json(new { success = false, message = "An error occurred while fixing payroll period assignments." });
            }
        }

        [HttpGet]
        [Permission("PAYROLL_VIEW")]
        public async Task<IActionResult> GetPayrollDataStatus()
        {
            try
            {
                var status = new
                {
                    PayrollPeriods = await _db.PayrollPeriods.CountAsync(p => p.IsActive && !p.IsLocked),
                    ActiveEmployees = await _db.Employees.CountAsync(e => e.IsActive && !e.IsDeleted),
                    PayrollPeriodAssignments = await _db.PayrollPeriodEmployees.CountAsync(),
                    AttendanceRecords = 0,
                    ActivePayrollConfigurations = await _db.PayrollConfigurations.CountAsync(pc => pc.IsActive),
                    DayTypes = await _db.DayTypes.CountAsync(dt => !dt.IsDeleted),
                    PayrollRuns = await _db.PayrollRuns.CountAsync()
                };

                return Json(new { success = true, data = status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payroll data status");
                return Json(new { success = false, message = "Error retrieving payroll data status" });
            }
        }

        [HttpGet]
        [Permission("PAYROLL_VIEW")]
        public async Task<IActionResult> GetPayrollRuns()
        {
            try
            {
                var payrollRuns = await _db.PayrollRuns
                    .Include(pr => pr.PayrollPeriod)
                    .Include(pr => pr.ProcessedByUser)
                    .Where(pr => !pr.IsDeleted)
                    .OrderByDescending(pr => pr.CreatedOn)
                    .Select(pr => new
                    {
                        pr.Id,
                        pr.Name,
                        PayrollPeriodName = pr.PayrollPeriod != null ? pr.PayrollPeriod.Name : "N/A",
                        Status = pr.Status.ToString(),
                        pr.TotalEmployees,
                        pr.TotalGrossPay,
                        pr.TotalNetPay,
                        ProcessedDate = pr.ProcessedDate.HasValue
                            ? pr.ProcessedDate.Value.ToString("yyyy-MM-dd HH:mm") : null,
                        pr.IsFinalized,
                        CreatedOn = pr.CreatedOn.HasValue
                            ? pr.CreatedOn.Value.ToString("yyyy-MM-dd HH:mm") : null
                    })
                    .ToListAsync();

                return Json(new { success = true, data = payrollRuns });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payroll runs");
                return Json(new { success = false, message = "Error retrieving payroll runs" });
            }
        }
    }
}