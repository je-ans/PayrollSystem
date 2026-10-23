using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Services;
using PayrollSystem.Utility;

namespace PayrollSystem.ViewComponents
{
    public class PendingApprovalNavViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _db;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly UserManager<ApplicationUser> _userManager;

        public PendingApprovalNavViewComponent(
            ApplicationDbContext db,
            IApprovalWorkflowService workflowService,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _workflowService = workflowService;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (HttpContext.User?.Identity?.IsAuthenticated != true)
                return Content(string.Empty);

            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, HttpContext.User);
            if (employee == null)
                return Content(string.Empty);

            var count = await _workflowService.GetPendingCountForEmployeeAsync(employee.Id);
            if (count <= 0)
                return Content(string.Empty);

            return View(count);
        }
    }
}
