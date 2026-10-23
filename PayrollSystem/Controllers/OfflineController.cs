using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayrollSystem.Data;

namespace PayrollSystem.Controllers
{
    [AllowAnonymous]
    [Route("offline.html")]
    public class OfflineController : Controller
    {
        private readonly ApplicationDbContext _db;
        public OfflineController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var systemInfo = _db.SystemInfos.FirstOrDefault(s => !s.IsDeleted);
            return View("~/Views/Offline/Index.cshtml", systemInfo);
        }
    }
}