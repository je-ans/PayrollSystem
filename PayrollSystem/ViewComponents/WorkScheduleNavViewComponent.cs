using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;

namespace PayrollSystem.ViewComponents
{
    public class WorkScheduleNavViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public WorkScheduleNavViewComponent(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (HttpContext.User?.Identity?.IsAuthenticated != true)
                return Content(string.Empty);

            var access = await WorkScheduleAccessHelper.GetAccessAsync(_db, _userManager, HttpContext.User);
            return View(access);
        }
    }
}
