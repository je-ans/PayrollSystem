using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Services;
using PayrollSystem.Utility;

namespace PayrollSystem.ViewComponents
{
    // Sidebar badge for items the current user still needs to approve (workflow current step).
    // Same rule for everyone, including admins — not the full system queue.
    public class NavBadgeViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _db;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly UserManager<ApplicationUser> _userManager;

        public NavBadgeViewComponent(
            ApplicationDbContext db,
            IApprovalWorkflowService workflowService,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _workflowService = workflowService;
            _userManager = userManager;
        }

        // overtime | justification | schedulechange | pendingapproval | attendance
        public async Task<IViewComponentResult> InvokeAsync(string type)
        {
            if (HttpContext.User?.Identity?.IsAuthenticated != true)
                return Content(string.Empty);

            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, HttpContext.User);
            if (employee == null)
                return Content(string.Empty);

            WorkflowProcessType? processType = type?.ToLowerInvariant() switch
            {
                "overtime" => WorkflowProcessType.Overtime,
                "justification" => WorkflowProcessType.TimeLogJustification,
                "schedulechange" => WorkflowProcessType.ScheduleChange,
                "pendingapproval" or "attendance" => null,
                _ => null
            };

            if (type?.ToLowerInvariant() is not ("overtime" or "justification" or "schedulechange"
                or "pendingapproval" or "attendance"))
                return Content(string.Empty);

            var count = await _workflowService.GetPendingCountForEmployeeAsync(employee.Id, processType);
            if (count <= 0)
                return Content(string.Empty);

            return View(count);
        }
    }
}
