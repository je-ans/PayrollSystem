using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Services;
using PayrollSystem.Utility;

namespace PayrollSystem.Services
{
    public class PayrollService : IPayrollService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<PayrollService> _logger;
        private readonly IPayrollComputationService _payrollComputation;

        public PayrollService(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<PayrollService> logger,
            IPayrollComputationService payrollComputation)
        {
            _db = db;
            _userManager = userManager;
            _auditHelper = auditHelper;
            _logger = logger;
            _payrollComputation = payrollComputation;
        }

        public async Task ProcessPayrollCalculationsAsync(PayrollRun payrollRun)
        {
            var payrollPeriod = await _db.PayrollPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(pp => pp.Id == payrollRun.PayrollPeriodId);

            if (payrollPeriod == null)
                throw new InvalidOperationException(
                    $"Payroll period {payrollRun.PayrollPeriodId} not found for payroll run {payrollRun.Id}.");

            payrollRun.PayrollPeriod = payrollPeriod;

            var employeeIdsInPeriod = await _db.PayrollPeriodEmployees
                .Where(ppe => ppe.PayrollPeriodId == payrollRun.PayrollPeriodId)
                .Select(ppe => ppe.EmployeeId)
                .ToListAsync();

            if (!employeeIdsInPeriod.Any())
                throw new InvalidOperationException(
                    "No employees are assigned to this payroll period.");

            var activeEmployees = await _db.Employees
                .Where(e => employeeIdsInPeriod.Contains(e.Id) && e.IsActive && !e.IsDeleted)
                .Include(e => e.DefaultRateType)
                .Include(e => e.Allowances.Where(a => a.IsActive && !a.IsDeleted))
                    .ThenInclude(a => a.Allowance)
                .ToListAsync();

            if (!activeEmployees.Any())
                throw new InvalidOperationException("No active employees found.");

            var errors = new List<string>();
            int staged = 0;

            foreach (var employee in activeEmployees)
            {
                try
                {
                    if (employee.DefaultRateType == null)
                    {
                        _logger.LogWarning("Employee {Id} has no default rate type — skipped.", employee.Id);
                        continue;
                    }

                    var entry = await CalculateEmployeePayrollAsync(
                        employee, payrollRun,
                        payrollPeriod.PeriodStartDate,
                        payrollPeriod.PeriodEndDate);

                    if (entry != null)
                    {
                        _db.Entry(payrollRun).State = EntityState.Detached;
                        _db.PayrollEntries.Add(entry);
                        staged++;
                    }
                }
                catch (Exception ex)
                {
                    var msg = $"Error calculating payroll for employee {employee.Id}: {ex.Message}";
                    _logger.LogError(ex, msg);
                    errors.Add(msg);
                }
            }

            if (staged == 0)
                throw new InvalidOperationException($"No payroll entries were produced. Errors: {string.Join("; ", errors)}");

            try
            {
                await _db.SaveChangesAsync();
                _logger.LogInformation("Saved {Count} payroll entries.", staged);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Save failed after staging {Count} entries.", staged);
                throw new InvalidOperationException($"Payroll entries could not be saved: {ex.InnerException?.Message ?? ex.Message}", ex);
            }
        }

        private async Task<PayrollEntry?> CalculateEmployeePayrollAsync(
            Employee employee, PayrollRun payrollRun,
            DateOnly startDate, DateOnly endDate)
        {
            if (employee.DefaultRateType == null)
                return null;

            var payrollConfig = await _db.PayrollConfigurations
                .AsNoTracking()
                .Where(pc => pc.IsActive)
                .OrderByDescending(pc => pc.EffectiveDate)
                .FirstOrDefaultAsync();

            if (payrollConfig == null)
                return null;

            var allDayTypes = await _db.DayTypes
                .AsNoTracking()
                .Where(dt => !dt.IsDeleted && dt.IsActive)
                .ToListAsync();

            if (!allDayTypes.Any())
                return null;

            // Timecard is the source of truth for payroll computation
            var timeCard = await _db.PayrollPeriodTimeCards
                .AsNoTracking()
                .Include(tc => tc.Details)
                .FirstOrDefaultAsync(tc =>
                    tc.PayrollPeriodId == payrollRun.PayrollPeriodId &&
                    tc.EmployeeId == employee.Id);

            if (timeCard == null || !timeCard.Details.Any())
            {
                _logger.LogWarning(
                    "Skipping employee {EmployeeId} for payroll run {PayrollRunId}: no timecard details found for period {Start} to {End}.",
                    employee.Id, payrollRun.Id, startDate, endDate);
                return null;
            }

            // Calculate hours and pay via centralized DayType-multiplier engine
            var dayTypesById = allDayTypes.ToDictionary(dt => dt.Id);
            var pay = _payrollComputation.ComputeFromTimeCard(
                employee.HourlyRate,
                timeCard.Details,
                dayTypesById);

            decimal regularHours = pay.RegularHours;
            decimal overtimeHours = pay.OvertimeHours;
            decimal nightDifferentialHours = pay.NightDifferentialHours;
            decimal nightDifferentialOvertimeHours = pay.NightDifferentialOvertimeHours;
            decimal regularPay = pay.RegularPay;
            decimal overtimePay = pay.OvertimePayForEntry; // OT + ND-OT (entry has no separate ND-OT pay column)
            decimal nightDifferentialPay = pay.NightDifferentialPay;
            decimal grossPay = pay.GrossPay;

            decimal totalBenefits = employee.Allowances
                .Where(a => a.IsCurrent)
                .Sum(a => a.MonthlyAmount);

            // Calculate taxable income (Gross Pay - Non-taxable benefits)
            decimal taxableIncome = grossPay - GetNonTaxableBenefits(employee);

            // Resolve monthly salary for government contributions
            decimal monthlyBasicSalary = ResolveMonthlyBasicSalary(employee, payrollConfig.WorkingDaysPerMonth);

            // Determine if government contributions should be applied
            bool isFirstPeriodOfMonth = payrollRun.PayrollPeriod.PeriodStartDate.Day <= 15
                                        && payrollRun.PayrollPeriod.PeriodEndDate.Day <= 15;

            bool applyGovernmentContributions = ShouldApplyGovernmentContributions(
                payrollRun.PayrollPeriod,
                employee);

            // Get period frequency for logging (calculated, not stored)
            var periodFrequency = GetFrequencyFromDateRange(startDate, endDate);

            _logger.LogInformation(
                "Employee {Id} | Period: {StartDate} to {EndDate} | Frequency: {Frequency} | FirstPeriod: {First} | ApplyGov: {Apply} | " +
                "MonthlySalary: {Monthly} | GrossPay: {Gross} | TaxableIncome: {Taxable}",
                employee.Id, startDate, endDate, periodFrequency, isFirstPeriodOfMonth,
                applyGovernmentContributions, monthlyBasicSalary, grossPay, taxableIncome);

            // Calculate tax on TAXABLE INCOME with proper bracket normalization
            decimal taxWithheld = await CalculateTaxAsync(taxableIncome, payrollRun.PayrollPeriod);

            // Calculate government contributions
            var govtContributions = applyGovernmentContributions
                ? await CalculateGovernmentContributionsAsync(
                    monthlyBasicSalary, payrollRun.PayrollPeriod, payrollRun.CreatedById)
                : new List<PayrollEntryGovernmentContribution>();

            decimal totalGovtEmployeeShare = govtContributions.Sum(gc => gc.EmployeeShare);

            // Calculate deductions from timecard values
            var (absenceDeduction, lateUndertimeDeduction) =
                CalculateTimeCardDeductions(employee, timeCard, allDayTypes);

            // Loan deductions (per-employee loans)
            decimal loanDeductions = await ApplyEmployeeLoanDeductionsAsync(employee, payrollRun, startDate, endDate, govtContributions);

            // Total deductions (base) = absence + late/undertime + tax + loans
            // Recurring employee deductions (non-loan) will be added after creating payrollEntry
            decimal totalDeductions = absenceDeduction + lateUndertimeDeduction + taxWithheld + loanDeductions;

            // Net Pay = Gross + Benefits - Deductions - Gov't Contributions
            decimal netPay = grossPay + totalBenefits - totalDeductions - totalGovtEmployeeShare;

            // Create payroll entry
            string contribNote = applyGovernmentContributions
                ? "[Gov't contributions applied]"
                : "[No gov't contributions — second semi-monthly period]";

            var payrollEntry = new PayrollEntry
            {
                PayrollRunId = payrollRun.Id,
                EmployeeId = employee.Id,

                RegularHours = regularHours,
                OvertimeHours = overtimeHours,
                NightDifferentialHours = nightDifferentialHours,
                NightDifferentialOvertimeHours = nightDifferentialOvertimeHours,

                HourlyRate = employee.HourlyRate,
                // Display/base rate only — multipliers come from DayType via PayrollComputationService.
                OvertimeRate = employee.HourlyRate,
                NightDifferentialRate = employee.HourlyRate,

                RegularPay = regularPay,
                OvertimePay = overtimePay,
                NightDifferentialPay = nightDifferentialPay,
                GrossPay = grossPay,

                TotalBenefits = totalBenefits,
                TotalDeductions = totalDeductions,
                NetPay = netPay,
                TaxWithheld = taxWithheld,

                Notes = $"Period: {startDate:MMM dd} - {endDate:MMM dd, yyyy} {contribNote}",
                IsProcessed = true,

                CreatedById = payrollRun.CreatedById,
                CreatedOn = DateTime.Now,
                UpdatedById = payrollRun.CreatedById,
                UpdatedOn = DateTime.Now,

                GovernmentContributions = govtContributions
            };

            // Add deduction line items (attendance & tax)
            var absenceTypeId = await GetDeductionTypeIdAsync("ABSENCE");
            var lateTypeId = await GetDeductionTypeIdAsync("LATE_UNDERTIME");
            var incomeTaxTypeId = await GetDeductionTypeIdAsync("INCOME_TAX");

            AddDeductionIfPositive(payrollEntry, absenceTypeId,
                absenceDeduction, "Absence Deductions", payrollRun.CreatedById);

            AddDeductionIfPositive(payrollEntry, lateTypeId,
                lateUndertimeDeduction, "Late/Undertime Deductions", payrollRun.CreatedById);

            AddDeductionIfPositive(payrollEntry, incomeTaxTypeId,
                taxWithheld, "Withholding Tax", payrollRun.CreatedById);

            // Recurring employee deductions (non-loan, from EmployeeDeduction → Deduction)
            decimal masterDeductions = await ApplyEmployeeMasterDeductionsAsync(
                employee, payrollRun, startDate, endDate, grossPay, totalBenefits, payrollEntry);

            // Update totals on entry to include master deductions
            payrollEntry.TotalDeductions += masterDeductions;
            payrollEntry.NetPay -= masterDeductions;

            return payrollEntry;
        }

        private async Task<decimal> ApplyEmployeeLoanDeductionsAsync(
            Employee employee,
            PayrollRun payrollRun,
            DateOnly startDate,
            DateOnly endDate,
            IList<PayrollEntryGovernmentContribution> govtContributions)
        {
            var periodEnd = endDate;

            var loans = await _db.EmployeeLoans
                .Include(l => l.DeductionType)
                .Where(l =>
                    l.EmployeeId == employee.Id &&
                    !l.IsDeleted &&
                    l.Status == EmployeeLoanStatus.Active &&
                    l.RemainingBalance > 0 &&
                    l.StartDate <= periodEnd &&
                    (!l.EndDate.HasValue || l.EndDate.Value >= startDate))
                .ToListAsync();

            if (!loans.Any())
                return 0M;

            decimal totalLoanDeductions = 0M;

            foreach (var loan in loans)
            {
                if (loan.RemainingBalance <= 0 || loan.InstallmentsPaid >= loan.TotalInstallments)
                {
                    loan.Status = EmployeeLoanStatus.Completed;
                    continue;
                }

                var installmentAmount = loan.InstallmentAmount;
                if (installmentAmount <= 0)
                {
                    installmentAmount = loan.TotalAmountToRepay / loan.TotalInstallments;
                }

                var amountThisPeriod = Math.Min(installmentAmount, loan.RemainingBalance);

                if (amountThisPeriod <= 0)
                    continue;

                loan.RemainingBalance -= amountThisPeriod;
                loan.InstallmentsPaid += 1;

                if (loan.RemainingBalance <= 0 || loan.InstallmentsPaid >= loan.TotalInstallments)
                {
                    loan.Status = EmployeeLoanStatus.Completed;
                }

                // Split principal/interest proportionally based on configured totals
                decimal totalInterest = loan.TotalAmountToRepay - loan.PrincipalAmount;
                decimal principalPortion;
                decimal interestPortion;

                if (loan.TotalAmountToRepay > 0 && totalInterest > 0)
                {
                    var interestRatio = totalInterest / loan.TotalAmountToRepay;
                    interestPortion = Math.Round(amountThisPeriod * interestRatio, 2);
                    principalPortion = amountThisPeriod - interestPortion;
                }
                else
                {
                    principalPortion = amountThisPeriod;
                    interestPortion = 0M;
                }

                totalLoanDeductions += amountThisPeriod;

                var loanLabel = !string.IsNullOrWhiteSpace(loan.LoanName) ? loan.LoanName : (loan.DeductionType?.Name ?? "Loan");
                _db.PayrollDeductions.Add(new PayrollDeduction
                {
                    PayrollEntryId = 0, // will be linked after PayrollEntry is saved
                    DeductionTypeId = loan.DeductionTypeId,
                    Amount = amountThisPeriod,
                    Description = $"{loanLabel} Loan Payment",
                    IsMandatory = true,
                    PrincipalAmount = principalPortion,
                    InterestAmount = interestPortion,
                    BalanceRemaining = loan.RemainingBalance,
                    InstallmentNumber = loan.InstallmentsPaid,
                    TotalInstallments = loan.TotalInstallments,
                    ReferenceNumber = loan.ReferenceNumber ?? loan.Id.ToString(),
                    CreatedById = payrollRun.CreatedById,
                    CreatedOn = DateTime.Now
                });
            }

            return totalLoanDeductions;
        }

        private async Task<decimal> ApplyEmployeeMasterDeductionsAsync(
            Employee employee,
            PayrollRun payrollRun,
            DateOnly startDate,
            DateOnly endDate,
            decimal grossPay,
            decimal totalBenefits,
            PayrollEntry payrollEntry)
        {
            var periodEnd = endDate;

            var assignments = await _db.EmployeeDeductions
                .Include(ed => ed.Deduction)
                    .ThenInclude(d => d.DeductionType)
                .Where(ed =>
                    ed.EmployeeId == employee.Id &&
                    !ed.IsDeleted &&
                    ed.IsActive &&
                    ed.Deduction != null &&
                    !ed.Deduction.IsDeleted &&
                    ed.Deduction.IsActive &&
                    ed.Deduction.EffectiveStartDate <= periodEnd &&
                    (!ed.Deduction.EffectiveEndDate.HasValue ||
                     ed.Deduction.EffectiveEndDate.Value >= startDate))
                .ToListAsync();

            if (!assignments.Any())
                return 0M;

            decimal total = 0M;

            foreach (var ed in assignments)
            {
                var deduction = ed.Deduction;
                if (deduction?.DeductionType == null)
                    continue;

                // Skip loan-type; loans use EmployeeLoan instead
                if (string.Equals(deduction.DeductionType.Code, "LOAN", StringComparison.OrdinalIgnoreCase))
                    continue;

                decimal amount = deduction.Amount;
                if (amount <= 0) continue;

                total += amount;

                payrollEntry.Deductions.Add(new PayrollDeduction
                {
                    DeductionTypeId = deduction.DeductionTypeId,
                    Amount = amount,
                    Description = string.IsNullOrWhiteSpace(deduction.DeductionName) ? "Employee Deduction" : deduction.DeductionName,
                    IsMandatory = false,
                    ReferenceNumber = deduction.Code,
                    CreatedById = payrollRun.CreatedById,
                    CreatedOn = DateTime.Now
                });
            }

            return total;
        }

        // Gets the frequency based on date range (not stored, just calculated)
        private PayrollFrequency GetFrequencyFromDateRange(DateOnly startDate, DateOnly endDate)
        {
            int days = endDate.DayNumber - startDate.DayNumber + 1;

            return days switch
            {
                7 => PayrollFrequency.Weekly,
                14 => PayrollFrequency.BiWeekly,
                >= 15 and <= 16 => PayrollFrequency.SemiMonthly,
                >= 28 and <= 31 => PayrollFrequency.Monthly,
                _ => PayrollFrequency.Monthly
            };
        }

        // Calculates withholding tax using tax table effective for the payroll period.
        // Tax tables are filtered by effective date (and optionally TaxYear); income is normalized
        // to the bracket's IncomeBasis, tax computed, then converted back to period amount.
        private async Task<decimal> CalculateTaxAsync(decimal taxableIncome, PayrollPeriod payrollPeriod)
        {
            var periodStart = payrollPeriod.PeriodStartDate.ToDateTime(TimeOnly.MinValue);
            var periodEnd = payrollPeriod.PeriodEndDate.ToDateTime(TimeOnly.MinValue);

            var taxTables = await _db.TaxTables
                .AsNoTracking()
                .Where(tt => tt.IsActive
                    && tt.EffectiveStartDate <= periodEnd
                    && (!tt.EffectiveEndDate.HasValue || tt.EffectiveEndDate.Value >= periodStart))
                .OrderBy(tt => tt.MinIncome)
                .ToListAsync();

            if (!taxTables.Any())
                return 0M;

            // Determine income basis from first tax table (IncomeBasis is enum)
            var incomeBasis = taxTables.First().IncomeBasis.ToString().ToUpperInvariant();

            // Get period frequency from date range
            var periodFrequency = GetFrequencyFromDateRange(payrollPeriod.PeriodStartDate, payrollPeriod.PeriodEndDate);

            // Normalize period income to match bracket basis
            decimal normalizedIncome = NormalizeIncomeToBasis(taxableIncome, periodFrequency, incomeBasis);

            // Find applicable bracket
            var bracket = taxTables.FirstOrDefault(tt =>
                normalizedIncome >= tt.MinIncome &&
                (tt.MaxIncome == null || normalizedIncome <= tt.MaxIncome));

            if (bracket == null)
                return 0M;

            // Calculate tax on normalized income (TaxRate stored as fraction: 0.20 = 20%)
            decimal taxOnNormalized = bracket.BracketType switch
            {
                TaxBracketType.Progressive when bracket.TaxRate.HasValue =>
                    bracket.BaseTaxAmount +
                    (normalizedIncome - bracket.MinIncome) * bracket.TaxRate.Value,

                TaxBracketType.Percentage when bracket.TaxRate.HasValue =>
                    normalizedIncome * bracket.TaxRate.Value,

                TaxBracketType.FixedAmount =>
                    bracket.FixedAmount ?? 0M,

                _ => 0M
            };

            // Convert back to period amount
            decimal periodTax = ConvertFromNormalizedToPeriod(taxOnNormalized, periodFrequency, incomeBasis);

            return Math.Round(Math.Max(0M, periodTax), 2);
        }

        // Normalizes income from period frequency to bracket basis
        private decimal NormalizeIncomeToBasis(decimal periodIncome, PayrollFrequency frequency, string targetBasis)
        {
            return targetBasis.ToUpperInvariant() switch
            {
                "ANNUAL" => frequency switch
                {
                    PayrollFrequency.SemiMonthly => periodIncome * 24M,
                    PayrollFrequency.Monthly => periodIncome * 12M,
                    PayrollFrequency.BiWeekly => periodIncome * 26M,
                    PayrollFrequency.Weekly => periodIncome * 52M,
                    _ => periodIncome
                },

                "SEMIMONTHLY" => frequency switch
                {
                    PayrollFrequency.SemiMonthly => periodIncome,
                    PayrollFrequency.Monthly => periodIncome / 2M,
                    PayrollFrequency.BiWeekly => periodIncome * 26M / 24M,
                    PayrollFrequency.Weekly => periodIncome * 52M / 24M,
                    _ => periodIncome
                },

                "MONTHLY" => frequency switch
                {
                    PayrollFrequency.SemiMonthly => periodIncome * 2M,
                    PayrollFrequency.Monthly => periodIncome,
                    PayrollFrequency.BiWeekly => periodIncome * 26M / 12M,
                    PayrollFrequency.Weekly => periodIncome * 52M / 12M,
                    _ => periodIncome
                },

                "BIWEEKLY" => frequency switch
                {
                    PayrollFrequency.SemiMonthly => periodIncome * 24M / 26M,
                    PayrollFrequency.Monthly => periodIncome * 12M / 26M,
                    PayrollFrequency.BiWeekly => periodIncome,
                    PayrollFrequency.Weekly => periodIncome * 52M / 26M,
                    _ => periodIncome
                },

                "WEEKLY" => frequency switch
                {
                    PayrollFrequency.SemiMonthly => periodIncome * 24M / 52M,
                    PayrollFrequency.Monthly => periodIncome * 12M / 52M,
                    PayrollFrequency.BiWeekly => periodIncome * 26M / 52M,
                    PayrollFrequency.Weekly => periodIncome,
                    _ => periodIncome
                },

                "DAILY" => frequency switch
                {
                    PayrollFrequency.SemiMonthly => periodIncome / 11M,  // ~11 working days per half-month
                    PayrollFrequency.Monthly => periodIncome / 22M,
                    PayrollFrequency.BiWeekly => periodIncome / 10M,
                    PayrollFrequency.Weekly => periodIncome / 5M,
                    _ => periodIncome
                },

                _ => periodIncome
            };
        }

        // Converts tax from normalized basis back to period amount
        private decimal ConvertFromNormalizedToPeriod(decimal normalizedTax, PayrollFrequency frequency, string sourceBasis)
        {
            return sourceBasis.ToUpperInvariant() switch
            {
                "ANNUAL" => frequency switch
                {
                    PayrollFrequency.SemiMonthly => normalizedTax / 24M,
                    PayrollFrequency.Monthly => normalizedTax / 12M,
                    PayrollFrequency.BiWeekly => normalizedTax / 26M,
                    PayrollFrequency.Weekly => normalizedTax / 52M,
                    _ => normalizedTax
                },

                "SEMIMONTHLY" => frequency switch
                {
                    PayrollFrequency.SemiMonthly => normalizedTax,
                    PayrollFrequency.Monthly => normalizedTax * 2M,
                    PayrollFrequency.BiWeekly => normalizedTax * 24M / 26M,
                    PayrollFrequency.Weekly => normalizedTax * 24M / 52M,
                    _ => normalizedTax
                },

                "MONTHLY" => frequency switch
                {
                    PayrollFrequency.SemiMonthly => normalizedTax / 2M,
                    PayrollFrequency.Monthly => normalizedTax,
                    PayrollFrequency.BiWeekly => normalizedTax * 12M / 26M,
                    PayrollFrequency.Weekly => normalizedTax * 12M / 52M,
                    _ => normalizedTax
                },

                "BIWEEKLY" => frequency switch
                {
                    PayrollFrequency.SemiMonthly => normalizedTax * 24M / 26M,
                    PayrollFrequency.Monthly => normalizedTax * 12M / 26M,
                    PayrollFrequency.BiWeekly => normalizedTax,
                    PayrollFrequency.Weekly => normalizedTax * 26M / 52M,
                    _ => normalizedTax
                },

                "WEEKLY" => frequency switch
                {
                    PayrollFrequency.SemiMonthly => normalizedTax * 24M / 52M,
                    PayrollFrequency.Monthly => normalizedTax * 12M / 52M,
                    PayrollFrequency.BiWeekly => normalizedTax * 26M / 52M,
                    PayrollFrequency.Weekly => normalizedTax,
                    _ => normalizedTax
                },

                "DAILY" => frequency switch
                {
                    PayrollFrequency.SemiMonthly => normalizedTax * 11M,  // ~11 working days per half-month
                    PayrollFrequency.Monthly => normalizedTax * 22M,
                    PayrollFrequency.BiWeekly => normalizedTax * 10M,
                    PayrollFrequency.Weekly => normalizedTax * 5M,
                    _ => normalizedTax
                },

                _ => normalizedTax
            };
        }

        // Determines if government contributions should be applied
        // Uses date range to determine period type, not stored frequency
        private bool ShouldApplyGovernmentContributions(
            PayrollPeriod payrollPeriod,
            Employee employee)
        {
            int daysInPeriod = payrollPeriod.PeriodEndDate.DayNumber - payrollPeriod.PeriodStartDate.DayNumber + 1;
            bool isSemiMonthly = daysInPeriod >= 15 && daysInPeriod <= 16;
            bool isFirstHalfOfMonth = payrollPeriod.PeriodStartDate.Day <= 15
                                      && payrollPeriod.PeriodEndDate.Day <= 15;

            // If not semi-monthly, always apply
            if (!isSemiMonthly)
                return true;

            // Use employee's specific rule
            return employee.GovContributionRule switch
            {
                ContributionApplicationRule.EveryPayroll => true,
                ContributionApplicationRule.FirstPeriodOnly => isFirstHalfOfMonth,
                ContributionApplicationRule.Never => false,
                _ => true // Default to every payroll
            };
        }

        // Calculates government contributions using monthly salary basis
        private async Task<List<PayrollEntryGovernmentContribution>> CalculateGovernmentContributionsAsync(
            decimal monthlyBasicSalary,
            PayrollPeriod payrollPeriod,
            string? createdById)
        {
            var periodStart = payrollPeriod.PeriodStartDate.ToDateTime(TimeOnly.MinValue);
            var periodEnd = payrollPeriod.PeriodEndDate.ToDateTime(TimeOnly.MinValue);

            var contributionTypes = await _db.GovernmentContributionTypes
                .AsNoTracking()
                .Where(gct => gct.IsActive)
                .Include(gct => gct.ContributionTables
                    .Where(gctt => gctt.IsActive
                        && gctt.EffectiveStartDate <= periodEnd
                        && (!gctt.EffectiveEndDate.HasValue || gctt.EffectiveEndDate.Value >= periodStart)))
                .OrderBy(gct => gct.DisplayOrder)
                .ToListAsync();

            var results = new List<PayrollEntryGovernmentContribution>();

            foreach (var type in contributionTypes)
            {
                // Find bracket using MONTHLY salary
                var bracket = type.ContributionTables.FirstOrDefault(t =>
                    monthlyBasicSalary >= t.MinSalary &&
                    (t.MaxSalary == null || monthlyBasicSalary <= t.MaxSalary));

                if (bracket == null)
                {
                    _logger.LogDebug("No bracket found for {Type} at salary {Salary}", type.Name, monthlyBasicSalary);
                    continue;
                }

                decimal employeeShare = 0M;
                decimal employerShare = 0M;

                switch (type.CalculationMethod)
                {
                    case ContributionCalculationMethod.Fixed:
                        // Fixed amount from table (used for SSS)
                        employeeShare = bracket.EmployeeShare ?? 0M;
                        employerShare = bracket.EmployerShare ?? 0M;
                        break;

                    case ContributionCalculationMethod.Percentage:
                        // Percentage of monthly salary (used for PhilHealth, Pag-IBIG)
                        // IMPORTANT: Percentage is stored as decimal (0.03 = 3%), so we multiply directly
                        if (bracket.EmployeeSharePercentage.HasValue)
                        {
                            employeeShare = monthlyBasicSalary * bracket.EmployeeSharePercentage.Value;
                        }
                        else
                        {
                            employeeShare = bracket.EmployeeShare ?? 0M;
                        }

                        if (bracket.EmployerSharePercentage.HasValue)
                        {
                            employerShare = monthlyBasicSalary * bracket.EmployerSharePercentage.Value;
                        }
                        else
                        {
                            employerShare = bracket.EmployerShare ?? 0M;
                        }
                        break;

                    case ContributionCalculationMethod.Tiered:
                        // Tiered calculation - use fixed if available, otherwise percentage
                        if (bracket.EmployeeShare.HasValue)
                        {
                            employeeShare = bracket.EmployeeShare.Value;
                        }
                        else if (bracket.EmployeeSharePercentage.HasValue)
                        {
                            employeeShare = monthlyBasicSalary * bracket.EmployeeSharePercentage.Value;
                        }

                        if (bracket.EmployerShare.HasValue)
                        {
                            employerShare = bracket.EmployerShare.Value;
                        }
                        else if (bracket.EmployerSharePercentage.HasValue)
                        {
                            employerShare = monthlyBasicSalary * bracket.EmployerSharePercentage.Value;
                        }
                        break;
                }

                // Apply minimum employee contribution floor if specified
                if (bracket.MinEmployeeContribution.HasValue && bracket.MinEmployeeContribution.Value > 0)
                    employeeShare = Math.Max(employeeShare, bracket.MinEmployeeContribution.Value);

                // Apply maximum contribution caps if specified
                if (bracket.MaxEmployeeContribution.HasValue && bracket.MaxEmployeeContribution.Value > 0)
                    employeeShare = Math.Min(employeeShare, bracket.MaxEmployeeContribution.Value);
                if (bracket.MaxEmployerContribution.HasValue && bracket.MaxEmployerContribution.Value > 0)
                    employerShare = Math.Min(employerShare, bracket.MaxEmployerContribution.Value);

                // Round to 2 decimal places
                employeeShare = Math.Round(employeeShare, 2);
                employerShare = Math.Round(employerShare, 2);

                // Skip if both shares are zero
                if (employeeShare <= 0 && employerShare <= 0)
                    continue;

                results.Add(new PayrollEntryGovernmentContribution
                {
                    GovernmentContributionTypeId = type.Id,
                    ContributionName = type.Name,
                    EmployeeShare = employeeShare,
                    EmployerShare = employerShare,
                    TotalContribution = employeeShare + employerShare,
                    SalaryBasis = monthlyBasicSalary
                });

                _logger.LogInformation(
                    "Contribution {Type}: Monthly={Salary:C}, Employee={Emp:C}, Employer={Empr:C}",
                    type.Name, monthlyBasicSalary, employeeShare, employerShare);
            }

            return results;
        }

        // Resolves monthly salary for government contribution lookup
        // Always returns full monthly equivalent regardless of pay frequency
        private static decimal ResolveMonthlyBasicSalary(Employee employee, int workingDaysPerMonth)
        {
            var rt = employee.DefaultRateType;
            if (rt == null) return 0M;

            if (rt.RateBasis == RateBasis.Monthly && rt.MonthlySalary.HasValue)
                return rt.MonthlySalary.Value;

            if (rt.RateBasis == RateBasis.Daily && rt.DailyRate.HasValue)
                return rt.DailyRate.Value * workingDaysPerMonth;

            if (rt.RateBasis == RateBasis.Hourly && rt.HourlyRate.HasValue)
                return rt.HourlyRate.Value * 8M * workingDaysPerMonth;

            return employee.BasicSalary > 0 ? employee.BasicSalary : 0M;
        }

        // Get non-taxable benefits (override this based on your business rules)
        private static decimal GetNonTaxableBenefits(Employee employee)
        {
            // Implement your logic for non-taxable benefits
            // e.g., De minimis benefits, 13th month pay threshold, etc.
            return 0M;
        }

        private static (decimal absenceDeduction, decimal lateUndertimeDeduction)
            CalculateTimeCardDeductions(
                Employee employee,
                PayrollPeriodTimeCardEntry timeCard,
                List<DayType> dayTypes)
        {
            // Late/Undertime are deducted here only. Basic Hours on the time card are full
            // scheduled shift hours and are NOT reduced for early departure / late arrival.
            var lateUndertimeMinutes = timeCard.LateMinutes + timeCard.UndertimeMinutes;
            var lateUndertimeDeduction = employee.HourlyRate * (lateUndertimeMinutes / 60M);

            // Prefer header AbsentHours (DTR import). Legacy: hours on a DayType named "Absent".
            var absentHours = timeCard.AbsentHours;
            if (absentHours <= 0)
            {
                absentHours = timeCard.Details
                    .Where(d =>
                    {
                        var dayTypeName = dayTypes.FirstOrDefault(dt => dt.Id == d.DayTypeId)?.Name;
                        return !string.IsNullOrWhiteSpace(dayTypeName) &&
                               dayTypeName.Contains("Absent", StringComparison.OrdinalIgnoreCase);
                    })
                    .Sum(d => d.RegularHours);
            }

            var absentDays = absentHours > 0 ? absentHours / 8M : 0M;
            var absenceDeduction = employee.DailyRate * absentDays;

            return (absenceDeduction, lateUndertimeDeduction);
        }

        private async Task<int> GetDeductionTypeIdAsync(string code)
        {
            var type = await _db.DeductionTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(dt => dt.Code == code);

            if (type == null)
            {
                throw new InvalidOperationException($"DeductionType with code '{code}' not found. Seed it in the database.");
            }

            return type.Id;
        }

        private static void AddDeductionIfPositive(
            PayrollEntry entry, int deductionTypeId, decimal amount,
            string description, string? createdById)
        {
            if (amount <= 0) return;
            entry.Deductions.Add(new PayrollDeduction
            {
                DeductionTypeId = deductionTypeId,
                Amount = amount,
                Description = description,
                IsMandatory = true,
                CreatedById = createdById,
                CreatedOn = DateTime.Now
            });
        }
    }
}