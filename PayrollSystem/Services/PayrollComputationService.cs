using PayrollSystem.Models;

namespace PayrollSystem.Services
{
    // One time-card DayType line used for pay calculation.
    public sealed class PayrollComputationLine
    {
        public int DayTypeId { get; init; }
        public string DayTypeName { get; init; } = string.Empty;
        public string DayTypeCode { get; init; } = string.Empty;
        public decimal RegularHours { get; init; }
        public decimal OvertimeHours { get; init; }
        public decimal NightDifferentialHours { get; init; }
        public decimal NightDifferentialOvertimeHours { get; init; }
        public decimal BasicMultiplier { get; init; }
        public decimal OvertimeMultiplier { get; init; }
        public decimal NightDifferentialMultiplier { get; init; }
        public decimal NightDifferentialOvertimeMultiplier { get; init; }
        public bool SkipEarnings { get; init; }
    }

    // Per-DayType pay breakdown (for payslip / payroll document columns).
    public sealed class PayrollDayTypeBreakdown
    {
        public int DayTypeId { get; init; }
        public string DayTypeName { get; init; } = string.Empty;
        public string DayTypeCode { get; init; } = string.Empty;
        public bool IsRegularDay { get; init; }

        public decimal RegularHours { get; init; }
        public decimal OvertimeHours { get; init; }
        public decimal NightDifferentialHours { get; init; }
        public decimal NightDifferentialOvertimeHours { get; init; }

        public decimal RegularPay { get; init; }
        public decimal OvertimePay { get; init; }
        public decimal NightDifferentialPay { get; init; }
        public decimal NightDifferentialOvertimePay { get; init; }

        //Basic + ND (non-OT earnings for this DayType)
        public decimal BasicAndNdAmount => RegularPay + NightDifferentialPay;

        //OT + ND-OT for this DayType
        public decimal OvertimeAndNdOtAmount => OvertimePay + NightDifferentialOvertimePay;
    }

    // Aggregated pay components from DayType multipliers only — no hardcoded rates.
    public sealed class PayrollPayComponents
    {
        public decimal RegularHours { get; init; }
        public decimal OvertimeHours { get; init; }
        public decimal NightDifferentialHours { get; init; }
        public decimal NightDifferentialOvertimeHours { get; init; }

        public decimal RegularPay { get; init; }
        public decimal OvertimePay { get; init; }
        public decimal NightDifferentialPay { get; init; }
        public decimal NightDifferentialOvertimePay { get; init; }

        // Gross earnings = Basic + OT + ND + ND-OT.
        public decimal GrossPay =>
            RegularPay + OvertimePay + NightDifferentialPay + NightDifferentialOvertimePay;

        // OT pay stored on PayrollEntry (OT + ND-OT) until a dedicated ND-OT pay column exists.
        public decimal OvertimePayForEntry => OvertimePay + NightDifferentialOvertimePay;

        public IReadOnlyList<PayrollDayTypeBreakdown> DayTypeBreakdowns { get; init; } =
            Array.Empty<PayrollDayTypeBreakdown>();
    }

    public interface IPayrollComputationService
    {
        PayrollPayComponents Compute(decimal hourlyRate, IEnumerable<PayrollComputationLine> lines);

        PayrollComputationLine ToLine(PayrollPeriodTimeCardDetail detail, DayType dayType);

        PayrollPayComponents ComputeFromTimeCard(
            decimal hourlyRate,
            IEnumerable<PayrollPeriodTimeCardDetail> details,
            IReadOnlyDictionary<int, DayType> dayTypesById);
    }

    // Single source of truth for Basic / OT / ND / ND-OT pay using DayType multipliers.
    public class PayrollComputationService : IPayrollComputationService
    {
        public PayrollPayComponents Compute(decimal hourlyRate, IEnumerable<PayrollComputationLine> lines)
        {
            decimal regularHours = 0, overtimeHours = 0, ndHours = 0, ndOtHours = 0;
            decimal regularPay = 0, overtimePay = 0, ndPay = 0, ndOtPay = 0;
            var breakdowns = new List<PayrollDayTypeBreakdown>();

            foreach (var line in lines)
            {
                if (line.SkipEarnings)
                    continue;

                var lineRegPay = RoundMoney(line.RegularHours * hourlyRate * line.BasicMultiplier);
                var lineOtPay = RoundMoney(line.OvertimeHours * hourlyRate * line.OvertimeMultiplier);
                var lineNdPay = RoundMoney(line.NightDifferentialHours * hourlyRate * line.NightDifferentialMultiplier);
                var lineNdOtPay = RoundMoney(
                    line.NightDifferentialOvertimeHours * hourlyRate * line.NightDifferentialOvertimeMultiplier);

                regularHours += line.RegularHours;
                overtimeHours += line.OvertimeHours;
                ndHours += line.NightDifferentialHours;
                ndOtHours += line.NightDifferentialOvertimeHours;

                regularPay += lineRegPay;
                overtimePay += lineOtPay;
                ndPay += lineNdPay;
                ndOtPay += lineNdOtPay;

                var isRegular = line.DayTypeName.Contains("Regular", StringComparison.OrdinalIgnoreCase) &&
                                line.BasicMultiplier == 1.0m &&
                                !line.DayTypeName.Contains("Holiday", StringComparison.OrdinalIgnoreCase);

                breakdowns.Add(new PayrollDayTypeBreakdown
                {
                    DayTypeId = line.DayTypeId,
                    DayTypeName = line.DayTypeName,
                    DayTypeCode = line.DayTypeCode,
                    IsRegularDay = isRegular,
                    RegularHours = line.RegularHours,
                    OvertimeHours = line.OvertimeHours,
                    NightDifferentialHours = line.NightDifferentialHours,
                    NightDifferentialOvertimeHours = line.NightDifferentialOvertimeHours,
                    RegularPay = lineRegPay,
                    OvertimePay = lineOtPay,
                    NightDifferentialPay = lineNdPay,
                    NightDifferentialOvertimePay = lineNdOtPay
                });
            }

            // Merge duplicate DayType rows (multiple details same DayType)
            var merged = breakdowns
                .GroupBy(b => b.DayTypeId)
                .Select(g => new PayrollDayTypeBreakdown
                {
                    DayTypeId = g.Key,
                    DayTypeName = g.First().DayTypeName,
                    DayTypeCode = g.First().DayTypeCode,
                    IsRegularDay = g.First().IsRegularDay,
                    RegularHours = g.Sum(x => x.RegularHours),
                    OvertimeHours = g.Sum(x => x.OvertimeHours),
                    NightDifferentialHours = g.Sum(x => x.NightDifferentialHours),
                    NightDifferentialOvertimeHours = g.Sum(x => x.NightDifferentialOvertimeHours),
                    RegularPay = RoundMoney(g.Sum(x => x.RegularPay)),
                    OvertimePay = RoundMoney(g.Sum(x => x.OvertimePay)),
                    NightDifferentialPay = RoundMoney(g.Sum(x => x.NightDifferentialPay)),
                    NightDifferentialOvertimePay = RoundMoney(g.Sum(x => x.NightDifferentialOvertimePay))
                })
                .OrderByDescending(b => b.IsRegularDay)
                .ThenBy(b => b.DayTypeName)
                .ToList();

            return new PayrollPayComponents
            {
                RegularHours = RoundHours(regularHours),
                OvertimeHours = RoundHours(overtimeHours),
                NightDifferentialHours = RoundHours(ndHours),
                NightDifferentialOvertimeHours = RoundHours(ndOtHours),
                RegularPay = RoundMoney(regularPay),
                OvertimePay = RoundMoney(overtimePay),
                NightDifferentialPay = RoundMoney(ndPay),
                NightDifferentialOvertimePay = RoundMoney(ndOtPay),
                DayTypeBreakdowns = merged
            };
        }

        public PayrollComputationLine ToLine(PayrollPeriodTimeCardDetail detail, DayType dayType)
        {
            var skip = dayType.Name.Contains("Absent", StringComparison.OrdinalIgnoreCase);
            return new PayrollComputationLine
            {
                DayTypeId = dayType.Id,
                DayTypeName = dayType.Name,
                DayTypeCode = dayType.Code,
                RegularHours = detail.RegularHours,
                OvertimeHours = detail.OvertimeHours,
                NightDifferentialHours = detail.NightDifferentialHours,
                NightDifferentialOvertimeHours = detail.NightDifferentialOvertimeHours,
                BasicMultiplier = dayType.BasicMultiplier,
                OvertimeMultiplier = dayType.OvertimeMultiplier,
                NightDifferentialMultiplier = dayType.NightDifferentialMultiplier,
                NightDifferentialOvertimeMultiplier = dayType.NightDifferentialOvertimeMultiplier,
                SkipEarnings = skip
            };
        }

        public PayrollPayComponents ComputeFromTimeCard(
            decimal hourlyRate,
            IEnumerable<PayrollPeriodTimeCardDetail> details,
            IReadOnlyDictionary<int, DayType> dayTypesById)
        {
            var lines = new List<PayrollComputationLine>();
            foreach (var detail in details)
            {
                if (!dayTypesById.TryGetValue(detail.DayTypeId, out var dayType))
                    continue;
                lines.Add(ToLine(detail, dayType));
            }

            return Compute(hourlyRate, lines);
        }

        private static decimal RoundMoney(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private static decimal RoundHours(decimal value) =>
            Math.Round(value, 4, MidpointRounding.AwayFromZero);
    }
}
