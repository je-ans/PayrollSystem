using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayrollSystem.Data;

namespace PayrollSystem.Controllers
{
    [AllowAnonymous]
    [Route("manifest.json")]
    public class ManifestController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ManifestController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult GetManifest()
        {
            var systemInfo = _db.SystemInfos.FirstOrDefault(s => !s.IsDeleted);
            var logoUrl = systemInfo?.LogoUrl;
            if (string.IsNullOrEmpty(logoUrl))
            {
                logoUrl = "/image/drmc_logo.png";
            }
            else if (!logoUrl.StartsWith("/"))
            {
                logoUrl = "/" + logoUrl.TrimStart('/');
            }

            string logoBase = logoUrl;
            if (logoBase.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                logoBase = logoBase.Substring(0, logoBase.Length - 4);

            var logo192 = logoBase + "_192x192.png";
            var logo512 = logoBase + "_512x512.png";

            var manifest = new
            {
                name = systemInfo?.SystemName ?? "DRMC-BMS",
                short_name = systemInfo?.ShortName ?? "BMS",
                id = "/",
                start_url = "/",
                display = "standalone",
                background_color = "#ffffff",
                theme_color = "#007bff",
                description = systemInfo?.Description ?? "DRMC Budget Management System",
                icons = new[]
                {
                    new {
                        src = logo192,
                        sizes = "192x192",
                        type = "image/png"
                    },
                    new {
                        src = logo512,
                        sizes = "512x512",
                        type = "image/png"
                    }
                },
                screenshots = new[]
                {
                    new {
                        src = "/image/screenshot-desktop.png",
                        sizes = "1466x842",
                        type = "image/png",
                        form_factor = "wide"
                    },
                    new {
                        src = "/image/screenshot-mobile.png",
                        sizes = "399x850",
                        type = "image/png",
                        form_factor = "narrow"
                    }
                }
            };
            return Json(manifest);
        }
    }
}