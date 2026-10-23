using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Models.BuildUp;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    public class OrganizationStructureController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<OrganizationStructureController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrganizationStructureController(
            ApplicationDbContext db,
            AuditHelper auditHelper,
            ILogger<OrganizationStructureController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _auditHelper = auditHelper;
            _logger = logger;
            _userManager = userManager;
        }

        private async Task LogAuditTrailAsync(string action, object? oldData, object? newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "OrganizationStructure", "OrganizationStructures", oldData, newData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit trail for {Action}", action);
            }
        }

        private JsonResult ValidationErrorResponse()
        {
            var errorMessages = ModelStateHelper.GetModelErrors(ModelState);
            return Json(new { success = false, message = string.Join("; ", errorMessages) });
        }

        private static string GetEnumDisplayName(Enum value)
        {
            var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
            var display = member?.GetCustomAttribute<DisplayAttribute>();
            return display?.Name ?? value.ToString();
        }

        private static IEnumerable<OrganizationLevel> GetAllowedLevels(OrganizationLevel? parentLevel)
        {
            return Enum.GetValues<OrganizationLevel>().OrderBy(l => (int)l);
        }

        private static bool CanHaveChildren(OrganizationLevel level) => true;

        private static List<SelectListItem> BuildLevelSelectList(
            OrganizationLevel? parentLevel,
            OrganizationLevel currentLevel)
        {
            return Enum.GetValues<OrganizationLevel>()
                .OrderBy(l => (int)l)
                .Select(l => new SelectListItem
                {
                    Value = ((int)l).ToString(),
                    Text = GetEnumDisplayName(l),
                    Selected = l == currentLevel
                })
                .ToList();
        }

        private static OrganizationStructureViewModel ToViewModel(OrganizationStructure entity, string? parentName = null)
        {
            return new OrganizationStructureViewModel
            {
                Id = entity.Id,
                Code = entity.Code,
                Name = entity.Name,
                ParentId = entity.ParentId,
                Level = entity.Level,
                IsActive = entity.IsActive,
                ParentName = parentName,
                LevelDisplayName = GetEnumDisplayName(entity.Level)
            };
        }

        private static bool IsAssignmentActive(DateTime? effectiveTo) => effectiveTo == null;

        private static string GetRoleDisplayName(
            OrganizationLeadershipRole? leadershipRole,
            OrganizationFunctionalRole? functionalRole)
        {
            var parts = new List<string>();
            if (leadershipRole.HasValue)
                parts.Add(GetEnumDisplayName(leadershipRole.Value));
            if (functionalRole.HasValue)
                parts.Add(GetEnumDisplayName(functionalRole.Value));
            return parts.Count > 0 ? string.Join(" / ", parts) : "—";
        }

        private async Task PopulateEmployeeSelectListAsync()
        {
            ViewBag.Employees = await _db.Employees
                .Where(e => !e.IsDeleted && e.IsActive)
                .OrderBy(e => e.Surname).ThenBy(e => e.FirstName)
                .Select(e => new
                {
                    Id = e.Id,
                    Name = $"{e.FirstName} {e.Surname} ({e.EmployeeIdNumber})"
                })
                .AsNoTracking()
                .ToListAsync();
        }

        private async Task PopulateUnitEmployeeSelectListAsync(int organizationStructureId, Guid? includeEmployeeId = null)
        {
            await OrganizationRoleAssignmentHelper.PopulateUnitEmployeeSelectListAsync(
                this,
                _db,
                organizationStructureId,
                includeEmployeeId);
        }

        private bool HasFullOrgStructureEditPermission() =>
            User.IsInRole("Admin") ||
            WorkScheduleAccessHelper.HasSystemPermission(User, "ORGANIZATIONSTRUCTURE_VIEW");

        private async Task<bool> CanManageRoleAssignmentsForOrgAsync(int organizationStructureId)
        {
            if (HasFullOrgStructureEditPermission())
                return true;

            var employee = await EmployeePortalHelper.GetCurrentEmployeeAsync(_userManager, _db, User);
            if (employee == null)
                return false;

            return await OrganizationScopeHelper.IsLeadershipManagerForOrgAsync(
                _db,
                employee.Id,
                organizationStructureId);
        }

        private async Task<bool> IsEmployeeAssignedToUnitAsync(Guid employeeId, int organizationStructureId)
        {
            return await _db.EmployeeOrganizationAssignments
                .AsNoTracking()
                .AnyAsync(a =>
                    a.EmployeeId == employeeId &&
                    a.OrganizationStructureId == organizationStructureId &&
                    a.EffectiveTo == null);
        }

        private async Task<OrganizationStaffViewModel?> BuildStaffViewModelAsync(int organizationStructureId)
        {
            var entity = await _db.OrganizationStructures
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == organizationStructureId);
            if (entity == null)
                return null;

            string? parentName = null;
            if (entity.ParentId.HasValue)
            {
                parentName = await _db.OrganizationStructures
                    .AsNoTracking()
                    .Where(o => o.Id == entity.ParentId.Value)
                    .Select(o => o.Name)
                    .FirstOrDefaultAsync();
            }

            var employees = await _db.EmployeeOrganizationAssignments
                .AsNoTracking()
                .Where(a => a.OrganizationStructureId == organizationStructureId)
                .Include(a => a.Employee!)
                    .ThenInclude(e => e!.Designations.Where(d => d.EffectiveTo == null))
                    .ThenInclude(d => d.Designation)
                .OrderByDescending(a => a.EffectiveFrom)
                .ToListAsync();

            var roles = await _db.EmployeeRoleAssignments
                .AsNoTracking()
                .Where(a => a.OrganizationStructureId == organizationStructureId)
                .Include(a => a.Employee)
                .OrderBy(a => a.LeadershipRole)
                .ThenBy(a => a.FunctionalRole)
                .ThenByDescending(a => a.EffectiveFrom)
                .ToListAsync();

            return new OrganizationStaffViewModel
            {
                Unit = ToViewModel(entity, parentName),
                Employees = employees.Select(a => new EmployeeAssignmentRowViewModel
                {
                    Id = a.Id,
                    EmployeeId = a.EmployeeId,
                    EmployeeName = a.Employee?.FullName ?? "—",
                    EmployeeIdNumber = a.Employee?.EmployeeIdNumber ?? "—",
                    DesignationName = a.Employee?.Designations
                        .FirstOrDefault(d => d.EffectiveTo == null)?.Designation?.DesignationName,
                    EffectiveFrom = a.EffectiveFrom,
                    EffectiveTo = a.EffectiveTo,
                    IsActive = a.IsActive,
                    IsPrimary = a.IsPrimary
                }).ToList(),
                Roles = roles.Select(a => new RoleAssignmentRowViewModel
                {
                    Id = a.Id,
                    EmployeeId = a.EmployeeId,
                    EmployeeName = a.Employee?.FullName ?? "—",
                    EmployeeIdNumber = a.Employee?.EmployeeIdNumber ?? "—",
                    LeadershipRole = a.LeadershipRole,
                    FunctionalRole = a.FunctionalRole,
                    RoleDisplayName = GetRoleDisplayName(a.LeadershipRole, a.FunctionalRole),
                    EffectiveFrom = a.EffectiveFrom,
                    EffectiveTo = a.EffectiveTo,
                    IsActive = a.IsActive
                }).ToList()
            };
        }

        private async Task<List<ParentOptionViewModel>> GetParentOptionsAsync(int? excludeId = null)
        {
            var nodes = await _db.OrganizationStructures
                .AsNoTracking()
                .OrderBy(o => o.Level)
                .ThenBy(o => o.Name)
                .Select(o => new { o.Id, o.Name, o.Level, o.ParentId })
                .ToListAsync();

            HashSet<int>? excludedIds = null;
            if (excludeId.HasValue)
            {
                excludedIds = new HashSet<int> { excludeId.Value };
                var queue = new Queue<int>();
                queue.Enqueue(excludeId.Value);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var childId in nodes.Where(n => n.ParentId == current).Select(n => n.Id))
                    {
                        if (excludedIds.Add(childId))
                            queue.Enqueue(childId);
                    }
                }
            }

            return nodes
                .Where(n => excludedIds == null || !excludedIds.Contains(n.Id))
                .Select(n => new ParentOptionViewModel
                {
                    Id = n.Id,
                    Level = n.Level,
                    DisplayText = $"{GetEnumDisplayName(n.Level)} — {n.Name}"
                })
                .ToList();
        }

        private async Task<(bool IsValid, string? ErrorMessage, OrganizationLevel ResolvedLevel)> ResolveLevelAsync(
            int? parentId,
            OrganizationLevel requestedLevel,
            int? editingId = null)
        {
            if (!parentId.HasValue)
                return (true, null, requestedLevel);

            var parentExists = await _db.OrganizationStructures
                .AnyAsync(o => o.Id == parentId.Value);
            if (!parentExists)
                return (false, "Selected parent unit was not found.", requestedLevel);

            return (true, null, requestedLevel);
        }

        private async Task<OrganizationStructureIndexViewModel> BuildIndexViewModelAsync()
        {
            var nodes = await _db.OrganizationStructures
                .AsNoTracking()
                .OrderBy(o => o.Level)
                .ThenBy(o => o.Name)
                .ToListAsync();

            var childCounts = nodes
                .Where(n => n.ParentId.HasValue)
                .GroupBy(n => n.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var employeeAssignmentOrgIds = (await _db.EmployeeOrganizationAssignments
                .AsNoTracking()
                .Select(a => a.OrganizationStructureId)
                .Distinct()
                .ToListAsync())
                .ToHashSet();

            var roleAssignmentOrgIds = (await _db.EmployeeRoleAssignments
                .AsNoTracking()
                .Select(a => a.OrganizationStructureId)
                .Distinct()
                .ToListAsync())
                .ToHashSet();

            var lookup = nodes.ToDictionary(n => n.Id);
            var childrenByParent = nodes
                .Where(n => n.ParentId.HasValue)
                .GroupBy(n => n.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Name).ToList());

            OrganizationStructureTreeNodeViewModel BuildNode(OrganizationStructure entity)
            {
                var vm = ToViewModel(entity, entity.ParentId.HasValue && lookup.TryGetValue(entity.ParentId.Value, out var parent)
                    ? parent.Name
                    : null);

                var childEntities = childrenByParent.TryGetValue(entity.Id, out var kids)
                    ? kids
                    : new List<OrganizationStructure>();

                return new OrganizationStructureTreeNodeViewModel
                {
                    Node = vm,
                    HasChildren = childCounts.ContainsKey(entity.Id),
                    HasEmployeeAssignments = employeeAssignmentOrgIds.Contains(entity.Id),
                    HasRoleAssignments = roleAssignmentOrgIds.Contains(entity.Id),
                    Children = childEntities.Select(BuildNode).ToList()
                };
            }

            var roots = nodes
                .Where(n => n.ParentId == null)
                .OrderBy(n => n.Name)
                .Select(BuildNode)
                .ToList();

            return new OrganizationStructureIndexViewModel
            {
                Roots = roots,
                TotalCount = nodes.Count
            };
        }

        private async Task<OrganizationStructureFormViewModel> BuildFormViewModelAsync(OrganizationStructureViewModel model)
        {
            OrganizationLevel? parentLevel = null;
            if (model.ParentId.HasValue)
            {
                parentLevel = await _db.OrganizationStructures
                    .AsNoTracking()
                    .Where(o => o.Id == model.ParentId.Value)
                    .Select(o => o.Level)
                    .FirstOrDefaultAsync();
            }

            var allowedLevels = GetAllowedLevels(parentLevel).ToList();
            if (allowedLevels.Count > 0 && !allowedLevels.Contains(model.Level))
                model.Level = allowedLevels[0];

            return new OrganizationStructureFormViewModel
            {
                Id = model.Id,
                Code = model.Code,
                Name = model.Name,
                ParentId = model.ParentId,
                Level = model.Level,
                IsActive = model.IsActive,
                ParentName = model.ParentName,
                LevelDisplayName = model.LevelDisplayName,
                ParentOptions = await GetParentOptionsAsync(model.Id > 0 ? model.Id : null),
                LevelOptions = BuildLevelSelectList(parentLevel, model.Level)
            };
        }

        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> Index()
        {
            var viewModel = await BuildIndexViewModelAsync();
            return View(viewModel);
        }

        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> Unit(int id)
        {
            var staff = await BuildStaffViewModelAsync(id);
            if (staff == null)
                return NotFound();

            var childCount = await _db.OrganizationStructures.CountAsync(o => o.ParentId == id);
            var hasEmployees = await _db.EmployeeOrganizationAssignments.AnyAsync(a => a.OrganizationStructureId == id);
            var hasRoles = await _db.EmployeeRoleAssignments.AnyAsync(a => a.OrganizationStructureId == id);
            var breadcrumb = await BuildBreadcrumbAsync(id);

            ViewBag.ActiveMenu = "OrganizationStructure";
            ViewBag.ActiveLink = "OrganizationStructure";
            ViewBag.ActiveMainMenu = "BuildUp";

            return View(new OrganizationUnitPageViewModel
            {
                Staff = staff,
                Breadcrumb = breadcrumb,
                ChildCount = childCount,
                HasChildren = childCount > 0,
                CanDelete = childCount == 0 && !hasEmployees && !hasRoles
            });
        }

        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> Chart()
        {
            var viewModel = await BuildIndexViewModelAsync();
            ViewBag.ActiveMenu = "OrganizationStructure";
            ViewBag.ActiveLink = "OrganizationStructure";
            ViewBag.ActiveMainMenu = "BuildUp";
            return View(viewModel);
        }

        private async Task<List<OrganizationBreadcrumbItem>> BuildBreadcrumbAsync(int unitId)
        {
            var items = new List<OrganizationBreadcrumbItem>();
            var currentId = (int?)unitId;
            var visited = new HashSet<int>();

            while (currentId.HasValue && visited.Add(currentId.Value))
            {
                var node = await _db.OrganizationStructures
                    .AsNoTracking()
                    .Where(o => o.Id == currentId.Value)
                    .Select(o => new { o.Id, o.Name, o.ParentId })
                    .FirstOrDefaultAsync();

                if (node == null)
                    break;

                items.Insert(0, new OrganizationBreadcrumbItem { Id = node.Id, Name = node.Name });
                currentId = node.ParentId;
            }

            return items;
        }

        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> Create(int? parentId = null)
        {
            var model = new OrganizationStructureViewModel
            {
                IsActive = true,
                ParentId = parentId,
                Level = OrganizationLevel.Institution
            };

            if (parentId.HasValue)
            {
                var parent = await _db.OrganizationStructures
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == parentId.Value);
                if (parent == null)
                    return NotFound();

                var allowedForParent = GetAllowedLevels(parent.Level).ToList();
                if (allowedForParent.Count > 0)
                    model.Level = allowedForParent[0];
                model.ParentName = parent.Name;
            }

            var form = await BuildFormViewModelAsync(model);
            return PartialView("Partials/_CreateOrganizationStructure", form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> Create(OrganizationStructureViewModel model)
        {
            if (!ModelState.IsValid)
                return ValidationErrorResponse();

            try
            {
                if (await _db.OrganizationStructures.AnyAsync(o => o.Code == model.Code))
                    return Json(new { success = false, message = "Organization code already exists." });

                var levelResult = await ResolveLevelAsync(model.ParentId, model.Level);
                if (!levelResult.IsValid)
                    return Json(new { success = false, message = levelResult.ErrorMessage });

                var entity = new OrganizationStructure
                {
                    Code = model.Code.Trim(),
                    Name = model.Name.Trim(),
                    ParentId = model.ParentId,
                    Level = levelResult.ResolvedLevel,
                    IsActive = model.IsActive
                };

                _db.OrganizationStructures.Add(entity);
                if (await _db.SaveChangesAsync() <= 0)
                    return Json(new { success = false, message = "Failed to create organization unit." });

                await LogAuditTrailAsync("Create Organization Unit", null, entity);
                return Json(new { success = true, message = "Organization unit created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating organization unit {Code}", model.Code);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }

        [HttpGet]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> Edit(int id)
        {
            var entity = await _db.OrganizationStructures.FindAsync(id);
            if (entity == null)
                return NotFound();

            string? parentName = null;
            if (entity.ParentId.HasValue)
            {
                parentName = await _db.OrganizationStructures
                    .AsNoTracking()
                    .Where(o => o.Id == entity.ParentId.Value)
                    .Select(o => o.Name)
                    .FirstOrDefaultAsync();
            }

            var model = ToViewModel(entity, parentName);
            var form = await BuildFormViewModelAsync(model);
            return PartialView("Partials/_EditOrganizationStructure", form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> Edit(int id, OrganizationStructureViewModel model)
        {
            if (!ModelState.IsValid)
                return ValidationErrorResponse();

            var entity = await _db.OrganizationStructures.FindAsync(id);
            if (entity == null)
                return Json(new { success = false, message = "Organization unit not found." });

            if (model.ParentId == id)
                return Json(new { success = false, message = "A unit cannot be its own parent." });

            if (await _db.OrganizationStructures.AnyAsync(o => o.Code == model.Code && o.Id != id))
                return Json(new { success = false, message = "Organization code already exists." });

            if (model.ParentId.HasValue)
            {
                var descendantIds = new HashSet<int>();
                var queue = new Queue<int>();
                queue.Enqueue(id);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var children = await _db.OrganizationStructures
                        .Where(o => o.ParentId == current)
                        .Select(o => o.Id)
                        .ToListAsync();
                    foreach (var childId in children)
                    {
                        if (descendantIds.Add(childId))
                            queue.Enqueue(childId);
                    }
                }

                if (descendantIds.Contains(model.ParentId.Value))
                    return Json(new { success = false, message = "Cannot assign a descendant as the parent." });
            }

            var levelResult = await ResolveLevelAsync(model.ParentId, model.Level, id);
            if (!levelResult.IsValid)
                return Json(new { success = false, message = levelResult.ErrorMessage });

            var oldData = JsonSerializer.Deserialize<OrganizationStructure>(
                JsonSerializer.Serialize(entity));

            entity.Code = model.Code.Trim();
            entity.Name = model.Name.Trim();
            entity.ParentId = model.ParentId;
            entity.Level = levelResult.ResolvedLevel;
            entity.IsActive = model.IsActive;

            try
            {
                _db.OrganizationStructures.Update(entity);
                if (await _db.SaveChangesAsync() <= 0)
                    return Json(new { success = false, message = "No changes were made." });

                await LogAuditTrailAsync("Update Organization Unit", oldData, entity);
                return Json(new { success = true, message = "Organization unit updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating organization unit {Id}", id);
                return Json(new { success = false, message = "An error occurred while updating." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> Reparent(int id, int? newParentId)
        {
            var entity = await _db.OrganizationStructures.FindAsync(id);
            if (entity == null)
                return Json(new { success = false, message = "Organization unit not found." });

            if (newParentId == id)
                return Json(new { success = false, message = "A unit cannot be its own parent." });

            if (entity.ParentId == newParentId)
                return Json(new { success = true, message = "No change — already under that unit.", unchanged = true });

            if (newParentId.HasValue)
            {
                var parentExists = await _db.OrganizationStructures.AnyAsync(o => o.Id == newParentId.Value);
                if (!parentExists)
                    return Json(new { success = false, message = "Target parent unit was not found." });

                var descendantIds = new HashSet<int>();
                var queue = new Queue<int>();
                queue.Enqueue(id);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var children = await _db.OrganizationStructures
                        .Where(o => o.ParentId == current)
                        .Select(o => o.Id)
                        .ToListAsync();
                    foreach (var childId in children)
                    {
                        if (descendantIds.Add(childId))
                            queue.Enqueue(childId);
                    }
                }

                if (descendantIds.Contains(newParentId.Value))
                    return Json(new { success = false, message = "Cannot move a unit under one of its descendants." });
            }

            var oldData = JsonSerializer.Deserialize<OrganizationStructure>(
                JsonSerializer.Serialize(entity));

            entity.ParentId = newParentId;

            try
            {
                _db.OrganizationStructures.Update(entity);
                if (await _db.SaveChangesAsync() <= 0)
                    return Json(new { success = false, message = "No changes were made." });

                await LogAuditTrailAsync("Reparent Organization Unit", oldData, entity);
                return Json(new { success = true, message = "Organization unit moved successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reparenting organization unit {Id}", id);
                return Json(new { success = false, message = "An error occurred while moving the unit." });
            }
        }

        [HttpGet]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> ViewUnit(int id)
        {
            var entity = await _db.OrganizationStructures
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id);
            if (entity == null)
                return NotFound();

            string? parentName = null;
            if (entity.ParentId.HasValue)
            {
                parentName = await _db.OrganizationStructures
                    .AsNoTracking()
                    .Where(o => o.Id == entity.ParentId.Value)
                    .Select(o => o.Name)
                    .FirstOrDefaultAsync();
            }

            var model = ToViewModel(entity, parentName);
            return PartialView("Partials/_ViewOrganizationStructure", model);
        }

        [HttpGet]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> Delete(int id)
        {
            var entity = await _db.OrganizationStructures
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id);
            if (entity == null)
                return NotFound();

            var hasChildren = await _db.OrganizationStructures.AnyAsync(o => o.ParentId == id);
            var hasEmployees = await _db.EmployeeOrganizationAssignments.AnyAsync(a => a.OrganizationStructureId == id);
            var hasRoles = await _db.EmployeeRoleAssignments.AnyAsync(a => a.OrganizationStructureId == id);

            var model = ToViewModel(entity);

            if (hasChildren || hasEmployees || hasRoles)
            {
                ViewBag.HasChildren = hasChildren;
                ViewBag.HasEmployees = hasEmployees;
                ViewBag.HasRoles = hasRoles;
                return PartialView("Partials/_DeleteWarningOrganizationStructure", model);
            }

            return PartialView("Partials/_DeleteOrganizationStructure", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            var entity = await _db.OrganizationStructures.FindAsync(id);
            if (entity == null)
                return Json(new { success = false, message = "Organization unit not found." });

            if (await _db.OrganizationStructures.AnyAsync(o => o.ParentId == id))
                return Json(new { success = false, message = "Cannot delete a unit that has child units." });

            if (await _db.EmployeeOrganizationAssignments.AnyAsync(a => a.OrganizationStructureId == id))
                return Json(new { success = false, message = "Cannot delete a unit with employee assignments." });

            if (await _db.EmployeeRoleAssignments.AnyAsync(a => a.OrganizationStructureId == id))
                return Json(new { success = false, message = "Cannot delete a unit with role assignments." });

            var oldData = ToViewModel(entity);

            try
            {
                _db.OrganizationStructures.Remove(entity);
                if (await _db.SaveChangesAsync() <= 0)
                    return Json(new { success = false, message = "Failed to delete organization unit." });

                await LogAuditTrailAsync("Delete Organization Unit", oldData, null);
                return Json(new
                {
                    success = true,
                    message = "Organization unit deleted successfully.",
                    needsRefresh = false,
                    redirectUrl = Url.Action(nameof(Index))
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting organization unit {Id}", id);
                return Json(new { success = false, message = "An error occurred while deleting." });
            }
        }

        [HttpGet]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<JsonResult> GetLevelsForParent(int? parentId)
        {
            OrganizationLevel? parentLevel = null;
            if (parentId.HasValue)
            {
                var parent = await _db.OrganizationStructures
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == parentId.Value);
                if (parent == null)
                    return Json(new { success = false, message = "Parent not found." });

                parentLevel = parent.Level;
                if (!GetAllowedLevels(parentLevel).Any())
                    return Json(new { success = false, message = "This parent cannot have child units." });
            }

            var levels = GetAllowedLevels(parentLevel)
                .Select(l => new { value = (int)l, text = GetEnumDisplayName(l) })
                .ToList();

            return Json(new { success = true, levels });
        }

        [HttpGet]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> Staff(int id)
        {
            var model = await BuildStaffViewModelAsync(id);
            if (model == null)
                return NotFound();

            ViewData["StaffParentModal"] = "#viewModal";
            return PartialView("Partials/_StaffStructure", model);
        }

        [HttpGet]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> AddEmployeeAssignment(int organizationStructureId)
        {
            var unit = await _db.OrganizationStructures
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == organizationStructureId);
            if (unit == null)
                return NotFound();

            await PopulateEmployeeSelectListAsync();
            return PartialView("Partials/_AddEmployeeAssignment", new EmployeeOrganizationAssignmentFormViewModel
            {
                OrganizationStructureId = organizationStructureId,
                OrganizationName = unit.Name,
                EffectiveFrom = DateTime.Today
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> SaveEmployeeAssignment(EmployeeOrganizationAssignmentFormViewModel model)
        {
            if (!ModelState.IsValid)
                return ValidationErrorResponse();

            var unit = await _db.OrganizationStructures.FindAsync(model.OrganizationStructureId);
            if (unit == null)
                return Json(new { success = false, message = "Organization unit not found." });

            if (model.EffectiveTo.HasValue && model.EffectiveTo.Value.Date < model.EffectiveFrom.Date)
                return Json(new { success = false, message = "Effective to date cannot be before effective from date." });

            var employee = await _db.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == model.EmployeeId && !e.IsDeleted);
            if (employee == null)
                return Json(new { success = false, message = "Employee not found." });

            try
            {
                if (model.Id > 0)
                {
                    var entity = await _db.EmployeeOrganizationAssignments
                        .AsTracking()
                        .FirstOrDefaultAsync(a => a.Id == model.Id);
                    if (entity == null || entity.OrganizationStructureId != model.OrganizationStructureId)
                        return Json(new { success = false, message = "Assignment not found." });

                    entity.EffectiveFrom = model.EffectiveFrom.Date;
                    entity.EffectiveTo = model.EffectiveTo?.Date;

                    if (model.IsPrimary && IsAssignmentActive(entity.EffectiveTo))
                        await EmployeeOrganizationHelper.SetPrimaryOrganizationStructureAsync(
                            _db, entity.EmployeeId, entity.OrganizationStructureId);
                    else if (!model.IsPrimary && entity.IsPrimary)
                        entity.IsPrimary = false;

                    await EmployeeOrganizationHelper.EnsurePrimaryAssignmentAsync(_db, entity.EmployeeId);
                }
                else
                {
                    var existing = await _db.EmployeeOrganizationAssignments
                        .AsTracking()
                        .FirstOrDefaultAsync(a =>
                            a.EmployeeId == model.EmployeeId &&
                            a.OrganizationStructureId == model.OrganizationStructureId);

                    if (existing != null)
                    {
                        if (IsAssignmentActive(existing.EffectiveTo))
                            return Json(new { success = false, message = "This employee is already assigned to this unit." });

                        existing.EffectiveFrom = model.EffectiveFrom.Date;
                        existing.EffectiveTo = model.EffectiveTo?.Date;
                        existing.IsPrimary = model.IsPrimary;
                        _db.EmployeeOrganizationAssignments.Update(existing);
                    }
                    else
                    {
                        var hasActiveAssignments = await _db.EmployeeOrganizationAssignments.AnyAsync(a =>
                            a.EmployeeId == model.EmployeeId && a.EffectiveTo == null);

                        _db.EmployeeOrganizationAssignments.Add(new EmployeeOrganizationAssignment
                        {
                            EmployeeId = model.EmployeeId,
                            OrganizationStructureId = model.OrganizationStructureId,
                            EffectiveFrom = model.EffectiveFrom.Date,
                            EffectiveTo = model.EffectiveTo?.Date,
                            IsPrimary = model.IsPrimary || !hasActiveAssignments
                        });
                    }

                    if (model.IsPrimary)
                    {
                        await EmployeeOrganizationHelper.SetPrimaryOrganizationStructureAsync(
                            _db, model.EmployeeId, model.OrganizationStructureId);
                    }
                    else
                    {
                        await EmployeeOrganizationHelper.EnsurePrimaryAssignmentAsync(_db, model.EmployeeId);
                    }
                }

                if (await _db.SaveChangesAsync() <= 0)
                    return Json(new { success = false, message = "No changes were saved." });

                return Json(new { success = true, message = "Employee assignment saved successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving employee assignment for unit {UnitId}", model.OrganizationStructureId);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> EndEmployeeAssignment(int id)
        {
            var entity = await _db.EmployeeOrganizationAssignments.FindAsync(id);
            if (entity == null)
                return Json(new { success = false, message = "Assignment not found." });

            if (!IsAssignmentActive(entity.EffectiveTo))
                return Json(new { success = false, message = "This assignment is already ended." });

            entity.EffectiveTo = DateTime.Today;
            if (entity.IsPrimary)
                entity.IsPrimary = false;
            _db.EmployeeOrganizationAssignments.Update(entity);

            if (await _db.SaveChangesAsync() <= 0)
                return Json(new { success = false, message = "Failed to end assignment." });

            await EmployeeOrganizationHelper.EnsurePrimaryAssignmentAsync(_db, entity.EmployeeId);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Employee assignment ended." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> RemoveEmployeeAssignment(int id)
        {
            var entity = await _db.EmployeeOrganizationAssignments.FindAsync(id);
            if (entity == null)
                return Json(new { success = false, message = "Assignment not found." });

            _db.EmployeeOrganizationAssignments.Remove(entity);

            if (await _db.SaveChangesAsync() <= 0)
                return Json(new { success = false, message = "Failed to remove assignment." });

            return Json(new { success = true, message = "Employee assignment removed." });
        }

        [HttpGet]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> EditEmployeeAssignment(int id)
        {
            var entity = await _db.EmployeeOrganizationAssignments
                .AsNoTracking()
                .Include(a => a.OrganizationStructure)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (entity == null)
                return NotFound();

            await PopulateEmployeeSelectListAsync();
            return PartialView("Partials/_AddEmployeeAssignment", new EmployeeOrganizationAssignmentFormViewModel
            {
                Id = entity.Id,
                OrganizationStructureId = entity.OrganizationStructureId,
                OrganizationName = entity.OrganizationStructure?.Name ?? "",
                EmployeeId = entity.EmployeeId,
                EffectiveFrom = entity.EffectiveFrom,
                EffectiveTo = entity.EffectiveTo,
                IsPrimary = entity.IsPrimary
            });
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult> AddRoleAssignment(
            int organizationStructureId,
            OrganizationFunctionalRole? functionalRole = null)
        {
            if (!await CanManageRoleAssignmentsForOrgAsync(organizationStructureId))
                return Unauthorized();

            var unit = await _db.OrganizationStructures
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == organizationStructureId);
            if (unit == null)
                return NotFound();

            await PopulateUnitEmployeeSelectListAsync(organizationStructureId);
            return PartialView("Partials/_AddRoleAssignment", new EmployeeRoleAssignmentFormViewModel
            {
                OrganizationStructureId = organizationStructureId,
                OrganizationName = unit.Name,
                EffectiveFrom = DateTime.Today,
                FunctionalRole = functionalRole,
                LockFunctionalRole = functionalRole.HasValue
            });
        }

        [HttpGet]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> EditRoleAssignment(int id)
        {
            var entity = await _db.EmployeeRoleAssignments
                .AsNoTracking()
                .Include(a => a.OrganizationStructure)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (entity == null)
                return NotFound();

            await PopulateUnitEmployeeSelectListAsync(entity.OrganizationStructureId, entity.EmployeeId);
            return PartialView("Partials/_AddRoleAssignment", new EmployeeRoleAssignmentFormViewModel
            {
                Id = entity.Id,
                OrganizationStructureId = entity.OrganizationStructureId,
                OrganizationName = entity.OrganizationStructure?.Name ?? "",
                EmployeeId = entity.EmployeeId,
                LeadershipRole = entity.LeadershipRole,
                FunctionalRole = entity.FunctionalRole,
                EffectiveFrom = entity.EffectiveFrom,
                EffectiveTo = entity.EffectiveTo
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<ActionResult> SaveRoleAssignment(EmployeeRoleAssignmentFormViewModel model)
        {
            if (!ModelState.IsValid)
                return ValidationErrorResponse();

            if (!await CanManageRoleAssignmentsForOrgAsync(model.OrganizationStructureId))
                return Json(new { success = false, message = "You are not allowed to manage role assignments for this unit." });

            if (!HasFullOrgStructureEditPermission())
            {
                if (!model.FunctionalRole.HasValue)
                    return Json(new { success = false, message = "Select a functional role to assign." });

                if (model.LeadershipRole.HasValue)
                    return Json(new { success = false, message = "Leadership roles can only be assigned by organization administrators." });
            }

            if (!model.LeadershipRole.HasValue && !model.FunctionalRole.HasValue)
                return Json(new { success = false, message = "Select at least one leadership or functional role." });

            var unit = await _db.OrganizationStructures.FindAsync(model.OrganizationStructureId);
            if (unit == null)
                return Json(new { success = false, message = "Organization unit not found." });

            if (model.EffectiveTo.HasValue && model.EffectiveTo.Value.Date < model.EffectiveFrom.Date)
                return Json(new { success = false, message = "Effective to date cannot be before effective from date." });

            var employee = await _db.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == model.EmployeeId && !e.IsDeleted);
            if (employee == null)
                return Json(new { success = false, message = "Employee not found." });

            if (!await IsEmployeeAssignedToUnitAsync(model.EmployeeId, model.OrganizationStructureId))
                return Json(new { success = false, message = "Selected employee must be assigned to this organization unit on the Staff tab first." });

            if (model.LeadershipRole == OrganizationLeadershipRole.Head && IsAssignmentActive(model.EffectiveTo))
            {
                var otherHead = await _db.EmployeeRoleAssignments.AnyAsync(a =>
                    a.OrganizationStructureId == model.OrganizationStructureId &&
                    a.LeadershipRole == OrganizationLeadershipRole.Head &&
                    a.EffectiveTo == null &&
                    a.EmployeeId != model.EmployeeId &&
                    (model.Id <= 0 || a.Id != model.Id));
                if (otherHead)
                    return Json(new { success = false, message = "This unit already has an active Head. End the current Head assignment first." });
            }

            try
            {
                if (model.Id > 0)
                {
                    var entity = await _db.EmployeeRoleAssignments
                        .AsTracking()
                        .FirstOrDefaultAsync(a => a.Id == model.Id);
                    if (entity == null || entity.OrganizationStructureId != model.OrganizationStructureId)
                        return Json(new { success = false, message = "Role assignment not found." });

                    entity.EmployeeId = model.EmployeeId;
                    entity.LeadershipRole = model.LeadershipRole;
                    entity.FunctionalRole = model.FunctionalRole;
                    entity.EffectiveFrom = model.EffectiveFrom.Date;
                    entity.EffectiveTo = model.EffectiveTo?.Date;
                }
                else
                {
                    var existing = await _db.EmployeeRoleAssignments
                        .AsTracking()
                        .FirstOrDefaultAsync(a =>
                            a.OrganizationStructureId == model.OrganizationStructureId &&
                            a.EmployeeId == model.EmployeeId &&
                            a.LeadershipRole == model.LeadershipRole &&
                            a.FunctionalRole == model.FunctionalRole);

                    if (existing != null)
                    {
                        if (existing.IsActive)
                            return Json(new { success = false, message = "This employee already has this role on this unit." });

                        existing.EffectiveFrom = model.EffectiveFrom.Date;
                        existing.EffectiveTo = model.EffectiveTo?.Date;
                        _db.EmployeeRoleAssignments.Update(existing);
                    }
                    else
                    {
                        _db.EmployeeRoleAssignments.Add(new EmployeeRoleAssignment
                        {
                            OrganizationStructureId = model.OrganizationStructureId,
                            EmployeeId = model.EmployeeId,
                            LeadershipRole = model.LeadershipRole,
                            FunctionalRole = model.FunctionalRole,
                            EffectiveFrom = model.EffectiveFrom.Date,
                            EffectiveTo = model.EffectiveTo?.Date
                        });
                    }
                }

                if (await _db.SaveChangesAsync() <= 0)
                    return Json(new { success = false, message = "No changes were saved." });

                return Json(new { success = true, message = "Role assignment saved successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving role assignment for unit {UnitId}", model.OrganizationStructureId);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> EndRoleAssignment(int id)
        {
            var entity = await _db.EmployeeRoleAssignments.FindAsync(id);
            if (entity == null)
                return Json(new { success = false, message = "Role assignment not found." });

            if (!entity.IsActive)
                return Json(new { success = false, message = "This role assignment is already inactive." });

            entity.EffectiveTo = DateTime.Today;
            _db.EmployeeRoleAssignments.Update(entity);

            if (await _db.SaveChangesAsync() <= 0)
                return Json(new { success = false, message = "Failed to end role assignment." });

            return Json(new { success = true, message = "Role assignment ended." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("ORGANIZATIONSTRUCTURE_VIEW")]
        public async Task<ActionResult> RemoveRoleAssignment(int id)
        {
            var entity = await _db.EmployeeRoleAssignments.FindAsync(id);
            if (entity == null)
                return Json(new { success = false, message = "Role assignment not found." });

            _db.EmployeeRoleAssignments.Remove(entity);

            if (await _db.SaveChangesAsync() <= 0)
                return Json(new { success = false, message = "Failed to remove role assignment." });

            return Json(new { success = true, message = "Role assignment removed." });
        }
    }
}
