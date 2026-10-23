using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Models.Workflow;
using PayrollSystem.Services;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    public class ApprovalWorkflowController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly IApprovalWorkflowService _workflowService;

        public ApprovalWorkflowController(
            ApplicationDbContext db,
            AuditHelper auditHelper,
            IApprovalWorkflowService workflowService)
        {
            _db = db;
            _auditHelper = auditHelper;
            _workflowService = workflowService;
        }

        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> GetWorkflows()
        {
            var workflows = await _db.ApprovalWorkflows
                .AsNoTracking()
                .Include(w => w.Steps)
                .OrderBy(w => w.ProcessType)
                .ToListAsync();

            var data = workflows.Select(w => new
            {
                w.Id,
                w.Name,
                processType = (int)w.ProcessType,
                processTypeDisplay = GetEnumDisplayName(w.ProcessType),
                processIcon = WorkflowDisplayHelper.GetProcessIcon(w.ProcessType),
                accentClass = WorkflowDisplayHelper.GetProcessAccentClass(w.ProcessType),
                w.IsActive,
                stepCount = w.Steps.Count,
                isFallback = w.ProcessType == WorkflowProcessType.Default
            });

            return Json(new { success = true, data });
        }

        [HttpGet]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public IActionResult Create()
        {
            PopulateProcessTypeSelectList();
            return PartialView("Partials/_CreateWorkflow", new ApprovalWorkflowViewModel { IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> Create(ApprovalWorkflowViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Validation failed." });

            if (model.IsActive && await _db.ApprovalWorkflows.AnyAsync(w =>
                    w.ProcessType == model.ProcessType && w.IsActive))
            {
                return Json(new
                {
                    success = false,
                    message = $"An active workflow already exists for {GetEnumDisplayName(model.ProcessType)}."
                });
            }

            var entity = new ApprovalWorkflow
            {
                Name = model.Name.Trim(),
                ProcessType = model.ProcessType,
                IsActive = model.IsActive
            };

            _db.ApprovalWorkflows.Add(entity);
            await _db.SaveChangesAsync();
            await LogAuditAsync("Create", null, entity);

            return Json(new { success = true, message = "Workflow created successfully." });
        }

        [HttpGet]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> Edit(int id)
        {
            var entity = await _db.ApprovalWorkflows.FirstOrDefaultAsync(w => w.Id == id);
            if (entity == null) return NotFound();

            PopulateProcessTypeSelectList(entity.ProcessType);
            return PartialView("Partials/_EditWorkflow", new ApprovalWorkflowViewModel
            {
                Id = entity.Id,
                Name = entity.Name,
                ProcessType = entity.ProcessType,
                IsActive = entity.IsActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> Edit(ApprovalWorkflowViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Validation failed." });

            var entity = await _db.ApprovalWorkflows
                .AsTracking()
                .FirstOrDefaultAsync(w => w.Id == model.Id);
            if (entity == null)
                return Json(new { success = false, message = "Workflow not found." });

            if (model.IsActive && await _db.ApprovalWorkflows.AnyAsync(w =>
                    w.ProcessType == model.ProcessType && w.IsActive && w.Id != model.Id))
            {
                return Json(new
                {
                    success = false,
                    message = $"Another active workflow already exists for {GetEnumDisplayName(model.ProcessType)}."
                });
            }

            var old = new { entity.Id, entity.Name, entity.ProcessType, entity.IsActive };
            entity.Name = model.Name.Trim();
            entity.ProcessType = model.ProcessType;
            entity.IsActive = model.IsActive;

            await _db.SaveChangesAsync();
            await LogAuditAsync("Update", old, entity);

            return Json(new { success = true, message = "Workflow updated successfully." });
        }

        [HttpGet]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> Configure(int id)
        {
            var workflow = await _db.ApprovalWorkflows
                .AsNoTracking()
                .Include(w => w.Steps.OrderBy(s => s.StepOrder))
                    .ThenInclude(s => s.Approvers.OrderBy(a => a.Priority))
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workflow == null)
                return NotFound();

            ViewBag.ActiveMenu = "SystemSetting";
            ViewBag.ActiveLink = "ApprovalWorkflow";

            return View(new ApprovalWorkflowConfigureViewModel
            {
                Id = workflow.Id,
                Name = workflow.Name,
                ProcessType = workflow.ProcessType,
                ProcessTypeDisplay = GetEnumDisplayName(workflow.ProcessType),
                IsActive = workflow.IsActive,
                Steps = workflow.Steps.OrderBy(s => s.StepOrder).ToList()
            });
        }

        [HttpGet]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> Steps(int workflowId)
        {
            var workflow = await _db.ApprovalWorkflows
                .AsNoTracking()
                .Include(w => w.Steps.OrderBy(s => s.StepOrder))
                    .ThenInclude(s => s.Approvers.OrderBy(a => a.Priority))
                .FirstOrDefaultAsync(w => w.Id == workflowId);

            if (workflow == null) return NotFound();

            ViewBag.WorkflowId = workflow.Id;
            ViewBag.WorkflowName = workflow.Name;
            ViewBag.ProcessTypeDisplay = GetEnumDisplayName(workflow.ProcessType);

            return PartialView("Partials/_ManageSteps", workflow.Steps.ToList());
        }

        [HttpGet]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> CreateStep(int workflowId)
        {
            var workflow = await _db.ApprovalWorkflows.AsNoTracking().FirstOrDefaultAsync(w => w.Id == workflowId);
            if (workflow == null) return NotFound();

            var nextOrder = await _db.ApprovalWorkflowSteps
                .Where(s => s.WorkflowId == workflowId)
                .Select(s => (int?)s.StepOrder)
                .MaxAsync() ?? 0;

            return PartialView("Partials/_CreateStep", new ApprovalWorkflowStepViewModel
            {
                WorkflowId = workflowId,
                WorkflowName = workflow.Name,
                StepOrder = nextOrder + 1
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> CreateStep(ApprovalWorkflowStepViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Validation failed." });

            if (await _db.ApprovalWorkflowSteps.AnyAsync(s =>
                    s.WorkflowId == model.WorkflowId && s.StepOrder == model.StepOrder))
            {
                return Json(new { success = false, message = "Step order already exists for this workflow." });
            }

            var entity = new ApprovalWorkflowStep
            {
                WorkflowId = model.WorkflowId,
                Name = model.Name.Trim(),
                StepOrder = model.StepOrder
            };

            _db.ApprovalWorkflowSteps.Add(entity);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Workflow step added successfully." });
        }

        [HttpGet]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> EditStep(int id)
        {
            var step = await _db.ApprovalWorkflowSteps
                .Include(s => s.Workflow)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (step == null) return NotFound();

            return PartialView("Partials/_EditStep", new ApprovalWorkflowStepViewModel
            {
                Id = step.Id,
                WorkflowId = step.WorkflowId,
                WorkflowName = step.Workflow?.Name ?? string.Empty,
                Name = step.Name,
                StepOrder = step.StepOrder
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> EditStep(ApprovalWorkflowStepViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Validation failed." });

            var entity = await _db.ApprovalWorkflowSteps
                .AsTracking()
                .FirstOrDefaultAsync(s => s.Id == model.Id);
            if (entity == null)
                return Json(new { success = false, message = "Workflow step not found." });

            if (await _db.ApprovalWorkflowSteps.AnyAsync(s =>
                    s.WorkflowId == entity.WorkflowId &&
                    s.StepOrder == model.StepOrder &&
                    s.Id != model.Id))
            {
                return Json(new { success = false, message = "Step order already exists for this workflow." });
            }

            entity.Name = model.Name.Trim();
            entity.StepOrder = model.StepOrder;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Workflow step updated successfully." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> DeleteStep(int id)
        {
            var entity = await _db.ApprovalWorkflowSteps
                .AsTracking()
                .Include(s => s.Approvers)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (entity == null)
                return Json(new { success = false, message = "Workflow step not found." });

            _db.ApprovalWorkflowStepApprovers.RemoveRange(entity.Approvers);
            _db.ApprovalWorkflowSteps.Remove(entity);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Workflow step deleted successfully." });
        }

        [HttpGet]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> Approvers(int stepId)
        {
            var step = await _db.ApprovalWorkflowSteps
                .AsNoTracking()
                .Include(s => s.Workflow)
                .Include(s => s.Approvers.OrderBy(a => a.Priority))
                .FirstOrDefaultAsync(s => s.Id == stepId);

            if (step == null) return NotFound();

            ViewBag.StepId = step.Id;
            ViewBag.StepName = step.Name;
            ViewBag.WorkflowName = step.Workflow?.Name ?? string.Empty;

            return PartialView("Partials/_ManageApprovers", step.Approvers.ToList());
        }

        [HttpGet]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> CreateApprover(int stepId)
        {
            var step = await _db.ApprovalWorkflowSteps
                .AsNoTracking()
                .Include(s => s.Workflow)
                .FirstOrDefaultAsync(s => s.Id == stepId);
            if (step == null) return NotFound();

            var nextPriority = await _db.ApprovalWorkflowStepApprovers
                .Where(a => a.WorkflowStepId == stepId)
                .Select(a => (int?)a.Priority)
                .MaxAsync() ?? 0;

            PopulateApproverSelectLists();
            return PartialView("Partials/_CreateApprover", new ApprovalWorkflowStepApproverViewModel
            {
                WorkflowStepId = stepId,
                Priority = nextPriority + 1
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> CreateApprover(ApprovalWorkflowStepApproverViewModel model)
        {
            var validationError = ValidateApproverModel(model);
            if (validationError != null)
                return Json(new { success = false, message = validationError });

            if (await _db.ApprovalWorkflowStepApprovers.AnyAsync(a =>
                    a.WorkflowStepId == model.WorkflowStepId && a.Priority == model.Priority))
            {
                return Json(new { success = false, message = "Priority already exists for this step." });
            }

            var entity = new ApprovalWorkflowStepApprover
            {
                WorkflowStepId = model.WorkflowStepId,
                SourceType = model.SourceType,
                OrganizationLevel = model.OrganizationLevel,
                LeadershipRole = model.LeadershipRole,
                FunctionalRole = model.FunctionalRole,
                Priority = model.Priority
            };

            _db.ApprovalWorkflowStepApprovers.Add(entity);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Approver definition added successfully." });
        }

        [HttpGet]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> EditApprover(int id)
        {
            var entity = await _db.ApprovalWorkflowStepApprovers.FirstOrDefaultAsync(a => a.Id == id);
            if (entity == null) return NotFound();

            PopulateApproverSelectLists(entity.SourceType, entity.OrganizationLevel,
                entity.LeadershipRole, entity.FunctionalRole);

            return PartialView("Partials/_EditApprover", new ApprovalWorkflowStepApproverViewModel
            {
                Id = entity.Id,
                WorkflowStepId = entity.WorkflowStepId,
                SourceType = entity.SourceType,
                OrganizationLevel = entity.OrganizationLevel,
                LeadershipRole = entity.LeadershipRole,
                FunctionalRole = entity.FunctionalRole,
                Priority = entity.Priority
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> EditApprover(ApprovalWorkflowStepApproverViewModel model)
        {
            var validationError = ValidateApproverModel(model);
            if (validationError != null)
                return Json(new { success = false, message = validationError });

            var entity = await _db.ApprovalWorkflowStepApprovers
                .AsTracking()
                .FirstOrDefaultAsync(a => a.Id == model.Id);
            if (entity == null)
                return Json(new { success = false, message = "Approver definition not found." });

            if (await _db.ApprovalWorkflowStepApprovers.AnyAsync(a =>
                    a.WorkflowStepId == entity.WorkflowStepId &&
                    a.Priority == model.Priority &&
                    a.Id != model.Id))
            {
                return Json(new { success = false, message = "Priority already exists for this step." });
            }

            entity.SourceType = model.SourceType;
            entity.OrganizationLevel = model.OrganizationLevel;
            entity.LeadershipRole = model.LeadershipRole;
            entity.FunctionalRole = model.FunctionalRole;
            entity.Priority = model.Priority;

            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Approver definition updated successfully." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> DeleteApprover(int id)
        {
            var entity = await _db.ApprovalWorkflowStepApprovers
                .AsTracking()
                .FirstOrDefaultAsync(a => a.Id == id);
            if (entity == null)
                return Json(new { success = false, message = "Approver definition not found." });

            _db.ApprovalWorkflowStepApprovers.Remove(entity);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Approver definition deleted successfully." });
        }

        [HttpGet]
        [Permission("APPROVAL_WORKFLOW_VIEW")]
        public async Task<IActionResult> Timeline(int requestId)
        {
            var timeline = await _workflowService.GetTimelineAsync(requestId);
            if (timeline == null) return NotFound();
            return PartialView("Partials/_ApprovalTimeline", timeline);
        }

        private static string? ValidateApproverModel(ApprovalWorkflowStepApproverViewModel model)
        {
            if (model.SourceType == ApproverSourceType.LeadershipRole && !model.LeadershipRole.HasValue)
                return "Leadership role is required.";
            if (model.SourceType == ApproverSourceType.FunctionalRole && !model.FunctionalRole.HasValue)
                return "Functional role is required.";
            if (model.Priority < 1)
                return "Priority must be at least 1.";
            return null;
        }

        private void PopulateProcessTypeSelectList(WorkflowProcessType? selected = null)
        {
            ViewBag.ProcessTypes = Enum.GetValues<WorkflowProcessType>()
                .Select(p => new SelectListItem
                {
                    Value = ((int)p).ToString(),
                    Text = GetEnumDisplayName(p),
                    Selected = selected.HasValue && selected.Value == p
                })
                .ToList();
        }

        private void PopulateApproverSelectLists(
            ApproverSourceType? sourceType = null,
            OrganizationLevel? orgLevel = null,
            OrganizationLeadershipRole? leadershipRole = null,
            OrganizationFunctionalRole? functionalRole = null)
        {
            ViewBag.SourceTypes = Enum.GetValues<ApproverSourceType>()
                .Select(s => new SelectListItem
                {
                    Value = ((int)s).ToString(),
                    Text = GetEnumDisplayName(s),
                    Selected = sourceType.HasValue && sourceType.Value == s
                }).ToList();

            ViewBag.OrganizationLevels = Enum.GetValues<OrganizationLevel>()
                .OrderBy(l => (int)l)
                .Select(l => new SelectListItem
                {
                    Value = ((int)l).ToString(),
                    Text = GetEnumDisplayName(l),
                    Selected = orgLevel.HasValue && orgLevel.Value == l
                }).ToList();

            ViewBag.LeadershipRoles = Enum.GetValues<OrganizationLeadershipRole>()
                .Select(r => new SelectListItem
                {
                    Value = ((int)r).ToString(),
                    Text = GetEnumDisplayName(r),
                    Selected = leadershipRole.HasValue && leadershipRole.Value == r
                }).ToList();

            ViewBag.FunctionalRoles = Enum.GetValues<OrganizationFunctionalRole>()
                .Select(r => new SelectListItem
                {
                    Value = ((int)r).ToString(),
                    Text = GetEnumDisplayName(r),
                    Selected = functionalRole.HasValue && functionalRole.Value == r
                }).ToList();
        }

        private static string GetEnumDisplayName(Enum value)
        {
            var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
            var display = member?.GetCustomAttribute<DisplayAttribute>();
            return display?.Name ?? value.ToString();
        }

        private async Task LogAuditAsync(string action, object? oldData, object newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "ApprovalWorkflow", "ApprovalWorkflows", oldData, newData);
            }
            catch
            {
                // Audit failures should not block workflow configuration.
            }
        }
    }
}
