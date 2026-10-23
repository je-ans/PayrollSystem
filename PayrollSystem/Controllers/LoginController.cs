using Microsoft.AspNetCore.Mvc;

namespace PayrollSystem.Controllers
{
    public class LoginController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
