using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Services;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    [Authorize]
    public class PendingApprovalController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly UserManager<ApplicationUser> _userManager;

        public PendingApprovalController(
            ApplicationDbContext db,
            IApprovalWorkflowService workflowService,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _workflowService = workflowService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.ActiveMenu = "PendingApprovals";
            ViewBag.ActiveLink = "PendingApprovals";

            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            if (employee == null)
                return View(new PendingApprovalIndexViewModel());

            return View(await _workflowService.GetPendingForEmployeeAsync(employee.Id));
        }

        [HttpGet]
        public async Task<IActionResult> Count()
        {
            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            if (employee == null)
                return Json(new { count = 0 });

            return Json(new { count = await _workflowService.GetPendingCountForEmployeeAsync(employee.Id) });
        }
    }
}
