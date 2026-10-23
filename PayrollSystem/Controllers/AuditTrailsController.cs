using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;

namespace PayrollSystem.Controllers
{
    [Permission("AUDIT_TRAILS")]
    public class AuditTrailsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AuditTrailsController(ApplicationDbContext db)
        {
            _db = db;
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetAuditTrailJson()
        {
            // DataTables parameters
            var draw = HttpContext.Request.Form["draw"].FirstOrDefault();
            var start = Convert.ToInt32(HttpContext.Request.Form["start"].FirstOrDefault() ?? "0");
            var length = Convert.ToInt32(HttpContext.Request.Form["length"].FirstOrDefault() ?? "10");
            var searchValue = HttpContext.Request.Form["search[value]"].FirstOrDefault();
            var orderColumnIndex = HttpContext.Request.Form["order[0][column]"].FirstOrDefault();
            var orderDirection = HttpContext.Request.Form["order[0][dir]"].FirstOrDefault() ?? "asc";
            var orderColumnName = HttpContext.Request.Form[$"columns[{orderColumnIndex}][data]"].FirstOrDefault();

            // Base query on entity for reliable ordering and filtering
            var baseQuery = _db.AuditTrails
                .AsNoTracking()
                .Include(a => a.User)
                .AsQueryable();

            var recordsTotal = await baseQuery.CountAsync();

            // Global search
            if (!string.IsNullOrWhiteSpace(searchValue))
            {
                var sv = searchValue.ToLower();
                baseQuery = baseQuery.Where(a =>
                    (a.Action ?? string.Empty).ToLower().Contains(sv) ||
                    (a.Module ?? string.Empty).ToLower().Contains(sv) ||
                    (a.AffectedTable ?? string.Empty).ToLower().Contains(sv) ||
                    (a.IpAddress ?? string.Empty).ToLower().Contains(sv) ||
                    (((a.User != null ? (a.User.FirstName + " " + a.User.LastName) : string.Empty)) ?? string.Empty).ToLower().Contains(sv));
            }

            var recordsFiltered = await baseQuery.CountAsync();

            // Ordering
            var oc = (orderColumnName ?? string.Empty).ToLower();
            var od = (orderDirection ?? "asc").ToLower();

            baseQuery = (oc, od) switch
            {
                ("action", "asc") => baseQuery.OrderBy(a => a.Action),
                ("action", "desc") => baseQuery.OrderByDescending(a => a.Action),
                ("module", "asc") => baseQuery.OrderBy(a => a.Module),
                ("module", "desc") => baseQuery.OrderByDescending(a => a.Module),
                ("affectedtable", "asc") => baseQuery.OrderBy(a => a.AffectedTable),
                ("affectedtable", "desc") => baseQuery.OrderByDescending(a => a.AffectedTable),
                ("timestamp", "asc") => baseQuery.OrderBy(a => a.TimeStamp),
                ("timestamp", "desc") => baseQuery.OrderByDescending(a => a.TimeStamp),
                ("userfullname", "asc") => baseQuery.OrderBy(a => a.User.FirstName).ThenBy(a => a.User.LastName),
                ("userfullname", "desc") => baseQuery.OrderByDescending(a => a.User.FirstName).ThenByDescending(a => a.User.LastName),
                ("ipaddress", "asc") => baseQuery.OrderBy(a => a.IpAddress),
                ("ipaddress", "desc") => baseQuery.OrderByDescending(a => a.IpAddress),
                _ => baseQuery.OrderByDescending(a => a.TimeStamp)
            };

            // Paging and projection to camelCase with formatted data
            var auditTrails = await baseQuery.Skip(start).Take(length).ToListAsync();

            var page = auditTrails.Select(a => new
            {
                id = a.Id,
                action = a.Action,
                module = a.Module,
                affectedTable = a.AffectedTable,
                oldData = string.IsNullOrWhiteSpace(a.OldData) ? "{}" : a.OldData,
                newData = string.IsNullOrWhiteSpace(a.NewData) ? "{}" : a.NewData,
                oldDataFormatted = FormatAuditData(a.OldData, a.AffectedTable),
                newDataFormatted = FormatAuditData(a.NewData, a.AffectedTable),
                timeStamp = a.TimeStamp,
                userFullName = (a.User != null ? (a.User.FirstName + " " + a.User.LastName) : string.Empty),
                ipAddress = a.IpAddress
            }).ToList();

            return Json(new
            {
                draw,
                recordsTotal,
                recordsFiltered,
                data = page
            });
        }

        private string FormatAuditData(string jsonData, string tableName)
        {
            if (string.IsNullOrWhiteSpace(jsonData) || jsonData == "{}")
            {
                return "<span class='text-muted'><em>No data</em></span>";
            }

            try
            {
                var dataDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonData);
                if (dataDict == null || !dataDict.Any())
                {
                    return "<span class='text-muted'><em>No data</em></span>";
                }

                // Fields to ignore
                var ignoredFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Id", "Users", "CreatedById", "CreatedBy", "CreatedOn", "Password", "UserId",
                    "DesignationId", "PasswordChangedOn", "DepartmentId", "UpdatedById", "UpdatedBy",
                    "UpdatedOn", "IsDeleted", "UserCount", "DisplayName", "RoleId"
                };

                // Property mappings
                var propertyMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "UserName", "Username" },
                    { "Name", "Fullname" },
                    { "FirstName", "Firstname" },
                    { "LastName", "Lastname" },
                    { "PhoneNumber", "Phone Number" },
                    { "RoleName", "Role" },
                    { "DepartmentName", "Department" },
                    { "DesignationName", "Designation" },
                    { "DesignationDescription", "Description" },
                    { "DepartmentDescription", "Description" }
                };

                var filteredData = dataDict
                    .Where(kvp => !ignoredFields.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
                    .Select(kvp =>
                    {
                        var key = propertyMappings.TryGetValue(kvp.Key, out var mappedKey) ? mappedKey : kvp.Key;
                        var value = FormatValue(kvp.Value);
                        return $"<div class='mb-1'><strong>{WebUtility.HtmlEncode(key)}:</strong> {value}</div>";
                    })
                    .ToList();

                if (!filteredData.Any())
                {
                    return "<span class='text-muted'><em>No relevant data</em></span>";
                }

                return string.Join("", filteredData);
            }
            catch (JsonException)
            {
                return "<span class='text-danger'><em>Invalid JSON</em></span>";
            }
        }

        private string FormatValue(object value)
        {
            if (value == null)
                return "<em>-</em>";

            // Handle JsonElement directly
            if (value is JsonElement jsonElement)
            {
                try
                {
                    var formatted = JsonSerializer.Serialize(
                        jsonElement,
                        new JsonSerializerOptions { WriteIndented = true }
                    );
                    return $"<pre class='mb-0' style='font-size: 0.85em; max-height: 100px; overflow-y: auto; background: #f8f9fa; padding: 0.25rem; border-radius: 3px;'>{WebUtility.HtmlEncode(formatted)}</pre>";
                }
                catch
                {
                    return WebUtility.HtmlEncode(value.ToString());
                }
            }

            var valueStr = value.ToString();

            // Check if it's a JSON object/array string
            if (!string.IsNullOrEmpty(valueStr) &&
                ((valueStr.TrimStart().StartsWith("{") && valueStr.TrimEnd().EndsWith("}")) ||
                 (valueStr.TrimStart().StartsWith("[") && valueStr.TrimEnd().EndsWith("]"))))
            {
                try
                {
                    var jsonDoc = JsonDocument.Parse(valueStr);
                    var formatted = JsonSerializer.Serialize(
                        jsonDoc.RootElement,
                        new JsonSerializerOptions { WriteIndented = true }
                    );
                    return $"<pre class='mb-0' style='font-size: 0.85em; max-height: 100px; overflow-y: auto; background: #f8f9fa; padding: 0.25rem; border-radius: 3px;'>{WebUtility.HtmlEncode(formatted)}</pre>";
                }
                catch
                {
                    // If parsing fails, just return as regular string
                }
            }

            // Check if it's a date
            if (DateTime.TryParse(valueStr, out var date))
            {
                return $"<span class='badge badge-info'>{WebUtility.HtmlEncode(date.ToString("yyyy-MM-dd HH:mm:ss"))}</span>";
            }

            // Regular string value - truncate if too long
            if (valueStr.Length > 150)
            {
                return $"<span title='{WebUtility.HtmlEncode(valueStr)}'>{WebUtility.HtmlEncode(valueStr.Substring(0, 150))}...</span>";
            }

            return WebUtility.HtmlEncode(valueStr);
        }


    }
}
