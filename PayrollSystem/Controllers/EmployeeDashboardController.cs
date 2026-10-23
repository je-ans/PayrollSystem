using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.Controllers
{
    [Authorize]
    public class EmployeeDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmployeeDashboardController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Get employee data
            var employee = await _context.Employees
                .Include(e => e.ApplicationUser)
                .Include(e => e.Designations.Where(d => d.EffectiveTo == null))
                    .ThenInclude(d => d.Designation)
                .Include(e => e.DefaultRateType)
                .Include(e => e.Allowances.Where(a => a.IsActive && !a.IsDeleted))
                    .ThenInclude(a => a.Allowance)
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUser.Id && e.IsActive);

            if (employee == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            // Get recent payslips
            var recentPayslips = await _context.Payslips
                .Include(p => p.PayrollEntry)
                .ThenInclude(pe => pe.PayrollRun)
                .ThenInclude(pr => pr.PayrollPeriod)
                .Where(p => p.PayrollEntry.EmployeeId == employee.Id)
                .OrderByDescending(p => p.GeneratedDate)
                .Take(3)
                .ToListAsync();

            // Get upcoming payroll period
            var upcomingPayroll = await _context.PayrollPeriods
                .Where(pp => pp.IsActive && !pp.IsDeleted &&
                            pp.PeriodEndDate >= DateOnly.FromDateTime(DateTime.Now))
                .OrderBy(pp => pp.PeriodEndDate)
                .FirstOrDefaultAsync();

            var unitNames = await EmployeeOrganizationHelper.GetActiveUnitDisplayNamesAsync(
                _context, new[] { employee.Id });

            var scheduleAccess = await WorkScheduleAccessHelper.GetAccessAsync(_context, _userManager, User);

            var viewModel = new EmployeeDashboardViewModel
            {
                Employee = employee,
                OrganizationUnitsDisplay = unitNames.GetValueOrDefault(employee.Id, "—"),
                RecentPayslips = recentPayslips,
                UpcomingPayrollPeriod = upcomingPayroll,
                CurrentYear = DateTime.Now.Year,
                CanManageSchedule = scheduleAccess.CanManageSchedule
            };

            return View(viewModel);
        }
    }

    public class EmployeeDashboardViewModel
    {
        public Employee Employee { get; set; }
        public string OrganizationUnitsDisplay { get; set; } = "—";
        public IEnumerable<Payslip> RecentPayslips { get; set; }
        public PayrollPeriod UpcomingPayrollPeriod { get; set; }
        public int CurrentYear { get; set; }
        public bool CanManageSchedule { get; set; }

        public decimal CurrentSalary => Employee?.BasicSalary ?? 0;

    }
}