using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using PayrollSystem.Data;
using PayrollSystem.Models;

namespace PayrollSystem.Utility
{
    public class AuditHelper
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;

        // Inject IHttpContextAccessor in the constructor
        public AuditHelper(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAuditTrail(string action, string module, string affectedTable, object? oldData, object? newData)
        {
            // Accessing HttpContext and User via IHttpContextAccessor
            var httpContext = _httpContextAccessor.HttpContext;

            var options = new JsonSerializerOptions { }; // Removed ReferenceHandler.Preserve

            var audit = new AuditTrail
            {
                Action = action,
                Module = module,
                AffectedTable = affectedTable,
                OldData = oldData != null ? JsonSerializer.Serialize(oldData) : "",
                NewData = newData != null ? JsonSerializer.Serialize(newData) : "",

                UserId = _userManager.GetUserId(httpContext.User),  // Accessing User from HttpContext
                TimeStamp = DateTime.Now,
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString()  // Accessing IP from HttpContext
            };

            try
            {
                _db.AuditTrails.Add(audit);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Handle exception (logging to external service, for example)
                Console.WriteLine(ex.Message); // Or log to an external service
            }
        }

    }
}