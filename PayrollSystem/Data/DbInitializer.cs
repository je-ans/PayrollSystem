using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.Interfaces;
using PayrollSystem.Models;
using PayrollSystem.Models.Workflow;
using PayrollSystem.Utility;

namespace PayrollSystem.Data
{
    public class DbInitializer : IDbInitializer
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _db;

        public DbInitializer(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ApplicationDbContext db)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _db = db;
        }

        public void Initialize()
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            strategy.Execute(() =>
            {
                using var transaction = _db.Database.BeginTransaction();
                try
                {
                    // 1. Apply pending migrations
                    if (_db.Database.GetPendingMigrations().Any())
                    {
                        _db.Database.Migrate();
                    }

                    // 2. Create Admin role if it doesn't exist
                    var adminRole = _roleManager.FindByNameAsync("Admin").GetAwaiter().GetResult();

                    if (adminRole == null)
                    {
                        adminRole = new ApplicationRole
                        {
                            Name = "Admin",
                            NormalizedName = "ADMIN",
                            Description = "Administrator role with full permissions",
                            ConcurrencyStamp = Guid.NewGuid().ToString(),
                            Remarks = $"This role was automatically created by the system on {DateTime.Now:yyyy-MM-dd HH:mm:ss}.",
                            IsActive = true
                        };

                        var roleResult = _roleManager.CreateAsync(adminRole).GetAwaiter().GetResult();
                        if (!roleResult.Succeeded)
                        {
                            throw new Exception($"Role creation failed: {string.Join(", ", roleResult.Errors)}");
                        }

                        // Reload to get the generated ID
                        adminRole = _roleManager.FindByNameAsync("Admin").GetAwaiter().GetResult();
                    }

                    // 3. Create admin user if it doesn't exist
                    var adminEmail = "admin@gmail.com";
                    var adminUser = _userManager.FindByEmailAsync(adminEmail).GetAwaiter().GetResult();

                    if (adminUser == null)
                    {
                        adminUser = new ApplicationUser
                        {
                            UserName = "admin",
                            Email = adminEmail,
                            FirstName = "Admin",
                            LastName = "Istrator",
                            FullName = "Admin Istrator",
                            NormalizedUserName = "ADMIN",
                            NormalizedEmail = "ADMIN@GMAIL.COM",
                            PhoneNumber = "1112223333",
                            EmailConfirmed = true,
                            SecurityStamp = Guid.NewGuid().ToString(),
                            CreatedOn = DateTime.Now,
                            IsActive = true,
                            RoleId = adminRole.Id // Set RoleId directly here
                        };

                        var createResult = _userManager.CreateAsync(adminUser, "Admin@1234").GetAwaiter().GetResult();
                        if (!createResult.Succeeded)
                        {
                            throw new Exception($"User creation failed: {string.Join(", ", createResult.Errors)}");
                        }

                        // 4. Add to Admin role (this maintains Identity system's role mapping)
                        var addToRoleResult = _userManager.AddToRoleAsync(adminUser, "Admin").GetAwaiter().GetResult();
                        if (!addToRoleResult.Succeeded)
                        {
                            throw new Exception($"Adding to role failed: {string.Join(", ", addToRoleResult.Errors)}");
                        }

                        // Verify RoleId was set
                        if (string.IsNullOrEmpty(adminUser.RoleId))
                        {
                            adminUser.RoleId = adminRole.Id;
                            var updateResult = _userManager.UpdateAsync(adminUser).GetAwaiter().GetResult();
                            if (!updateResult.Succeeded)
                            {
                                throw new Exception($"Updating user role failed: {string.Join(", ", updateResult.Errors)}");
                            }
                        }
                    }
                    else if (string.IsNullOrEmpty(adminUser.RoleId))
                    {
                        // Update existing admin user with role if missing
                        adminUser.RoleId = adminRole.Id;
                        var updateResult = _userManager.UpdateAsync(adminUser).GetAwaiter().GetResult();
                        if (!updateResult.Succeeded)
                        {
                            throw new Exception($"Updating user role failed: {string.Join(", ", updateResult.Errors)}");
                        }

                        // Ensure they're in the Admin role
                        if (!_userManager.IsInRoleAsync(adminUser, "Admin").GetAwaiter().GetResult())
                        {
                            var addToRoleResult = _userManager.AddToRoleAsync(adminUser, "Admin").GetAwaiter().GetResult();
                            if (!addToRoleResult.Succeeded)
                            {
                                throw new Exception($"Adding to role failed: {string.Join(", ", addToRoleResult.Errors)}");
                            }
                        }
                    }

                    // 5. Seed SystemTasks (Permissions)
                    if (!_db.SystemTasks.Any())
                    {
                        var initialSystemTaskData = new[]
                        {
                            // Parent Tasks (Modules)
                            new { Code = "DASHBOARD", TaskName = "Dashboard", ParentCode = (string?)null, CreatedById = adminUser.Id, CreatedOn = DateTime.Now  },
                            new { Code = "USER_MANAGEMENT", TaskName = "User Management", ParentCode = (string?)null, CreatedById = adminUser.Id, CreatedOn = DateTime.Now  },
                            new { Code = "SYSTEMSETTINGS", TaskName = "System Settings", ParentCode = (string?)null, CreatedById = adminUser.Id, CreatedOn = DateTime.Now  },
                            new { Code = "DEPARTMENT_MANAGEMENT", TaskName = "Department Management", ParentCode = (string?)null, CreatedById = adminUser.Id, CreatedOn = DateTime.Now  },
                            new { Code = "DESIGNATION_MANAGEMENT", TaskName = "Designation Management", ParentCode = (string?)null, CreatedById = adminUser.Id, CreatedOn = DateTime.Now  },
                            new { Code = "AUDIT_TRAILS", TaskName = "Audit Trails", ParentCode = (string?)null, CreatedById = adminUser.Id, CreatedOn = DateTime.Now  },

                            // User Management Child Tasks
                            new { Code = "USER_VIEW", TaskName = "User Accounts", ParentCode = "USER_MANAGEMENT", CreatedById = adminUser.Id, CreatedOn = DateTime.Now  },
            
                            // Department Management Child Tasks
                            new { Code = "DEPARTMENT_VIEW", TaskName = "Departments", ParentCode = "DEPARTMENT_MANAGEMENT", CreatedById = adminUser.Id, CreatedOn = DateTime.Now  },

                            // Designation Management Child Tasks
                            new { Code = "DESIGNATION_VIEW", TaskName = "Designations", ParentCode = "DESIGNATION_MANAGEMENT", CreatedById = adminUser.Id, CreatedOn = DateTime.Now  },

                            // Organization Structure
                            new { Code = "ORGANIZATIONSTRUCTURE_VIEW", TaskName = "Organization Structure", ParentCode = (string?)null, CreatedById = adminUser.Id, CreatedOn = DateTime.Now  },

                            // Settings Child Tasks
                            new { Code = "ROLE_VIEW", TaskName = "Roles", ParentCode = "SYSTEMSETTINGS", CreatedById = adminUser.Id, CreatedOn = DateTime.Now  },
                            new { Code = "PERMISSION_ASSIGN", TaskName = "Assign Permissions", ParentCode = "SYSTEMSETTINGS", CreatedById = adminUser.Id, CreatedOn = DateTime.Now  },

                            new { Code = "SYSTEMTASKS_VIEW", TaskName = "System Tasks", ParentCode = "SYSTEMSETTINGS", CreatedById = adminUser.Id, CreatedOn = DateTime.Now  },
                            new { Code = "SYSTEMINFO_VIEW", TaskName = "System Info", ParentCode = "SYSTEMSETTINGS", CreatedById = adminUser.Id, CreatedOn = DateTime.Now  },
                        };

                        var systemTasksToAdd = new List<SystemTask>();
                        foreach (var data in initialSystemTaskData.Where(d => d.ParentCode == null))
                        {
                            systemTasksToAdd.Add(new SystemTask { Code = data.Code, TaskName = data.TaskName, CreatedById = data.CreatedById, CreatedOn = DateTime.Now });
                        }
                        _db.SystemTasks.AddRange(systemTasksToAdd);
                        _db.SaveChanges();

                        // Map the created tasks for ParentId assignment
                        var existingTasks = _db.SystemTasks.ToList();
                        var taskCodeToIdMap = existingTasks.ToDictionary(t => t.Code, t => t.Id);

                        // Add child tasks and set ParentId
                        foreach (var data in initialSystemTaskData.Where(d => d.ParentCode != null))
                        {
                            if (taskCodeToIdMap.TryGetValue(data.ParentCode!, out int parentId))
                            {
                                _db.SystemTasks.Add(new SystemTask { Code = data.Code, TaskName = data.TaskName, ParentId = parentId, CreatedById = data.CreatedById, CreatedOn = DateTime.Now });
                            }
                        }
                        _db.SaveChanges();
                    }

                    // 6. Assign all SystemTasks to the Admin role
                    if (adminRole != null && !_db.AssignRolePermissions.Any(arp => arp.RoleId == adminRole.Id))
                    {
                        var allSystemTaskIds = _db.SystemTasks.Select(st => st.Id).ToList();
                        var userId = adminUser?.Id; // Get the admin user ID

                        if (userId != null)
                        {
                            var adminPermissions = allSystemTaskIds.Select(taskId => new AssignRolePermission
                            {
                                TaskId = taskId,
                                RoleId = adminRole.Id,
                                CreatedById = userId,
                                CreatedOn = DateTime.UtcNow,
                                UpdatedById = userId,
                                UpdatedOn = DateTime.UtcNow,
                                IsDeleted = false
                            }).ToList();

                            _db.AssignRolePermissions.AddRange(adminPermissions);
                            _db.SaveChanges();
                            Console.WriteLine($"Assigned {adminPermissions.Count} permissions to Admin role.");
                        }
                        else
                        {
                            Console.WriteLine("Admin user ID not found, skipping permission assignment for Admin role.");
                        }
                    }

                    // 7. Seed Departments
                    Department adminDepartment;
                    if (!_db.Departments.Any())
                    {
                        adminDepartment = new Department
                        {
                            DepartmentName = "IT Department",
                            DepartmentDescription = "Information Technology Department",
                            IsActive = true,
                            CreatedOn = DateTime.Now,
                            CreatedById = adminUser.Id
                        };
                        _db.Departments.Add(adminDepartment);
                        _db.SaveChanges();
                    }
                    else
                    {
                        adminDepartment = _db.Departments.First();
                    }

                    // Assign admin user to IT Department
                    if (adminUser != null && adminDepartment != null && adminUser.DepartmentId == null)
                    {
                        adminUser.DepartmentId = adminDepartment.Id;
                        var updateResult = _userManager.UpdateAsync(adminUser).GetAwaiter().GetResult();
                        if (!updateResult.Succeeded)
                        {
                            throw new Exception($"Updating admin user's department failed: {string.Join(", ", updateResult.Errors)}");
                        }
                    }

                    // 8. Seed Designations
                    Designation adminDesignation;
                    if (!_db.Designations.Any())
                    {
                        adminDesignation = new Designation
                        {
                            DesignationName = "System Administrator",
                            DesignationDescription = "Manages and maintains the system",
                            IsActive = true,
                            CreatedOn = DateTime.Now,
                            CreatedById = adminUser.Id
                        };
                        _db.Designations.Add(adminDesignation);
                        _db.SaveChanges();
                    }
                    else
                    {
                        adminDesignation = _db.Designations.First();
                    }

                    // Assign admin user to System Administrator Designation
                    if (adminUser != null && adminDesignation != null && adminUser.DesignationId == null)
                    {
                        adminUser.DesignationId = adminDesignation.Id;
                        var updateResult = _userManager.UpdateAsync(adminUser).GetAwaiter().GetResult();
                        if (!updateResult.Succeeded)
                        {
                            throw new Exception($"Updating admin user's designation failed: {string.Join(", ", updateResult.Errors)}");
                        }
                    }

                    // Ensure Shift Type permissions exist (incremental seed for existing databases)
                    EnsureShiftTypePermissions(adminUser, adminRole);
                    EnsureOrganizationStructurePermissions(adminUser, adminRole);
                    EnsureWorkSchedulePermissions(adminUser, adminRole);
                    EnsureOvertimeRequestPermissions(adminUser, adminRole);
                    EnsureHolidayCalendarPermissions(adminUser, adminRole);
                    EnsureApprovalWorkflowPermissions(adminUser, adminRole);
                    EnsureCoreModulePermissions(adminUser, adminRole);
                    SeedOperationalRoles(adminUser);
                    SeedDefaultApprovalWorkflows();
                    EnsureWorkScheduleWorkflowDefinition();
                    EnsureScheduleChangeWorkflowDefinition();
                    EnsureTimeLogJustificationWorkflowDefinition();
                    EnsureOvertimeWorkflowDefinition();
                    EnsureDutyExchangeWorkflowDefinition();
                    EnsureExchangeRestDayWorkflowDefinition();
                    EnsureUndertimeWorkflowDefinition();
                    EnsureDefaultWorkflowDefinition();

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    var errorMessage = ex.InnerException != null
                        ? $"{ex.Message} Inner: {ex.InnerException.Message}"
                        : ex.Message;

                    Console.WriteLine($"Database initialization failed: {errorMessage}");
                    throw new Exception($"Database initialization failed: {errorMessage}", ex);
                }
            });
        }

        private void EnsureTaskPermissions(ApplicationUser? adminUser, ApplicationRole? adminRole, (string Code, string TaskName)[] tasks)
        {
            if (adminUser == null) return;

            var existingCodes = _db.SystemTasks.Select(st => st.Code).ToHashSet();
            var addedTaskIds = new List<int>();

            foreach (var task in tasks)
            {
                if (existingCodes.Contains(task.Code)) continue;

                var systemTask = new SystemTask
                {
                    Code = task.Code,
                    TaskName = task.TaskName,
                    CreatedById = adminUser.Id,
                    CreatedOn = DateTime.UtcNow
                };
                _db.SystemTasks.Add(systemTask);
                _db.SaveChanges();
                addedTaskIds.Add(systemTask.Id);
            }

            AssignTaskPermissionsToRole(adminUser, adminRole, addedTaskIds);
        }

        private void EnsureModulePermissions(
            ApplicationUser? adminUser,
            ApplicationRole? adminRole,
            string parentCode,
            string parentName,
            (string Code, string TaskName)[] children)
        {
            if (adminUser == null) return;

            var parent = _db.SystemTasks.FirstOrDefault(t => t.Code == parentCode);
            if (parent == null)
            {
                parent = new SystemTask
                {
                    Code = parentCode,
                    TaskName = parentName,
                    CreatedById = adminUser.Id,
                    CreatedOn = DateTime.UtcNow
                };
                _db.SystemTasks.Add(parent);
                _db.SaveChanges();
                AssignTaskPermissionsToRole(adminUser, adminRole, new[] { parent.Id });
            }

            var existingCodes = _db.SystemTasks.Select(st => st.Code).ToHashSet();
            var addedTaskIds = new List<int>();

            foreach (var child in children)
            {
                if (existingCodes.Contains(child.Code)) continue;

                var systemTask = new SystemTask
                {
                    Code = child.Code,
                    TaskName = child.TaskName,
                    ParentId = parent.Id,
                    CreatedById = adminUser.Id,
                    CreatedOn = DateTime.UtcNow
                };
                _db.SystemTasks.Add(systemTask);
                _db.SaveChanges();
                addedTaskIds.Add(systemTask.Id);
            }

            AssignTaskPermissionsToRole(adminUser, adminRole, addedTaskIds);
        }

        private void AssignTaskPermissionsToRole(
            ApplicationUser adminUser,
            ApplicationRole? adminRole,
            IEnumerable<int> taskIds)
        {
            if (adminRole == null) return;

            foreach (var taskId in taskIds)
            {
                if (_db.AssignRolePermissions.Any(arp => arp.RoleId == adminRole.Id && arp.TaskId == taskId))
                    continue;

                _db.AssignRolePermissions.Add(new AssignRolePermission
                {
                    TaskId = taskId,
                    RoleId = adminRole.Id,
                    CreatedById = adminUser.Id,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedById = adminUser.Id,
                    UpdatedOn = DateTime.UtcNow,
                    IsDeleted = false
                });
            }

            _db.SaveChanges();
        }

        private void EnsureCoreModulePermissions(ApplicationUser? adminUser, ApplicationRole? adminRole)
        {
            // General module access: one *_VIEW permission unlocks that feature (create/edit/delete included).
            EnsureModulePermissions(adminUser, adminRole, "EMPLOYEE_MANAGEMENT", "Employee Management", new[]
            {
                ("EMPLOYEE_VIEW", "Employee Management"),
                ("EMPLOYEEALLOWANCE_VIEW", "Employee Allowances"),
                ("EMPLOYEELOAN_VIEW", "Employee Loans"),
                ("DEDUCTIONMASTER_VIEW", "Employee Deductions / Deduction Types"),
                ("SALARY_HISTORY_VIEW", "Salary History"),
                ("PERSONALINFORMATION_VIEW", "Personal Information")
            });

            EnsureModulePermissions(adminUser, adminRole, "ATTENDANCE_MONITORING", "Attendance Monitoring", new[]
            {
                ("TIME_ENTRY_VIEW", "Time Entries"),
                ("DTR_VIEW", "Daily Time Record")
            });

            EnsureModulePermissions(adminUser, adminRole, "PAYROLL_MANAGEMENT", "Payroll Management", new[]
            {
                ("PAYROLL_PERIOD_VIEW", "Payroll Periods"),
                ("PAYROLL_VIEW", "Payroll"),
                ("PAYSLIP_VIEW", "Payslips")
            });

            EnsureModulePermissions(adminUser, adminRole, "MASTER_DATA", "Master Data", new[]
            {
                ("RATETYPE_VIEW", "Rate Types"),
                ("DAYTYPE_VIEW", "Day Types"),
                ("ALLOWANCETYPE_VIEW", "Allowance Types"),
                ("GOVERNMENTCONTRIBUTIONTYPE_VIEW", "Government Contribution Types"),
                ("GOVERNMENTCONTRIBUTIONTABLE_VIEW", "Government Contribution Tables"),
                ("TAXTABLE_VIEW", "Tax Tables"),
                ("PAYROLLCONFIG_VIEW", "Payroll Configuration")
            });

            EnsureModulePermissions(adminUser, adminRole, "EMPLOYEE_PORTAL", "Employee Portal", new[]
            {
                ("EMPLOYEE_PORTAL_VIEW", "Employee Portal")
            });
        }

        private void SeedOperationalRoles(ApplicationUser? adminUser)
        {
            if (adminUser == null) return;

            // Self-service portal only — no admin console permissions.
            SeedRoleWithPermissions(
                adminUser,
                "Employee",
                "Standard employee self-service portal access",
                "EMPLOYEE_PORTAL:EMPLOYEE_PORTAL_VIEW",
                "EMPLOYEE_PORTAL_VIEW");

            SeedRoleWithPermissions(
                adminUser,
                "HR Staff",
                "HR operations — employees, schedules, and organization",
                "DASHBOARD",
                "EMPLOYEE_MANAGEMENT:EMPLOYEE_VIEW",
                "EMPLOYEE_MANAGEMENT:EMPLOYEEALLOWANCE_VIEW",
                "WORK_SCHEDULE_VIEW",
                "SCHEDULE_CHANGE_VIEW",
                "ORGANIZATIONSTRUCTURE_VIEW",
                "APPROVAL_WORKFLOW_VIEW");

            SeedRoleWithPermissions(
                adminUser,
                "Payroll Staff",
                "Payroll operations — periods, runs, and payslips",
                "DASHBOARD",
                "EMPLOYEE_MANAGEMENT:EMPLOYEE_VIEW",
                "PAYROLL_MANAGEMENT:PAYROLL_PERIOD_VIEW",
                "PAYROLL_MANAGEMENT:PAYROLL_VIEW",
                "PAYROLL_MANAGEMENT:PAYSLIP_VIEW",
                "MASTER_DATA:PAYROLLCONFIG_VIEW");
        }

        private void SeedRoleWithPermissions(
            ApplicationUser adminUser,
            string roleName,
            string description,
            params string[] permissionKeys)
        {
            var role = _roleManager.FindByNameAsync(roleName).GetAwaiter().GetResult();
            if (role == null)
            {
                role = new ApplicationRole
                {
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant(),
                    Description = description,
                    Remarks = $"Auto-generated operational role on {DateTime.Now:yyyy-MM-dd HH:mm:ss}.",
                    IsActive = true,
                    ConcurrencyStamp = Guid.NewGuid().ToString()
                };

                var createResult = _roleManager.CreateAsync(role).GetAwaiter().GetResult();
                if (!createResult.Succeeded)
                    return;

                role = _roleManager.FindByNameAsync(roleName).GetAwaiter().GetResult();
            }

            if (role == null || _db.AssignRolePermissions.Any(arp => arp.RoleId == role.Id))
                return;

            var taskIds = new List<int>();
            foreach (var permissionKey in permissionKeys)
            {
                var task = ResolveSystemTask(permissionKey);
                if (task != null)
                    taskIds.Add(task.Id);
            }

            AssignTaskPermissionsToRole(adminUser, role, taskIds.Distinct());
        }

        private SystemTask? ResolveSystemTask(string permissionKey)
        {
            if (permissionKey.Contains(':'))
            {
                var parts = permissionKey.Split(':', 2);
                return _db.SystemTasks
                    .Include(t => t.Parent)
                    .FirstOrDefault(t =>
                        t.Code == parts[1] &&
                        t.Parent != null &&
                        t.Parent.Code == parts[0]);
            }

            return _db.SystemTasks.FirstOrDefault(t => t.Code == permissionKey);
        }

        private void EnsureShiftTypePermissions(ApplicationUser? adminUser, ApplicationRole? adminRole)
        {
            EnsureTaskPermissions(adminUser, adminRole, new[]
            {
                ("SHIFTTYPE_VIEW", "Shift Types")
            });
        }

        private void EnsureOrganizationStructurePermissions(ApplicationUser? adminUser, ApplicationRole? adminRole)
        {
            EnsureTaskPermissions(adminUser, adminRole, new[]
            {
                ("ORGANIZATIONSTRUCTURE_VIEW", "Organization Structure")
            });
        }

        private void EnsureWorkSchedulePermissions(ApplicationUser? adminUser, ApplicationRole? adminRole)
        {
            // General module access. Unit-level edit/approve still comes from org roles
            // (Scheduler, ScheduleApprover, Head, etc.) via WorkScheduleAccessHelper.
            EnsureTaskPermissions(adminUser, adminRole, new[]
            {
                ("WORK_SCHEDULE_VIEW", "Work Schedules"),
                ("SCHEDULE_CHANGE_VIEW", "Schedule Change Requests")
            });
        }

        private void EnsureOvertimeRequestPermissions(ApplicationUser? adminUser, ApplicationRole? adminRole)
        {
            EnsureTaskPermissions(adminUser, adminRole, new[]
            {
                ("OVERTIME_REQUEST_VIEW", "Overtime Requests")
            });
        }

        private void EnsureHolidayCalendarPermissions(ApplicationUser? adminUser, ApplicationRole? adminRole)
        {
            EnsureTaskPermissions(adminUser, adminRole, new[]
            {
                ("HOLIDAY_VIEW", "Holidays"),
                ("WORKING_CALENDAR_VIEW", "Working Calendar")
            });
        }

        private void EnsureApprovalWorkflowPermissions(ApplicationUser? adminUser, ApplicationRole? adminRole)
        {
            EnsureTaskPermissions(adminUser, adminRole, new[]
            {
                ("APPROVAL_WORKFLOW_VIEW", "Approval Workflows")
            });
        }

        private void SeedDefaultApprovalWorkflows()
        {
            if (_db.ApprovalWorkflows.Any())
                return;

            var workSchedule = ApprovalWorkflowSeedBuilder.CreateWorkScheduleWorkflow();
            var scheduleChange = ApprovalWorkflowSeedBuilder.CreateScheduleChangeWorkflow();
            var timeLogJustification = ApprovalWorkflowSeedBuilder.CreateTimeLogJustificationWorkflow();
            var overtime = ApprovalWorkflowSeedBuilder.CreateOvertimeWorkflow();
            var dutyExchange = ApprovalWorkflowSeedBuilder.CreateDutyExchangeWorkflow();
            var exchangeRestDay = ApprovalWorkflowSeedBuilder.CreateExchangeRestDayWorkflow();
            var undertime = ApprovalWorkflowSeedBuilder.CreateUndertimeWorkflow();
            var defaultWorkflow = ApprovalWorkflowSeedBuilder.CreateDefaultWorkflow();

            var payroll = new ApprovalWorkflow
            {
                Name = "Payroll Workflow",
                ProcessType = WorkflowProcessType.Payroll,
                IsActive = true,
                Steps =
                {
                    CreateDefaultStep(1, "Department Head", ApproverSourceType.LeadershipRole,
                        OrganizationLevel.Department, leadershipRole: OrganizationLeadershipRole.Head),
                    CreateDefaultStep(2, "HR Approver", ApproverSourceType.FunctionalRole,
                        OrganizationLevel.Institution, functionalRole: OrganizationFunctionalRole.HRApprover),
                    CreateDefaultStep(3, "Payroll Approver", ApproverSourceType.FunctionalRole,
                        OrganizationLevel.Institution, functionalRole: OrganizationFunctionalRole.PayrollApprover)
                }
            };

            _db.ApprovalWorkflows.AddRange(workSchedule, scheduleChange, timeLogJustification, overtime, dutyExchange, exchangeRestDay, undertime, defaultWorkflow, payroll);
            _db.SaveChanges();
        }

        private void EnsureWorkScheduleWorkflowDefinition()
        {
            var workflow = _db.ApprovalWorkflows
                .Include(w => w.Steps)
                    .ThenInclude(s => s.Approvers)
                .FirstOrDefault(w => w.ProcessType == WorkflowProcessType.WorkSchedule && w.IsActive);

            if (workflow == null)
                return;

            bool SameDefinition(ApprovalWorkflow existing, ApprovalWorkflow template)
            {
                var existingSteps = existing.Steps.OrderBy(s => s.StepOrder).ToList();
                var templateSteps = template.Steps.OrderBy(s => s.StepOrder).ToList();
                if (existingSteps.Count != templateSteps.Count)
                    return false;

                for (var i = 0; i < existingSteps.Count; i++)
                {
                    if (existingSteps[i].StepOrder != templateSteps[i].StepOrder)
                        return false;
                    if (!string.Equals(existingSteps[i].Name, templateSteps[i].Name, StringComparison.Ordinal))
                        return false;

                    var exApprovers = existingSteps[i].Approvers
                        .OrderBy(a => a.Priority)
                        .Select(a => new
                        {
                            a.SourceType,
                            a.OrganizationLevel,
                            a.LeadershipRole,
                            a.FunctionalRole,
                            a.Priority
                        })
                        .ToList();

                    var tplApprovers = templateSteps[i].Approvers
                        .OrderBy(a => a.Priority)
                        .Select(a => new
                        {
                            a.SourceType,
                            a.OrganizationLevel,
                            a.LeadershipRole,
                            a.FunctionalRole,
                            a.Priority
                        })
                        .ToList();

                    if (exApprovers.Count != tplApprovers.Count)
                        return false;

                    for (var j = 0; j < exApprovers.Count; j++)
                    {
                        if (!Equals(exApprovers[j], tplApprovers[j]))
                            return false;
                    }
                }

                return true;
            }

            var template = ApprovalWorkflowSeedBuilder.CreateWorkScheduleWorkflow();
            var needsUpdate = !SameDefinition(workflow, template);

            if (!needsUpdate)
                return;

            foreach (var step in workflow.Steps.ToList())
            {
                _db.ApprovalWorkflowStepApprovers.RemoveRange(step.Approvers);
                _db.ApprovalWorkflowSteps.Remove(step);
            }

            workflow.Name = template.Name;

            foreach (var templateStep in template.Steps.OrderBy(s => s.StepOrder))
            {
                _db.ApprovalWorkflowSteps.Add(new ApprovalWorkflowStep
                {
                    WorkflowId = workflow.Id,
                    StepOrder = templateStep.StepOrder,
                    Name = templateStep.Name,
                    Approvers = templateStep.Approvers.Select(a => new ApprovalWorkflowStepApprover
                    {
                        SourceType = a.SourceType,
                        OrganizationLevel = a.OrganizationLevel,
                        LeadershipRole = a.LeadershipRole,
                        FunctionalRole = a.FunctionalRole,
                        Priority = a.Priority
                    }).ToList()
                });
            }

            _db.SaveChanges();
        }

        private void EnsureScheduleChangeWorkflowDefinition()
        {
            var workflow = _db.ApprovalWorkflows
                .Include(w => w.Steps)
                    .ThenInclude(s => s.Approvers)
                .FirstOrDefault(w => w.ProcessType == WorkflowProcessType.ScheduleChange && w.IsActive);

            if (workflow == null)
                return;

            bool SameDefinition(ApprovalWorkflow existing, ApprovalWorkflow template)
            {
                var existingSteps = existing.Steps.OrderBy(s => s.StepOrder).ToList();
                var templateSteps = template.Steps.OrderBy(s => s.StepOrder).ToList();
                if (existingSteps.Count != templateSteps.Count)
                    return false;

                for (var i = 0; i < existingSteps.Count; i++)
                {
                    if (existingSteps[i].StepOrder != templateSteps[i].StepOrder)
                        return false;
                    if (!string.Equals(existingSteps[i].Name, templateSteps[i].Name, StringComparison.Ordinal))
                        return false;

                    var exApprovers = existingSteps[i].Approvers
                        .OrderBy(a => a.Priority)
                        .Select(a => new
                        {
                            a.SourceType,
                            a.OrganizationLevel,
                            a.LeadershipRole,
                            a.FunctionalRole,
                            a.Priority
                        })
                        .ToList();

                    var tplApprovers = templateSteps[i].Approvers
                        .OrderBy(a => a.Priority)
                        .Select(a => new
                        {
                            a.SourceType,
                            a.OrganizationLevel,
                            a.LeadershipRole,
                            a.FunctionalRole,
                            a.Priority
                        })
                        .ToList();

                    if (exApprovers.Count != tplApprovers.Count)
                        return false;

                    for (var j = 0; j < exApprovers.Count; j++)
                    {
                        if (!Equals(exApprovers[j], tplApprovers[j]))
                            return false;
                    }
                }

                return true;
            }

            var template = ApprovalWorkflowSeedBuilder.CreateScheduleChangeWorkflow();
            var needsUpdate = !SameDefinition(workflow, template);

            if (!needsUpdate)
                return;

            foreach (var step in workflow.Steps.ToList())
            {
                _db.ApprovalWorkflowStepApprovers.RemoveRange(step.Approvers);
                _db.ApprovalWorkflowSteps.Remove(step);
            }

            workflow.Name = template.Name;

            foreach (var templateStep in template.Steps.OrderBy(s => s.StepOrder))
            {
                _db.ApprovalWorkflowSteps.Add(new ApprovalWorkflowStep
                {
                    WorkflowId = workflow.Id,
                    StepOrder = templateStep.StepOrder,
                    Name = templateStep.Name,
                    Approvers = templateStep.Approvers.Select(a => new ApprovalWorkflowStepApprover
                    {
                        SourceType = a.SourceType,
                        OrganizationLevel = a.OrganizationLevel,
                        LeadershipRole = a.LeadershipRole,
                        FunctionalRole = a.FunctionalRole,
                        Priority = a.Priority
                    }).ToList()
                });
            }

            _db.SaveChanges();
        }

        private void EnsureTimeLogJustificationWorkflowDefinition()
        {
            var workflow = _db.ApprovalWorkflows
                .Include(w => w.Steps)
                    .ThenInclude(s => s.Approvers)
                .FirstOrDefault(w => w.ProcessType == WorkflowProcessType.TimeLogJustification && w.IsActive);

            if (workflow == null)
            {
                var legacyWorkflow = _db.ApprovalWorkflows
                    .Include(w => w.Steps)
                        .ThenInclude(s => s.Approvers)
                    .FirstOrDefault(w => w.ProcessType == WorkflowProcessType.Dtr && w.IsActive);

                if (legacyWorkflow != null)
                {
                    legacyWorkflow.ProcessType = WorkflowProcessType.TimeLogJustification;
                    legacyWorkflow.Name = "Time Log Justification Workflow";

                    var legacyRequests = _db.ApprovalRequests
                        .Where(r => r.ProcessType == WorkflowProcessType.Dtr)
                        .ToList();
                    foreach (var request in legacyRequests)
                        request.ProcessType = WorkflowProcessType.TimeLogJustification;

                    _db.SaveChanges();
                    return;
                }

                _db.ApprovalWorkflows.Add(ApprovalWorkflowSeedBuilder.CreateTimeLogJustificationWorkflow());
                _db.SaveChanges();
                return;
            }

            bool SameDefinition(ApprovalWorkflow existing, ApprovalWorkflow template)
            {
                var existingSteps = existing.Steps.OrderBy(s => s.StepOrder).ToList();
                var templateSteps = template.Steps.OrderBy(s => s.StepOrder).ToList();
                if (existingSteps.Count != templateSteps.Count)
                    return false;

                for (var i = 0; i < existingSteps.Count; i++)
                {
                    if (existingSteps[i].StepOrder != templateSteps[i].StepOrder)
                        return false;
                    if (!string.Equals(existingSteps[i].Name, templateSteps[i].Name, StringComparison.Ordinal))
                        return false;

                    var exApprovers = existingSteps[i].Approvers
                        .OrderBy(a => a.Priority)
                        .Select(a => new
                        {
                            a.SourceType,
                            a.OrganizationLevel,
                            a.LeadershipRole,
                            a.FunctionalRole,
                            a.Priority
                        })
                        .ToList();

                    var tplApprovers = templateSteps[i].Approvers
                        .OrderBy(a => a.Priority)
                        .Select(a => new
                        {
                            a.SourceType,
                            a.OrganizationLevel,
                            a.LeadershipRole,
                            a.FunctionalRole,
                            a.Priority
                        })
                        .ToList();

                    if (exApprovers.Count != tplApprovers.Count)
                        return false;

                    for (var j = 0; j < exApprovers.Count; j++)
                    {
                        if (!Equals(exApprovers[j], tplApprovers[j]))
                            return false;
                    }
                }

                return true;
            }

            var template = ApprovalWorkflowSeedBuilder.CreateTimeLogJustificationWorkflow();
            var needsUpdate = !SameDefinition(workflow, template);

            if (!needsUpdate)
                return;

            foreach (var step in workflow.Steps.ToList())
            {
                _db.ApprovalWorkflowStepApprovers.RemoveRange(step.Approvers);
                _db.ApprovalWorkflowSteps.Remove(step);
            }

            workflow.Name = template.Name;

            foreach (var templateStep in template.Steps.OrderBy(s => s.StepOrder))
            {
                _db.ApprovalWorkflowSteps.Add(new ApprovalWorkflowStep
                {
                    WorkflowId = workflow.Id,
                    StepOrder = templateStep.StepOrder,
                    Name = templateStep.Name,
                    Approvers = templateStep.Approvers.Select(a => new ApprovalWorkflowStepApprover
                    {
                        SourceType = a.SourceType,
                        OrganizationLevel = a.OrganizationLevel,
                        LeadershipRole = a.LeadershipRole,
                        FunctionalRole = a.FunctionalRole,
                        Priority = a.Priority
                    }).ToList()
                });
            }

            _db.SaveChanges();
        }

        private void EnsureOvertimeWorkflowDefinition()
        {
            var workflow = _db.ApprovalWorkflows
                .Include(w => w.Steps)
                    .ThenInclude(s => s.Approvers)
                .FirstOrDefault(w => w.ProcessType == WorkflowProcessType.Overtime && w.IsActive);

            if (workflow == null)
            {
                _db.ApprovalWorkflows.Add(ApprovalWorkflowSeedBuilder.CreateOvertimeWorkflow());
                _db.SaveChanges();
                return;
            }

            bool SameDefinition(ApprovalWorkflow existing, ApprovalWorkflow template)
            {
                var existingSteps = existing.Steps.OrderBy(s => s.StepOrder).ToList();
                var templateSteps = template.Steps.OrderBy(s => s.StepOrder).ToList();
                if (existingSteps.Count != templateSteps.Count)
                    return false;

                for (var i = 0; i < existingSteps.Count; i++)
                {
                    if (existingSteps[i].StepOrder != templateSteps[i].StepOrder)
                        return false;
                    if (!string.Equals(existingSteps[i].Name, templateSteps[i].Name, StringComparison.Ordinal))
                        return false;

                    var exApprovers = existingSteps[i].Approvers
                        .OrderBy(a => a.Priority)
                        .Select(a => new
                        {
                            a.SourceType,
                            a.OrganizationLevel,
                            a.LeadershipRole,
                            a.FunctionalRole,
                            a.Priority
                        })
                        .ToList();

                    var tplApprovers = templateSteps[i].Approvers
                        .OrderBy(a => a.Priority)
                        .Select(a => new
                        {
                            a.SourceType,
                            a.OrganizationLevel,
                            a.LeadershipRole,
                            a.FunctionalRole,
                            a.Priority
                        })
                        .ToList();

                    if (exApprovers.Count != tplApprovers.Count)
                        return false;

                    for (var j = 0; j < exApprovers.Count; j++)
                    {
                        if (!Equals(exApprovers[j], tplApprovers[j]))
                            return false;
                    }
                }

                return true;
            }

            var template = ApprovalWorkflowSeedBuilder.CreateOvertimeWorkflow();
            var needsUpdate = !SameDefinition(workflow, template);

            if (!needsUpdate)
                return;

            foreach (var step in workflow.Steps.ToList())
            {
                _db.ApprovalWorkflowStepApprovers.RemoveRange(step.Approvers);
                _db.ApprovalWorkflowSteps.Remove(step);
            }

            workflow.Name = template.Name;

            foreach (var templateStep in template.Steps.OrderBy(s => s.StepOrder))
            {
                _db.ApprovalWorkflowSteps.Add(new ApprovalWorkflowStep
                {
                    WorkflowId = workflow.Id,
                    StepOrder = templateStep.StepOrder,
                    Name = templateStep.Name,
                    Approvers = templateStep.Approvers.Select(a => new ApprovalWorkflowStepApprover
                    {
                        SourceType = a.SourceType,
                        OrganizationLevel = a.OrganizationLevel,
                        LeadershipRole = a.LeadershipRole,
                        FunctionalRole = a.FunctionalRole,
                        Priority = a.Priority
                    }).ToList()
                });
            }

            _db.SaveChanges();
        }

        private void EnsureDutyExchangeWorkflowDefinition()
        {
            var exists = _db.ApprovalWorkflows
                .Any(w => w.ProcessType == WorkflowProcessType.DutyExchange && w.IsActive);

            if (exists)
                return;

            _db.ApprovalWorkflows.Add(ApprovalWorkflowSeedBuilder.CreateDutyExchangeWorkflow());
            _db.SaveChanges();
        }

        private void EnsureExchangeRestDayWorkflowDefinition()
        {
            var exists = _db.ApprovalWorkflows
                .Any(w => w.ProcessType == WorkflowProcessType.ExchangeRestDay && w.IsActive);

            if (exists)
                return;

            _db.ApprovalWorkflows.Add(ApprovalWorkflowSeedBuilder.CreateExchangeRestDayWorkflow());
            _db.SaveChanges();
        }

        private void EnsureUndertimeWorkflowDefinition()
        {
            var exists = _db.ApprovalWorkflows
                .Any(w => w.ProcessType == WorkflowProcessType.Undertime && w.IsActive);

            if (exists)
                return;

            _db.ApprovalWorkflows.Add(ApprovalWorkflowSeedBuilder.CreateUndertimeWorkflow());
            _db.SaveChanges();
        }

        private void EnsureDefaultWorkflowDefinition()
        {
            var exists = _db.ApprovalWorkflows
                .Any(w => w.ProcessType == WorkflowProcessType.Default && w.IsActive);

            if (exists)
                return;

            _db.ApprovalWorkflows.Add(ApprovalWorkflowSeedBuilder.CreateDefaultWorkflow());
            _db.SaveChanges();
        }

        private static ApprovalWorkflowStep CreateDefaultStepWithFallback(
            int order,
            string name,
            params ApprovalWorkflowStepApprover[] approvers)
        {
            return new ApprovalWorkflowStep
            {
                StepOrder = order,
                Name = name,
                Approvers = approvers.ToList()
            };
        }

        private static ApprovalWorkflowStep CreateDefaultStep(
            int order,
            string name,
            ApproverSourceType sourceType,
            OrganizationLevel level,
            OrganizationLeadershipRole? leadershipRole = null,
            OrganizationFunctionalRole? functionalRole = null)
        {
            return new ApprovalWorkflowStep
            {
                StepOrder = order,
                Name = name,
                Approvers =
                {
                    new ApprovalWorkflowStepApprover
                    {
                        SourceType = sourceType,
                        OrganizationLevel = level,
                        LeadershipRole = leadershipRole,
                        FunctionalRole = functionalRole,
                        Priority = 1
                    }
                }
            };
        }
    }
}