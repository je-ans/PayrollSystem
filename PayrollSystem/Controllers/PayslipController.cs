using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Services;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    public class PayslipController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<PayslipController> _logger;
        private readonly IPayrollComputationService _payrollComputation;

        public PayslipController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<PayslipController> logger,
            IPayrollComputationService payrollComputation)
        {
            _userManager = userManager;
            _db = db;
            _auditHelper = auditHelper;
            _logger = logger;
            _payrollComputation = payrollComputation;
        }

        //=======================================================Private Helpers=================================================================

        private async Task LogAuditTrailAsync(string action, object oldData, object newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "Payslip", "Payslips", oldData, newData);
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

        private async Task<bool> IsEmployeeOnlyUserAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return false;

            var roles = await _userManager.GetRolesAsync(user);
            var isEmployee = roles.Contains("Employee", StringComparer.OrdinalIgnoreCase);
            var isAdmin = roles.Contains("Admin", StringComparer.OrdinalIgnoreCase) ||
                          roles.Any(r => string.Equals(r, "HR", StringComparison.OrdinalIgnoreCase));

            return isEmployee && !isAdmin;
        }

        private async Task<Employee?> GetCurrentUserEmployeeAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            return await _db.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == user.Id && !e.IsDeleted && e.IsActive);
        }

        // Every action that needs a PayrollEntry for payslip display should use this helper
        private IQueryable<PayrollEntry> PayrollEntryWithPayslipIncludes()
        {
            return _db.PayrollEntries
                .Include(pe => pe.Employee)
                    .ThenInclude(e => e!.Designations.Where(d => d.EffectiveTo == null))
                    .ThenInclude(d => d.Designation)
                .Include(pe => pe.Employee)
                    .ThenInclude(e => e.DefaultRateType)
                .Include(pe => pe.PayrollRun)
                    .ThenInclude(pr => pr.PayrollPeriod)
                .Include(pe => pe.Deductions)
                .Include(pe => pe.GovernmentContributions);
        }

        //=======================================================Public Methods=================================================================

        public async Task<IActionResult> Index()
        {
            var isEmployeeOnly = await IsEmployeeOnlyUserAsync();
            if (!isEmployeeOnly && !PermissionHelper.HasPermission(User, "PAYSLIP_VIEW"))
                return RedirectToAction("AccessDenied", "Account");

            ViewBag.IsEmployeeOnly = isEmployeeOnly;

            if (!isEmployeeOnly)
            {
                var employees = await _db.Employees
                    .Where(e => !e.IsDeleted && e.IsActive)
                    .Select(e => new { e.Id, e.Surname, e.FirstName, e.MiddleName, e.EmployeeIdNumber })
                    .AsNoTracking()
                    .ToListAsync();

                ViewBag.Employees = employees
                    .Select(e => new
                    {
                        Id = e.Id,
                        Name = $"{e.Surname}, {e.FirstName} {e.MiddleName}".Trim() + $" ({e.EmployeeIdNumber})"
                    })
                    .OrderBy(e => e.Name)
                    .ToList();
            }
            else
            {
                var employee = await GetCurrentUserEmployeeAsync();
                if (employee == null)
                    return RedirectToAction("AccessDenied", "Account");

                ViewBag.CurrentEmployeeId = employee.Id;
            }

            var viewModel = new PayslipViewModel();
            return View(viewModel);
        }

        // View payslip by selecting an employee and payroll period. Redirects to ViewPayslipDocument.
        [HttpGet]
        public async Task<IActionResult> ViewByEmployeeAndPeriod(Guid? employeeId, int? payrollPeriodId)
        {
            if (!employeeId.HasValue || !payrollPeriodId.HasValue)
            {
                TempData["error"] = "Please select both employee and payroll period.";
                return RedirectToAction(nameof(Index));
            }

            var payrollRun = await _db.PayrollRuns
                .Include(pr => pr.PayrollEntries)
                .Where(pr => pr.PayrollPeriodId == payrollPeriodId.Value
                          && pr.Status == PayrollStatus.Completed
                          && !pr.IsDeleted)
                .OrderByDescending(pr => pr.ProcessedDate)
                .FirstOrDefaultAsync();

            if (payrollRun == null)
            {
                TempData["error"] = "No completed payroll run found for the selected period.";
                return RedirectToAction(nameof(Index));
            }

            var payrollEntry = payrollRun.PayrollEntries
                .FirstOrDefault(pe => pe.EmployeeId == employeeId.Value && pe.IsProcessed);

            if (payrollEntry == null)
            {
                TempData["error"] = "No payroll entry found for this employee in the selected period.";
                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction(nameof(ViewPayslipDocument), new { payrollEntryId = payrollEntry.Id });
        }

        // Returns payslip document content (partial HTML) for injection into the Index document preview.
        [HttpGet]
        public async Task<IActionResult> GetPayslipDocumentByEmployeeAndPeriod(Guid? employeeId, int? payrollPeriodId)
        {
            var isEmployeeOnly = await IsEmployeeOnlyUserAsync();
            Guid targetEmployeeId;

            if (isEmployeeOnly)
            {
                var employee = await GetCurrentUserEmployeeAsync();
                if (employee == null)
                    return Forbid();
                targetEmployeeId = employee.Id;
            }
            else
            {
                if (!employeeId.HasValue)
                    return BadRequest("Employee is required.");
                targetEmployeeId = employeeId.Value;
            }

            if (!payrollPeriodId.HasValue)
                return BadRequest("Payroll period is required.");

            // Find the completed run for this period
            var payrollRun = await _db.PayrollRuns
                .Include(pr => pr.PayrollEntries)
                .Where(pr => pr.PayrollPeriodId == payrollPeriodId.Value
                          && pr.Status == PayrollStatus.Completed
                          && !pr.IsDeleted)
                .OrderByDescending(pr => pr.ProcessedDate)
                .FirstOrDefaultAsync();

            if (payrollRun == null)
                return NotFound();

            var entryShell = payrollRun.PayrollEntries
                .FirstOrDefault(pe => pe.EmployeeId == targetEmployeeId && pe.IsProcessed);

            if (entryShell == null)
                return NotFound();

            // Load full entry with all payslip includes (including GovernmentContributions)
            var payrollEntryFull = await PayrollEntryWithPayslipIncludes()
                .FirstOrDefaultAsync(pe => pe.Id == entryShell.Id && !pe.IsDeleted);

            if (payrollEntryFull == null)
                return NotFound();

            var viewModel = await BuildPayslipViewModelAsync(payrollEntryFull);
            return PartialView("Partials/_PayslipDocumentContent", viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeesForPayslip()
        {
            var employees = await _db.Employees
                .Where(e => !e.IsDeleted)
                .OrderBy(e => e.Surname).ThenBy(e => e.FirstName)
                .Select(e => new
                {
                    id = e.Id,
                    name = e.FirstName + (e.MiddleName != null ? " " + e.MiddleName : "") + " " + e.Surname,
                    employeeIdNumber = e.EmployeeIdNumber
                })
                .ToListAsync();

            return Json(new { success = true, data = employees });
        }

        [HttpGet]
        public async Task<IActionResult> GetPayrollPeriodsForPayslip()
        {
            var periodIds = await _db.PayrollRuns
                .Where(pr => pr.Status == PayrollStatus.Completed && !pr.IsDeleted)
                .Select(pr => pr.PayrollPeriodId)
                .Distinct()
                .ToListAsync();

            var periods = await _db.PayrollPeriods
                .Where(pp => periodIds.Contains(pp.Id) && !pp.IsDeleted)
                .OrderByDescending(pp => pp.PeriodEndDate)
                .Select(pp => new { id = pp.Id, name = pp.Name })
                .ToListAsync();

            return Json(new { success = true, data = periods });
        }

        [HttpGet]
        public IActionResult MyPayslips()
        {
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ViewPayslip(int? id, int? payrollEntryId)
        {
            PayrollEntry? payrollEntry = null;

            if (payrollEntryId.HasValue)
            {
                payrollEntry = await PayrollEntryWithPayslipIncludes()
                    .FirstOrDefaultAsync(pe => pe.Id == payrollEntryId.Value && !pe.IsDeleted);
            }
            else if (id.HasValue)
            {
                var payslip = await _db.Payslips
                    .Include(p => p.PayrollEntry)
                        .ThenInclude(pe => pe.PayrollRun)
                            .ThenInclude(pr => pr.PayrollPeriod)
                    .Include(p => p.PayrollEntry)
                        .ThenInclude(pe => pe.Employee)
                            .ThenInclude(e => e!.Designations.Where(d => d.EffectiveTo == null))
                    .ThenInclude(d => d.Designation)
                    .Include(p => p.PayrollEntry)
                        .ThenInclude(pe => pe.Employee)
                            .ThenInclude(e => e.DefaultRateType)
                    .Include(p => p.PayrollEntry)
                        .ThenInclude(pe => pe.Deductions)
                    .Include(p => p.PayrollEntry)
                        .ThenInclude(pe => pe.GovernmentContributions)
                    .FirstOrDefaultAsync(p => p.Id == id.Value && !p.IsDeleted);

                if (payslip != null)
                    payrollEntry = payslip.PayrollEntry;
                else
                {
                    payrollEntry = await PayrollEntryWithPayslipIncludes()
                        .FirstOrDefaultAsync(pe => pe.Id == id.Value && !pe.IsDeleted);
                }
            }

            if (payrollEntry == null)
                return NotFound();

            var viewModel = await BuildPayslipViewModelAsync(payrollEntry);
            return PartialView("ViewPayslip", viewModel);
        }

        // Full page that displays a payslip inside _DocumentPreview (zoom, print, export).
        [HttpGet]
        public async Task<IActionResult> ViewPayslipDocument(int? payrollEntryId)
        {
            if (!payrollEntryId.HasValue)
                return NotFound();

            var payrollEntry = await PayrollEntryWithPayslipIncludes()
                .FirstOrDefaultAsync(pe => pe.Id == payrollEntryId.Value && !pe.IsDeleted);

            if (payrollEntry == null)
                return NotFound();

            var viewModel = await BuildPayslipViewModelAsync(payrollEntry);
            return View("ViewPayslipDocument", viewModel);
        }

        // Returns only the payslip document content (partial HTML) for injection into _DocumentPreview.
        [HttpGet]
        public async Task<IActionResult> GetPayslipDocument(int? payrollEntryId)
        {
            if (!payrollEntryId.HasValue)
                return NotFound();

            var payrollEntry = await PayrollEntryWithPayslipIncludes()
                .FirstOrDefaultAsync(pe => pe.Id == payrollEntryId.Value && !pe.IsDeleted);

            if (payrollEntry == null)
                return NotFound();

            var viewModel = await BuildPayslipViewModelAsync(payrollEntry);
            return PartialView("Partials/_PayslipDocumentContent", viewModel);
        }

        //=======================================================Build ViewModel=================================================================

        private async Task<PayslipViewModel> BuildPayslipViewModelAsync(PayrollEntry payrollEntry)
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

            // Days on duty (timecard-based)
            // If timecard is present, use total regular hours / 8 as "days on duty".
            // This keeps payslip independent from attendance tables.
            var payrollPeriodId = payrollEntry.PayrollRun.PayrollPeriodId;
            var timeCardForDays = await _db.PayrollPeriodTimeCards
                .Where(tc => tc.PayrollPeriodId == payrollPeriodId && tc.EmployeeId == payrollEntry.EmployeeId)
                .Include(tc => tc.Details)
                .FirstOrDefaultAsync();

            var totalRegularHours = timeCardForDays?.Details?.Sum(d => d.RegularHours) ?? 0M;
            var daysOnDuty = totalRegularHours > 0 ? (int)decimal.Ceiling(totalRegularHours / 8M) : 0;

            // Rate
            var rateType = payrollEntry.Employee.DefaultRateType;
            decimal? rate = null;
            string? rateBasis = null;

            if (rateType != null)
            {
                rateBasis = rateType.RateBasis.ToString();
                rate = rateType.RateBasis switch
                {
                    RateBasis.Monthly => rateType.MonthlySalary,
                    RateBasis.Daily => rateType.DailyRate,
                    _ => rateType.HourlyRate ?? payrollEntry.HourlyRate
                };
            }
            else
            {
                rate = payrollEntry.HourlyRate;
                rateBasis = "Hourly";
            }

            // Allowances
            var allowances = await _db.EmployeeAllowances
                .Include(a => a.Allowance)
                .Where(a => a.EmployeeId == payrollEntry.EmployeeId && a.IsActive && !a.IsDeleted &&
                            a.Allowance != null && a.Allowance.IsActive && !a.Allowance.IsDeleted)
                .ToListAsync();

            // Day-type breakdown (from time card) 
            var timeCard = await _db.PayrollPeriodTimeCards
                .Where(tc => tc.PayrollPeriodId == payrollPeriodId && tc.EmployeeId == payrollEntry.EmployeeId)
                .Include(tc => tc.Details).ThenInclude(d => d.DayType)
                .FirstOrDefaultAsync();

            var dayTypes = await _db.DayTypes
                .AsNoTracking()
                .Where(dt => !dt.IsDeleted)
                .ToListAsync();
            var dayTypeColumns = new List<DayTypeColumnInfo>();
            var dayTypeBreakdown = new Dictionary<int, DayTypeRowBreakdown>();

            if (timeCard != null && timeCard.Details.Any() && dayTypes.Any())
            {
                var dayTypeById = dayTypes.ToDictionary(dt => dt.Id);
                var pay = _payrollComputation.ComputeFromTimeCard(
                    payrollEntry.HourlyRate,
                    timeCard.Details,
                    dayTypeById);

                foreach (var breakdown in pay.DayTypeBreakdowns)
                {
                    dayTypeColumns.Add(new DayTypeColumnInfo
                    {
                        DayTypeId = breakdown.DayTypeId,
                        DayTypeName = breakdown.DayTypeName,
                        IsRegularDay = breakdown.IsRegularDay
                    });

                    dayTypeBreakdown[breakdown.DayTypeId] = new DayTypeRowBreakdown
                    {
                        Hours = breakdown.RegularHours,
                        Amount = breakdown.BasicAndNdAmount,
                        OvertimeHours = breakdown.OvertimeHours + breakdown.NightDifferentialOvertimeHours,
                        OvertimeAmount = breakdown.OvertimeAndNdOtAmount
                    };
                }

                dayTypeColumns = dayTypeColumns
                    .OrderByDescending(dt => dt.IsRegularDay)
                    .ThenBy(dt => dt.DayTypeName)
                    .ToList();
            }

            // Government contributions 
            var govtContributions = payrollEntry.GovernmentContributions?
                .OrderBy(gc => gc.ContributionName)
                .Select(gc => new GovtContributionViewModel
                {
                    ContributionName = gc.ContributionName,
                    EmployeeShare = gc.EmployeeShare,
                    EmployerShare = gc.EmployerShare,
                    SalaryBasis = gc.SalaryBasis
                })
                .ToList()
                ?? new List<GovtContributionViewModel>();

            var unitNames = await EmployeeOrganizationHelper.GetActiveUnitDisplayNamesAsync(
                _db, new[] { payrollEntry.EmployeeId });

            // Build view model
            return new PayslipViewModel
            {
                Id = payrollEntry.Id,
                PayrollEntryId = payrollEntry.Id,
                PayslipNumber = $"PAY-{payrollEntry.PayrollRunId}-{payrollEntry.Id}",
                GeneratedDate = payrollEntry.CreatedOn,

                EmployeeName = payrollEntry.Employee.FullName,
                EmployeeIdNumber = payrollEntry.Employee.EmployeeIdNumber,
                OrganizationUnitName = unitNames.GetValueOrDefault(payrollEntry.EmployeeId, "N/A"),
                DesignationName = payrollEntry.Employee.Designations
                    .Where(d => d.EffectiveTo == null)
                    .Select(d => d.Designation!.DesignationName)
                    .FirstOrDefault() ?? "N/A",
                RateTypeName = payrollEntry.Employee.DefaultRateType?.Name ?? "N/A",

                PeriodStartDate = payrollEntry.PayrollRun.PayrollPeriod.PeriodStartDate,
                PeriodEndDate = payrollEntry.PayrollRun.PayrollPeriod.PeriodEndDate,
                PayDate = payrollEntry.PayrollRun.PayrollPeriod.PayDate,
                PayrollPeriodName = payrollEntry.PayrollRun.PayrollPeriod.Name,

                Rate = rate,
                RateBasis = rateBasis,
                DaysOnDuty = (decimal)daysOnDuty,

                RegularPay = payrollEntry.RegularPay,
                OvertimePay = payrollEntry.OvertimePay,
                HolidayPay = payrollEntry.HolidayPay,
                RestDayPay = payrollEntry.RestDayPay,
                NightDifferentialPay = payrollEntry.NightDifferentialPay,

                DayTypeColumns = dayTypeColumns,
                DayTypeBreakdown = dayTypeBreakdown,

                GrossPay = payrollEntry.GrossPay,
                TotalBenefits = payrollEntry.TotalBenefits,
                TotalDeductions = payrollEntry.TotalDeductions,
                NetPay = payrollEntry.NetPay,
                Notes = payrollEntry.Notes,

                Benefits = allowances.Select(a => new BenefitViewModel
                {
                    Id = a.Id,
                    PayrollEntryId = payrollEntry.Id,
                    BenefitType = BenefitType.Other,
                    Description = a.Allowance!.AllowanceName,
                    Amount = a.Allowance!.Amount
                }).ToList(),

                Deductions = payrollEntry.Deductions?.Select(d => new DeductionViewModel
                {
                    Id = d.Id,
                    PayrollEntryId = d.PayrollEntryId,
                    DeductionTypeId = d.DeductionTypeId,
                    DeductionTypeName = d.DeductionType != null ? d.DeductionType.Name : null,
                    Description = d.Description,
                    Amount = d.Amount
                }).ToList(),

                GovernmentContributions = govtContributions,

                // Company profile — sourced from active PayrollConfiguration 
                CompanyName = config?.CompanyName ?? string.Empty,
                CompanyShortName = config?.CompanyShortName,
                CompanyLogoPath = config?.CompanyLogoPath,
                CompanyAddress = BuildAddress(config),
                CompanyPhone = config?.CompanyContactNumber ?? string.Empty,
                CompanyEmail = config?.CompanyEmail ?? string.Empty,
                CompanyWebsite = config?.CompanyWebsite,
            };
        }

        //=======================================================AJAX Methods=================================================================

        [HttpGet]
        public async Task<IActionResult> GetPayslips(int? payrollRunId = null)
        {
            try
            {
                var query = _db.PayrollEntries
                    .Include(pe => pe.Employee)
                    .Include(pe => pe.PayrollRun)
                        .ThenInclude(pr => pr.PayrollPeriod)
                    .Where(pe => !pe.IsDeleted && pe.IsProcessed);

                if (payrollRunId.HasValue)
                    query = query.Where(pe => pe.PayrollRunId == payrollRunId.Value);

                var rows = await query
                    .OrderByDescending(pe => pe.CreatedOn)
                    .Select(pe => new
                    {
                        id = pe.Id,
                        payrollEntryId = pe.Id,
                        payslipNumber = $"PAY-{pe.PayrollRunId}-{pe.Id}",
                        employeeId = pe.EmployeeId,
                        employeeName = pe.Employee.FirstName
                                         + (pe.Employee.MiddleName != null ? " " + pe.Employee.MiddleName : "")
                                         + " " + pe.Employee.Surname,
                        employeeIdNumber = pe.Employee.EmployeeIdNumber,
                        payrollPeriodName = pe.PayrollRun.PayrollPeriod != null ? pe.PayrollRun.PayrollPeriod.Name : "N/A",
                        netPay = pe.NetPay,
                        status = "Available",
                        generatedDate = pe.CreatedOn.HasValue ? pe.CreatedOn.Value.ToString("yyyy-MM-dd HH:mm") : "",
                        sentDate = (string?)null
                    })
                    .ToListAsync();

                var unitNames = await EmployeeOrganizationHelper.GetActiveUnitDisplayNamesAsync(
                    _db, rows.Select(r => r.employeeId));

                var payslips = rows.Select(r => new
                {
                    r.id,
                    r.payrollEntryId,
                    r.payslipNumber,
                    r.employeeName,
                    r.employeeIdNumber,
                    organizationUnitName = unitNames.GetValueOrDefault(r.employeeId, "N/A"),
                    r.payrollPeriodName,
                    r.netPay,
                    r.status,
                    r.generatedDate,
                    r.sentDate
                }).ToList();

                return Json(new { success = true, data = payslips });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payslips");
                return Json(new { success = false, message = "Error retrieving payslips" });
            }
        }
    }
}