using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Models.BuildUp;

namespace PayrollSystem.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new ApplicationDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>()))
            {
                var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

                // Ensure database is created
                await context.Database.EnsureCreatedAsync();

                // Seed data in correct order (respecting foreign key constraints)
                await SeedRolesAsync(roleManager);
                await SeedUsersAsync(userManager, roleManager);
                await SeedSystemConfigurationAsync(context);
                await SeedDepartmentsAsync(context);
                await SeedOrganizationStructuresAsync(context);
                await SeedDesignationsAsync(context);
                await SeedDayTypesAsync(context);
                await SeedShiftTypesAsync(context);
                await SeedRateTypesAsync(context);
                await SeedGovernmentContributionTypesAsync(context);
                await SeedGovernmentContributionTablesAsync(context);
                await SeedTaxTablesAsync(context);
                //await SeedShiftsAsync(context);
                await SeedEmployeesAsync(context, userManager);
                await SeedAllowanceTypesAsync(context);
                await SeedEmployeeAllowancesAsync(context);
                await SeedPayrollPeriodsAsync(context);
                await SeedPayrollPeriodEmployeesAsync(context);
                await SeedTimeCardsAsync(context);
                await SeedPayrollRunsAsync(context);
                await SeedAttendanceAndTimeEntriesAsync(context);

                await SeedDeductionTypesAsync(context);

                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
        {
            var roles = new[]
            {
                new { Name = "SuperAdmin", Description = "Super Administrator with full system access and control", Remarks = "Auto-generated role for system initialization" },
                new { Name = "Admin", Description = "Administrator with elevated permissions for system management", Remarks = "Auto-generated role for system initialization" },
                new { Name = "Employee", Description = "Standard employee role with access to the employee portal", Remarks = "Auto-generated role for system initialization" }
            };

            foreach (var roleData in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleData.Name))
                {
                    await roleManager.CreateAsync(new ApplicationRole
                    {
                        Name = roleData.Name,
                        Description = roleData.Description,
                        Remarks = roleData.Remarks
                    });
                }
            }
        }

        private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
        {
            var users = new[]
            {
                new { Email = "admin@payroll.com", Password = "Admin123!", Role = "SuperAdmin", FirstName = "System", LastName = "Administrator", FullName = "System Administrator" },
                new { Email = "john.doe@company.com", Password = "Employee123!", Role = "Employee", FirstName = "John", LastName = "Doe", FullName = "John Doe" },
                new { Email = "jane.smith@company.com", Password = "Employee123!", Role = "Employee", FirstName = "Jane", LastName = "Smith", FullName = "Jane Smith" },
                new { Email = "mike.johnson@company.com", Password = "Employee123!", Role = "Employee", FirstName = "Mike", LastName = "Johnson", FullName = "Mike Johnson" }
            };

            foreach (var userData in users)
            {
                if (await userManager.FindByEmailAsync(userData.Email) == null)
                {
                    // Get the role first to set the RoleId
                    var role = await roleManager.FindByNameAsync(userData.Role);
                    if (role == null)
                    {
                        continue; // Skip if role doesn't exist
                    }

                    var user = new ApplicationUser
                    {
                        UserName = userData.Email,
                        Email = userData.Email,
                        EmailConfirmed = true,
                        FirstName = userData.FirstName,
                        LastName = userData.LastName,
                        FullName = userData.FullName,
                        RoleId = role.Id, // Set the RoleId before creating the user
                        CreatedOn = DateTime.Now
                    };

                    var result = await userManager.CreateAsync(user, userData.Password);
                    if (result.Succeeded)
                    {
                        // User is already assigned to role via RoleId, but we still need to add to UserRoles table for ASP.NET Identity
                        await userManager.AddToRoleAsync(user, userData.Role);
                    }
                }
            }
        }

        private static async Task SeedSystemConfigurationAsync(ApplicationDbContext context)
        {
            if (!context.PayrollConfigurations.Any())
            {
                var config = new PayrollConfiguration
                {
                    Name = "Default Payroll Configuration",
                    WorkingDaysPerMonth = 22,
                    WorkingHoursPerDay = 8,
                    DefaultWorkStartTime = TimeSpan.FromHours(8),
                    DefaultWorkEndTime = TimeSpan.FromHours(17),
                    DefaultGracePeriodMinutes = 15,
                    DefaultBreakDurationMinutes = 60,
                    DefaultHalfDayHours = 4,
                    DefaultAutomaticPairingWindowMinutes = 15,
                    UseWeekendAsDayOff = true,
                    TaxTableYear = DateTime.Now.Year,
                    SSSTableYear = DateTime.Now.Year,
                    PhilHealthTableYear = DateTime.Now.Year,
                    PagIbigTableYear = DateTime.Now.Year,
                    CompanyName = "Sample Company Inc.",
                    CompanyTIN = "123-456-789-000",
                    CompanySSSNumber = "00-1234567-8",
                    CompanyPhilHealthNumber = "12-345678901-2",
                    CompanyPagIbigNumber = "1234-5678-9012",
                    EffectiveDate = DateTime.Now,
                    IsActive = true
                };

                context.PayrollConfigurations.Add(config);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedDepartmentsAsync(ApplicationDbContext context)
        {
            var existingDepartmentNames = await context.Departments
                .Select(d => d.DepartmentName)
                .ToListAsync();

            var departmentsToAdd = new[]
            {
                new Department { DepartmentName = "Information Technology", DepartmentDescription = "IT Department", IsActive = true },
                new Department { DepartmentName = "Human Resources", DepartmentDescription = "HR Department", IsActive = true },
                new Department { DepartmentName = "Finance", DepartmentDescription = "Finance Department", IsActive = true },
                new Department { DepartmentName = "Operations", DepartmentDescription = "Operations Department", IsActive = true },
                new Department { DepartmentName = "Marketing", DepartmentDescription = "Marketing Department", IsActive = true }
            };

            var newDepartments = departmentsToAdd
                .Where(d => !existingDepartmentNames.Contains(d.DepartmentName))
                .ToList();

            if (newDepartments.Any())
            {
                context.Departments.AddRange(newDepartments);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedOrganizationStructuresAsync(ApplicationDbContext context)
        {
            var existingNames = await context.OrganizationStructures
                .Select(o => o.Name)
                .ToListAsync();

            var root = await context.OrganizationStructures
                .FirstOrDefaultAsync(o => o.ParentId == null);

            if (root == null)
            {
                root = new OrganizationStructure
                {
                    Code = "ORG",
                    Name = "Main Organization",
                    Level = OrganizationLevel.Institution,
                    IsActive = true
                };
                context.OrganizationStructures.Add(root);
                await context.SaveChangesAsync();
                existingNames.Add(root.Name);
            }

            var unitsToAdd = new[]
            {
                new { Code = "IT", Name = "Information Technology" },
                new { Code = "HR", Name = "Human Resources" },
                new { Code = "FIN", Name = "Finance" },
                new { Code = "OPS", Name = "Operations" },
                new { Code = "MKT", Name = "Marketing" }
            };

            var newUnits = unitsToAdd
                .Where(u => !existingNames.Contains(u.Name))
                .Select(u => new OrganizationStructure
                {
                    Code = u.Code,
                    Name = u.Name,
                    ParentId = root.Id,
                    Level = OrganizationLevel.Department,
                    IsActive = true
                })
                .ToList();

            if (newUnits.Any())
            {
                context.OrganizationStructures.AddRange(newUnits);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedDesignationsAsync(ApplicationDbContext context)
        {
            var existingDesignationNames = await context.Designations
                .Select(d => d.DesignationName)
                .ToListAsync();

            var designationsToAdd = new[]
            {
                new Designation { DesignationName = "Software Developer", DesignationDescription = "Software development role", IsActive = true },
                new Designation { DesignationName = "Senior Developer", DesignationDescription = "Senior software development role", IsActive = true },
                new Designation { DesignationName = "HR Manager", DesignationDescription = "Human Resources Manager", IsActive = true },
                new Designation { DesignationName = "Accountant", DesignationDescription = "Accounting position", IsActive = true },
                new Designation { DesignationName = "Operations Manager", DesignationDescription = "Operations management role", IsActive = true },
                new Designation { DesignationName = "Marketing Specialist", DesignationDescription = "Marketing role", IsActive = true }
            };

            var newDesignations = designationsToAdd
                .Where(d => !existingDesignationNames.Contains(d.DesignationName))
                .ToList();

            if (newDesignations.Any())
            {
                context.Designations.AddRange(newDesignations);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedDayTypesAsync(ApplicationDbContext context)
        {
            if (!context.DayTypes.Any())
            {
                var effectiveFrom = new DateTime(2020, 1, 1);
                var dayTypes = new[]
                {
                    new DayType
                    {
                        Name = "Regular Day",
                        Code = "REG-DAY",
                        Description = "Standard working day",
                        BasicMultiplier = 1.0M,
                        OvertimeMultiplier = 1.25M,
                        NightDifferentialMultiplier = 1.10M,
                        NightDifferentialOvertimeMultiplier = 1.375M,
                        EffectiveFrom = effectiveFrom,
                        IsActive = true
                    },
                    new DayType
                    {
                        Name = "Saturday",
                        Code = "SAT",
                        Description = "Weekend day",
                        BasicMultiplier = 1.30M,
                        OvertimeMultiplier = 1.69M,
                        NightDifferentialMultiplier = 1.43M,
                        NightDifferentialOvertimeMultiplier = 1.793M,
                        EffectiveFrom = effectiveFrom,
                        IsActive = true
                    },
                    new DayType
                    {
                        Name = "Sunday",
                        Code = "SUN",
                        Description = "Weekend day",
                        BasicMultiplier = 1.30M,
                        OvertimeMultiplier = 1.69M,
                        NightDifferentialMultiplier = 1.43M,
                        NightDifferentialOvertimeMultiplier = 1.793M,
                        EffectiveFrom = effectiveFrom,
                        IsActive = true
                    },
                    new DayType
                    {
                        Name = "Regular Holiday",
                        Code = "REG-HOL",
                        Description = "Legal holiday",
                        BasicMultiplier = 2.0M,
                        OvertimeMultiplier = 2.6M,
                        NightDifferentialMultiplier = 2.2M,
                        NightDifferentialOvertimeMultiplier = 2.86M,
                        EffectiveFrom = effectiveFrom,
                        IsActive = true
                    },
                    new DayType
                    {
                        Name = "Special Holiday",
                        Code = "SPC-HOL",
                        Description = "Special non-working holiday",
                        BasicMultiplier = 1.30M,
                        OvertimeMultiplier = 1.69M,
                        NightDifferentialMultiplier = 1.43M,
                        NightDifferentialOvertimeMultiplier = 1.793M,
                        EffectiveFrom = effectiveFrom,
                        IsActive = true
                    },
                    new DayType
                    {
                        Name = "Rest Day",
                        Code = "REST-DAY",
                        Description = "Employee's designated rest day",
                        BasicMultiplier = 1.30M,
                        OvertimeMultiplier = 1.69M,
                        NightDifferentialMultiplier = 1.43M,
                        NightDifferentialOvertimeMultiplier = 1.793M,
                        EffectiveFrom = effectiveFrom,
                        IsActive = true
                    }
                };

                context.DayTypes.AddRange(dayTypes);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedShiftTypesAsync(ApplicationDbContext context)
        {
            if (await context.ShiftTypes.AnyAsync()) return;

            context.ShiftTypes.AddRange(new[]
            {
                new ShiftType
                {
                    Code = "REG-8-5",
                    Label = "Regular Day (8am–5pm)",
                    Hours = 8m,
                    IsLeave = false,
                    StartTime = TimeSpan.FromHours(8),
                    EndTime = TimeSpan.FromHours(17),
                    HasLunchBreak = true,
                    LunchStart = TimeSpan.FromHours(12),
                    LunchEnd = TimeSpan.FromHours(13),
                    GracePeriodMinutes = 15,
                    ColorHex = "#2563eb",
                    SortOrder = 1,
                    IsActive = true
                },
                new ShiftType
                {
                    Code = "7AM-3PM",
                    Label = "Early Shift (7am–3pm, no lunch)",
                    Hours = 8m,
                    IsLeave = false,
                    StartTime = TimeSpan.FromHours(7),
                    EndTime = TimeSpan.FromHours(15),
                    HasLunchBreak = false,
                    GracePeriodMinutes = 15,
                    ColorHex = "#0891b2",
                    SortOrder = 2,
                    IsActive = true
                },
                new ShiftType
                {
                    Code = "7PM-7AM",
                    Label = "Night Shift (7pm–7am)",
                    Hours = 12m,
                    IsLeave = false,
                    StartTime = TimeSpan.FromHours(19),
                    EndTime = TimeSpan.FromHours(7),
                    SpansNextDay = true,
                    HasLunchBreak = false,
                    GracePeriodMinutes = 15,
                    ColorHex = "#4f46e5",
                    SortOrder = 3,
                    IsActive = true
                },
                new ShiftType { Code = "VL", Label = "Vacation Leave", Hours = 8m, IsLeave = true, ColorHex = "#f59e0b", SortOrder = 10, IsActive = true },
                new ShiftType { Code = "SL", Label = "Sick Leave", Hours = 8m, IsLeave = true, ColorHex = "#ef4444", SortOrder = 11, IsActive = true },
                new ShiftType { Code = "DAY OFF", Label = "Day Off", Hours = 0m, IsLeave = true, ColorHex = "#9ca3af", SortOrder = 20, IsActive = true },
                new ShiftType { Code = "HOL", Label = "Holiday", Hours = 0m, IsLeave = true, ColorHex = "#10b981", SortOrder = 21, IsActive = true },
            });
            await context.SaveChangesAsync();
        }

        private static async Task SeedRateTypesAsync(ApplicationDbContext context)
        {
            if (!context.RateTypes.Any())
            {
                // Philippine Salary Grade (SG) 2024 First Tranche, Step 1 monthly rates (in PHP)
                var sgRates = new[]
                {
                    new { Grade = 1, Salary = 13530M }, new { Grade = 2, Salary = 14372M }, new { Grade = 3, Salary = 15265M },
                    new { Grade = 4, Salary = 16209M }, new { Grade = 5, Salary = 17205M }, new { Grade = 6, Salary = 18255M },
                    new { Grade = 7, Salary = 19365M }, new { Grade = 8, Salary = 20534M }, new { Grade = 9, Salary = 22219M },
                    new { Grade = 10, Salary = 24381M }, new { Grade = 11, Salary = 28512M }, new { Grade = 12, Salary = 30705M },
                    new { Grade = 13, Salary = 32870M }, new { Grade = 14, Salary = 35434M }, new { Grade = 15, Salary = 38413M },
                    new { Grade = 16, Salary = 41616M }, new { Grade = 17, Salary = 45138M }, new { Grade = 18, Salary = 49015M },
                    new { Grade = 19, Salary = 53873M }, new { Grade = 20, Salary = 60157M }, new { Grade = 21, Salary = 67005M },
                    new { Grade = 22, Salary = 74836M }, new { Grade = 23, Salary = 83659M }, new { Grade = 24, Salary = 94132M },
                    new { Grade = 25, Salary = 107208M }, new { Grade = 26, Salary = 121146M }, new { Grade = 27, Salary = 136893M },
                    new { Grade = 28, Salary = 154320M }, new { Grade = 29, Salary = 173962M }, new { Grade = 30, Salary = 196199M },
                    new { Grade = 31, Salary = 285813M }, new { Grade = 32, Salary = 339921M }, new { Grade = 33, Salary = 428994M }
                };

                var rateTypes = sgRates.Select(sg => new RateType
                {
                    Name = $"SG {sg.Grade}",
                    Code = $"SG{sg.Grade}",
                    RateBasis = RateBasis.Monthly,
                    MonthlySalary = sg.Salary,
                    Description = $"Salary Grade {sg.Grade} (2024 First Tranche, Step 1)",
                    IsActive = true
                }).ToArray();

                context.RateTypes.AddRange(rateTypes);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedGovernmentContributionTypesAsync(ApplicationDbContext context)
        {
            if (!context.GovernmentContributionTypes.Any())
            {
                var contributionTypes = new[]
                {
                    new GovernmentContributionType
                    {
                        Code = "SSS",
                        Name = "Social Security System",
                        Description = "Social Security System contributions",
                        IsMandatory = true,
                        HasEmployerShare = true,
                        CalculationMethod = ContributionCalculationMethod.Tiered,
                        DisplayOrder = 1,
                        IsActive = true
                    },
                    new GovernmentContributionType
                    {
                        Code = "PHILHEALTH",
                        Name = "PhilHealth",
                        Description = "Philippine Health Insurance Corporation",
                        IsMandatory = true,
                        HasEmployerShare = true,
                        CalculationMethod = ContributionCalculationMethod.Percentage,
                        DisplayOrder = 2,
                        IsActive = true
                    },
                    new GovernmentContributionType
                    {
                        Code = "PAGIBIG",
                        Name = "Pag-IBIG",
                        Description = "Home Development Mutual Fund",
                        IsMandatory = true,
                        HasEmployerShare = true,
                        CalculationMethod = ContributionCalculationMethod.Percentage,
                        DisplayOrder = 3,
                        IsActive = true
                    }
                };

                context.GovernmentContributionTypes.AddRange(contributionTypes);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedGovernmentContributionTablesAsync(ApplicationDbContext context)
        {
            if (!context.GovernmentContributionTables.Any())
            {
                var contributionTypes = await context.GovernmentContributionTypes.ToListAsync();
                var effectiveStart = new DateTime(DateTime.Now.Year, 1, 1);

                // ── SSS (Fixed / Tiered) ──────────────────────────────────────────────
                var sssType = contributionTypes.First(ct => ct.Code == "SSS");
                var sssTables = new[]
                {
                    new GovernmentContributionTable
                    {
                        GovernmentContributionTypeId = sssType.Id,
                        EffectiveStartDate = effectiveStart,
                        MinSalary = 0,         MaxSalary = 4249.99M,
                        EmployeeShare = 180,   EmployerShare = 360,   TotalContribution = 540,
                        IsActive = true
                    },
                    new GovernmentContributionTable
                    {
                        GovernmentContributionTypeId = sssType.Id,
                        EffectiveStartDate = effectiveStart,
                        MinSalary = 4250,      MaxSalary = 4749.99M,
                        EmployeeShare = 202.50M, EmployerShare = 405, TotalContribution = 607.50M,
                        IsActive = true
                    },
                    new GovernmentContributionTable
                    {
                        GovernmentContributionTypeId = sssType.Id,
                        EffectiveStartDate = effectiveStart,
                        MinSalary = 4750,      MaxSalary = 5249.99M,
                        EmployeeShare = 225,   EmployerShare = 450,   TotalContribution = 675,
                        IsActive = true
                    },
                    new GovernmentContributionTable
                    {
                        GovernmentContributionTypeId = sssType.Id,
                        EffectiveStartDate = effectiveStart,
                        MinSalary = 5250,      MaxSalary = 5749.99M,
                        EmployeeShare = 247.50M, EmployerShare = 495, TotalContribution = 742.50M,
                        IsActive = true
                    },
                    new GovernmentContributionTable
                    {
                        GovernmentContributionTypeId = sssType.Id,
                        EffectiveStartDate = effectiveStart,
                        MinSalary = 5750,      MaxSalary = 6249.99M,
                        EmployeeShare = 270,   EmployerShare = 540,   TotalContribution = 810,
                        IsActive = true
                    },
                    new GovernmentContributionTable
                    {
                        GovernmentContributionTypeId = sssType.Id,
                        EffectiveStartDate = effectiveStart,
                        MinSalary = 6250,      MaxSalary = null,
                        EmployeeShare = 292.50M, EmployerShare = 585, TotalContribution = 877.50M,
                        IsActive = true
                    }
                };

                // ── PhilHealth (Percentage with cap) ─────────────────────────────────
                // Rates stored as decimal fractions: 0.02 = 2%
                var philHealthType = contributionTypes.First(ct => ct.Code == "PHILHEALTH");
                var philHealthTables = new[]
                {
                    new GovernmentContributionTable
                    {
                        GovernmentContributionTypeId = philHealthType.Id,
                        EffectiveStartDate           = effectiveStart,
                        MinSalary = 0,         MaxSalary = 10000,
                        EmployeeSharePercentage = 0.02M,
                        EmployerSharePercentage = 0.02M,
                        MaxEmployeeContribution = 200,   // 2% of 10,000
                        MaxEmployerContribution = 200,
                        IsActive = true
                    },
                    new GovernmentContributionTable
                    {
                        GovernmentContributionTypeId = philHealthType.Id,
                        EffectiveStartDate           = effectiveStart,
                        MinSalary = 10000.01M, MaxSalary = null,
                        EmployeeSharePercentage = 0.02M,
                        EmployerSharePercentage = 0.02M,
                        MaxEmployeeContribution = 1200,  // 2% of 60,000 ceiling
                        MaxEmployerContribution = 1200,
                        IsActive = true
                    }
                };

                // ── Pag-IBIG (Percentage) ─────────────────────────────────────────────
                // Rates stored as decimal fractions: 0.01 = 1%, 0.02 = 2%
                var pagIbigType = contributionTypes.First(ct => ct.Code == "PAGIBIG");
                var pagIbigTables = new[]
                {
                    new GovernmentContributionTable
                    {
                        GovernmentContributionTypeId = pagIbigType.Id,
                        EffectiveStartDate           = effectiveStart,
                        MinSalary = 0,        MaxSalary = 1500,
                        EmployeeSharePercentage = 0.01M,
                        EmployerSharePercentage = 0.02M,
                        MinEmployeeContribution = 100,  // Pag-IBIG minimum
                        MaxEmployeeContribution = 100,
                        IsActive = true
                    },
                    new GovernmentContributionTable
                    {
                        GovernmentContributionTypeId = pagIbigType.Id,
                        EffectiveStartDate           = effectiveStart,
                        MinSalary = 1500.01M, MaxSalary = null,
                        EmployeeSharePercentage = 0.02M,
                        EmployerSharePercentage = 0.02M,
                        MaxEmployeeContribution = 100,  // ₱5,000 salary ceiling × 2% = ₱100 max
                        MaxEmployerContribution = 100,
                        IsActive = true
                    }
                };

                context.GovernmentContributionTables.AddRange(sssTables);
                context.GovernmentContributionTables.AddRange(philHealthTables);
                context.GovernmentContributionTables.AddRange(pagIbigTables);
                await context.SaveChangesAsync();
            }
        }


        private static async Task SeedTaxTablesAsync(ApplicationDbContext context)
        {
            if (!context.TaxTables.Any())
            {
                var currentYear = DateTime.Now.Year;
                var effectiveStart = new DateTime(currentYear, 1, 1);

                // BIR TRAIN Law Annual brackets (2023 onward).
                // TaxRate is stored as a decimal fraction: 0.15 = 15%.
                // IncomeBasis = Annual means the MinIncome/MaxIncome are annual amounts.
                // The PayrollService normalizes period income to annual before bracket lookup.
                var taxTables = new[]
                {
                    new TaxTable
                    {
                        Name              = "Annual Bracket 1 – Tax Exempt",
                        BracketType       = TaxBracketType.Progressive,
                        IncomeBasis       = IncomeBasis.Annual,
                        MinIncome         = 0,
                        MaxIncome         = 250_000,
                        TaxRate           = 0,
                        BaseTaxAmount     = 0,
                        BracketOrder      = 1,
                        EffectiveStartDate = effectiveStart,
                        IsActive          = true,
                        TaxYear           = currentYear.ToString(),
                        Reference         = "BIR TRAIN Law – RA 10963"
                    },
                    new TaxTable
                    {
                        Name              = "Annual Bracket 2",
                        BracketType       = TaxBracketType.Progressive,
                        IncomeBasis       = IncomeBasis.Annual,
                        MinIncome         = 250_000,
                        MaxIncome         = 400_000,
                        TaxRate           = 0.15M,   // 15%
                        BaseTaxAmount     = 0,
                        BracketOrder      = 2,
                        EffectiveStartDate = effectiveStart,
                        IsActive          = true,
                        TaxYear           = currentYear.ToString(),
                        Reference         = "BIR TRAIN Law – RA 10963"
                    },
                    new TaxTable
                    {
                        Name              = "Annual Bracket 3",
                        BracketType       = TaxBracketType.Progressive,
                        IncomeBasis       = IncomeBasis.Annual,
                        MinIncome         = 400_000,
                        MaxIncome         = 800_000,
                        TaxRate           = 0.20M,   // 20%
                        BaseTaxAmount     = 22_500,  // ₱0 + 15% × (400k–250k)
                        BracketOrder      = 3,
                        EffectiveStartDate = effectiveStart,
                        IsActive          = true,
                        TaxYear           = currentYear.ToString(),
                        Reference         = "BIR TRAIN Law – RA 10963"
                    },
                    new TaxTable
                    {
                        Name              = "Annual Bracket 4",
                        BracketType       = TaxBracketType.Progressive,
                        IncomeBasis       = IncomeBasis.Annual,
                        MinIncome         = 800_000,
                        MaxIncome         = 2_000_000,
                        TaxRate           = 0.25M,   // 25%
                        BaseTaxAmount     = 102_500, // ₱22,500 + 20% × (800k–400k)
                        BracketOrder      = 4,
                        EffectiveStartDate = effectiveStart,
                        IsActive          = true,
                        TaxYear           = currentYear.ToString(),
                        Reference         = "BIR TRAIN Law – RA 10963"
                    },
                    new TaxTable
                    {
                        Name              = "Annual Bracket 5",
                        BracketType       = TaxBracketType.Progressive,
                        IncomeBasis       = IncomeBasis.Annual,
                        MinIncome         = 2_000_000,
                        MaxIncome         = 8_000_000,
                        TaxRate           = 0.30M,   // 30%
                        BaseTaxAmount     = 402_500, // ₱102,500 + 25% × (2M–800k)
                        BracketOrder      = 5,
                        EffectiveStartDate = effectiveStart,
                        IsActive          = true,
                        TaxYear           = currentYear.ToString(),
                        Reference         = "BIR TRAIN Law – RA 10963"
                    },
                    new TaxTable
                    {
                        Name              = "Annual Bracket 6",
                        BracketType       = TaxBracketType.Progressive,
                        IncomeBasis       = IncomeBasis.Annual,
                        MinIncome         = 8_000_000,
                        MaxIncome         = null,
                        TaxRate           = 0.35M,   // 35%
                        BaseTaxAmount     = 2_202_500, // ₱402,500 + 30% × (8M–2M)
                        BracketOrder      = 6,
                        EffectiveStartDate = effectiveStart,
                        IsActive          = true,
                        TaxYear           = currentYear.ToString(),
                        Reference         = "BIR TRAIN Law – RA 10963"
                    }
                };

                context.TaxTables.AddRange(taxTables);
                await context.SaveChangesAsync();
            }
        }

        //private static async Task SeedShiftsAsync(ApplicationDbContext context)
        //{
        //    if (!context.Shifts.Any())
        //    {
        //        var shifts = new[]
        //        {
        //            new Shift
        //            {
        //                Name = "Regular Shift",
        //                Description = "Standard 8-hour working shift",
        //                ShiftType = ShiftType.Morning,
        //                StartTime = new TimeSpan(8, 0, 0),
        //                EndTime = new TimeSpan(17, 0, 0),
        //                BreakStartTime = new TimeSpan(12, 0, 0),
        //                BreakEndTime = new TimeSpan(13, 0, 0),
        //                LunchStartTime = new TimeSpan(12, 0, 0),
        //                LunchEndTime = new TimeSpan(13, 0, 0),
        //                GracePeriodMinutes = 15,
        //                IsActive = true
        //            },
        //            new Shift
        //            {
        //                Name = "Night Shift",
        //                Description = "Night shift for operations",
        //                ShiftType = ShiftType.Night,
        //                StartTime = new TimeSpan(22, 0, 0),
        //                EndTime = new TimeSpan(6, 0, 0), // Next day
        //                BreakStartTime = new TimeSpan(2, 0, 0),
        //                BreakEndTime = new TimeSpan(2, 30, 0),
        //                LunchStartTime = new TimeSpan(2, 0, 0),
        //                LunchEndTime = new TimeSpan(2, 30, 0),
        //                GracePeriodMinutes = 15,
        //                IsActive = true
        //            }
        //        };

        //        context.Shifts.AddRange(shifts);
        //        await context.SaveChangesAsync();
        //    }
        //}

        private static async Task SeedEmployeesAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            if (!context.Employees.Any())
            {
                var organizationStructures = await context.OrganizationStructures
                    .Where(o => o.IsActive)
                    .ToListAsync();
                var designations = await context.Designations.ToListAsync();
                var rateTypes = await context.RateTypes.ToListAsync();
                //var shifts = await context.Shifts.ToListAsync();

                var users = await context.Users.ToListAsync();

                // Debug: Check if required data exists
                if (!organizationStructures.Any())
                    throw new InvalidOperationException("No organization structures found. Create organization units before seeding employees.");
                if (!designations.Any())
                    throw new InvalidOperationException($"No designations found. Designations must be seeded before employees. Available designations: {designations.Count}");
                if (!rateTypes.Any())
                    throw new InvalidOperationException("No rate types found. Rate types must be seeded before employees.");
                if (!users.Any())
                    throw new InvalidOperationException("No users found. Users must be seeded before employees.");

                // Helper function to safely get user by email
                ApplicationUser GetUserByEmail(string email)
                {
                    return users.FirstOrDefault(u => u.Email == email)
                        ?? throw new InvalidOperationException($"User with email {email} not found. Available users: {string.Join(", ", users.Select(u => u.Email))}");
                }

                // Helper function to safely get designation by name
                Designation GetDesignationByName(string name)
                {
                    return designations.FirstOrDefault(d => d.DesignationName == name)
                        ?? throw new InvalidOperationException($"Designation '{name}' not found. Available designations: {string.Join(", ", designations.Select(d => d.DesignationName))}");
                }

                // Helper function to safely get organization unit by name
                OrganizationStructure GetOrganizationUnitByName(string name)
                {
                    return organizationStructures.FirstOrDefault(o => o.Name == name)
                        ?? organizationStructures.FirstOrDefault(o =>
                            o.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException(
                            $"Organization unit '{name}' not found. Available units: {string.Join(", ", organizationStructures.Select(o => o.Name))}");
                }

                // Helper function to safely get rate type by name
                RateType GetRateTypeByName(string name)
                {
                    return rateTypes.FirstOrDefault(rt => rt.Name == name)
                        ?? throw new InvalidOperationException($"Rate type '{name}' not found. Available rate types: {string.Join(", ", rateTypes.Select(rt => rt.Name))}");
                }

                var employeeSpecs = new[]
                {
                    new
                    {
                        Email = "john.doe@company.com",
                        EmployeeIdNumber = "EMP001",
                        Surname = "Doe",
                        FirstName = "John",
                        MiddleName = "Smith",
                        DateOfBirth = new DateOnly(1990, 5, 15),
                        Sex = "Male",
                        TelephoneNo = "123-4567",
                        MobileNo = "09123456789",
                        DateHired = new DateOnly(2020, 1, 15),
                        DesignationName = "Software Developer",
                        OrganizationUnitName = "Information Technology",
                        RateTypeName = "SG 11"
                    },
                    new
                    {
                        Email = "jane.smith@company.com",
                        EmployeeIdNumber = "EMP002",
                        Surname = "Smith",
                        FirstName = "Jane",
                        MiddleName = "Marie",
                        DateOfBirth = new DateOnly(1988, 8, 22),
                        Sex = "Female",
                        TelephoneNo = "123-4568",
                        MobileNo = "09123456790",
                        DateHired = new DateOnly(2019, 3, 10),
                        DesignationName = "HR Manager",
                        OrganizationUnitName = "Human Resources",
                        RateTypeName = "SG 22"
                    },
                    new
                    {
                        Email = "mike.johnson@company.com",
                        EmployeeIdNumber = "EMP003",
                        Surname = "Johnson",
                        FirstName = "Mike",
                        MiddleName = "David",
                        DateOfBirth = new DateOnly(1992, 12, 3),
                        Sex = "Male",
                        TelephoneNo = "123-4569",
                        MobileNo = "09123456791",
                        DateHired = new DateOnly(2021, 6, 1),
                        DesignationName = "Accountant",
                        OrganizationUnitName = "Finance",
                        RateTypeName = "SG 2"
                    }
                };

                var employees = employeeSpecs.Select(spec => new Employee
                {
                    ApplicationUserId = GetUserByEmail(spec.Email).Id,
                    EmployeeIdNumber = spec.EmployeeIdNumber,
                    Surname = spec.Surname,
                    FirstName = spec.FirstName,
                    MiddleName = spec.MiddleName,
                    DateOfBirth = spec.DateOfBirth,
                    Sex = spec.Sex,
                    EmailAddress = spec.Email,
                    TelephoneNo = spec.TelephoneNo,
                    MobileNo = spec.MobileNo,
                    EmploymentBasis = EmploymentBasis.Regular,
                    DateHired = spec.DateHired,
                    DefaultRateTypeId = GetRateTypeByName(spec.RateTypeName).Id,
                    IsActive = true
                }).ToArray();

                context.Employees.AddRange(employees);
                await context.SaveChangesAsync();

                //var designations = employeeSpecs.Zip(employees, (spec, employee) => new EmployeeDesignation
                //{
                //    EmployeeId = employee.Id,
                //    DesignationId = GetDesignationByName(spec.DesignationName).Id,
                //    EffectiveFrom = spec.DateHired.ToDateTime(TimeOnly.MinValue)
                //}).ToList();

                //context.EmployeeDesignations.AddRange(designations);

                var assignments = employeeSpecs.Zip(employees, (spec, employee) => new EmployeeOrganizationAssignment
                {
                    EmployeeId = employee.Id,
                    OrganizationStructureId = GetOrganizationUnitByName(spec.OrganizationUnitName).Id,
                    EffectiveFrom = spec.DateHired.ToDateTime(TimeOnly.MinValue),
                    IsPrimary = true
                }).ToList();

                context.EmployeeOrganizationAssignments.AddRange(assignments);
                await context.SaveChangesAsync();

                // Work schedules are no longer used; employees will rely on generic shift/attendance logic.
            }
        }

        private static async Task SeedAllowanceTypesAsync(ApplicationDbContext context)
        {
            if (!context.AllowanceTypes.Any())
            {
                var types = new[]
                {
                    new AllowanceType { Name = "Housing Allowance", Code = "HOUSING", Description = "Housing allowance", IsActive = true },
                    new AllowanceType { Name = "Transportation Allowance", Code = "TRANSP", Description = "Transportation allowance", IsActive = true },
                    new AllowanceType { Name = "Meal Allowance", Code = "MEAL", Description = "Meal allowance", IsActive = true },
                    new AllowanceType { Name = "Communication Allowance", Code = "COMM", Description = "Communication/phone allowance", IsActive = true },
                    new AllowanceType { Name = "Medical Allowance", Code = "MED", Description = "Medical allowance", IsActive = true },
                    new AllowanceType { Name = "Education Allowance", Code = "EDU", Description = "Education allowance", IsActive = true },
                    new AllowanceType { Name = "Clothing Allowance", Code = "CLOTH", Description = "Clothing allowance", IsActive = true },
                    new AllowanceType { Name = "Other Allowance", Code = "OTHER", Description = "Other allowance", IsActive = true }
                };
                context.AllowanceTypes.AddRange(types);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedDeductionTypesAsync(ApplicationDbContext context)
        {
            var requiredTypes = new[]
            {
                new { Code = "INCOME_TAX",    Name = "Income Tax",              IsSystem = true,  DisplayOrder = 1 },
                new { Code = "ABSENCE",       Name = "Absence Deduction",      IsSystem = true,  DisplayOrder = 2 },
                new { Code = "LATE_UNDERTIME",Name = "Late/Undertime Deduction",IsSystem = true, DisplayOrder = 3 },
                new { Code = "LOAN",          Name = "Loan Payment",           IsSystem = true,  DisplayOrder = 4 },
                new { Code = "INSURANCE",     Name = "Insurance",              IsSystem = false, DisplayOrder = 5 },
                new { Code = "UNION_DUES",    Name = "Union Dues",             IsSystem = false, DisplayOrder = 6 },
                new { Code = "OTHER",         Name = "Other Deduction",        IsSystem = false, DisplayOrder = 99 }
            };

            var existing = await context.DeductionTypes
                .Select(dt => dt.Code)
                .ToListAsync();

            var toAdd = requiredTypes
                .Where(t => !existing.Contains(t.Code))
                .Select(t => new DeductionType
                {
                    Code = t.Code,
                    Name = t.Name,
                    IsSystem = t.IsSystem,
                    DisplayOrder = t.DisplayOrder
                })
                .ToList();

            if (toAdd.Any())
            {
                context.DeductionTypes.AddRange(toAdd);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedEmployeeAllowancesAsync(ApplicationDbContext context)
        {
            if (!context.Allowances.Any())
            {
                var employees = await context.Employees.ToListAsync();
                var allowanceTypes = await context.AllowanceTypes.Where(at => !at.IsDeleted).ToListAsync();
                var transportId = allowanceTypes.First(at => at.Code == "TRANSP").Id;
                var mealId = allowanceTypes.First(at => at.Code == "MEAL").Id;
                var commId = allowanceTypes.First(at => at.Code == "COMM").Id;
                var effectiveDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                var transportAllowance = new Allowance
                {
                    AllowanceName = "Transportation Allowance",
                    AllowanceTypeId = transportId,
                    Amount = 1500,
                    Frequency = AllowanceFrequency.Monthly,
                    IsTaxable = true,
                    IsActive = true,
                    EffectiveDate = effectiveDate
                };

                var mealAllowance = new Allowance
                {
                    AllowanceName = "Meal Allowance",
                    AllowanceTypeId = mealId,
                    Amount = 1000,
                    Frequency = AllowanceFrequency.Monthly,
                    IsTaxable = true,
                    IsActive = true,
                    EffectiveDate = effectiveDate
                };

                var phoneAllowance = new Allowance
                {
                    AllowanceName = "Phone Allowance",
                    AllowanceTypeId = commId,
                    Amount = 1500,
                    Frequency = AllowanceFrequency.Monthly,
                    IsTaxable = true,
                    IsActive = true,
                    EffectiveDate = effectiveDate
                };

                context.Allowances.AddRange(transportAllowance, mealAllowance, phoneAllowance);
                await context.SaveChangesAsync();

                var emp001 = employees.First(e => e.EmployeeIdNumber == "EMP001").Id;
                var emp002 = employees.First(e => e.EmployeeIdNumber == "EMP002").Id;
                var emp003 = employees.First(e => e.EmployeeIdNumber == "EMP003").Id;

                var assignments = new[]
                {
                    new EmployeeAllowance { AllowanceId = transportAllowance.Id, EmployeeId = emp001, IsActive = true },
                    new EmployeeAllowance { AllowanceId = mealAllowance.Id, EmployeeId = emp001, IsActive = true },
                    new EmployeeAllowance { AllowanceId = transportAllowance.Id, EmployeeId = emp002, IsActive = true },
                    new EmployeeAllowance { AllowanceId = phoneAllowance.Id, EmployeeId = emp002, IsActive = true },
                    new EmployeeAllowance { AllowanceId = transportAllowance.Id, EmployeeId = emp003, IsActive = true }
                };

                context.EmployeeAllowances.AddRange(assignments);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedPayrollPeriodsAsync(ApplicationDbContext context)
        {
            if (!context.PayrollPeriods.Any())
            {
                var currentDate = DateTime.Now;
                var y = currentDate.Year;
                var m = currentDate.Month;
                var lastDay = DateTime.DaysInMonth(y, m);
                var nextMonth = new DateTime(y, m, 1).AddMonths(1);
                var monthName = new DateTime(y, m, 1).ToString("MMMM yyyy");

                // First half (1–15), second half (16–EOM), and full month for testing
                var payrollPeriods = new[]
                {
                    new PayrollPeriod
                    {
                        Name = $"First Half {monthName}",
                        Description = $"Semi-monthly payroll: 1st to 15th of {monthName}",
                        PeriodStartDate = new DateOnly(y, m, 1),
                        PeriodEndDate = new DateOnly(y, m, 15),
                        PayDate = new DateOnly(y, m, 20),
                        IsActive = true,
                        IsLocked = false
                    },
                    new PayrollPeriod
                    {
                        Name = $"Second Half {monthName}",
                        Description = $"Semi-monthly payroll: 16th to end of {monthName}",
                        PeriodStartDate = new DateOnly(y, m, 16),
                        PeriodEndDate = new DateOnly(y, m, lastDay),
                        PayDate = new DateOnly(nextMonth.Year, nextMonth.Month, 5),
                        IsActive = true,
                        IsLocked = false
                    },
                    new PayrollPeriod
                    {
                        Name = $"Full Month {monthName}",
                        Description = $"Monthly payroll for {monthName}",
                        PeriodStartDate = new DateOnly(y, m, 1),
                        PeriodEndDate = new DateOnly(y, m, lastDay),
                        PayDate = new DateOnly(nextMonth.Year, nextMonth.Month, 5),
                        IsActive = true,
                        IsLocked = false
                    }
                };

                context.PayrollPeriods.AddRange(payrollPeriods);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedPayrollPeriodEmployeesAsync(ApplicationDbContext context)
        // Get all active employees
        {
            if (!context.PayrollPeriodEmployees.Any())
            {
                var employees = await context.Employees
                    .Where(e => e.IsActive && !e.IsDeleted)
                    .ToListAsync();

                var periods = await context.PayrollPeriods
                    .Where(pp => pp.IsActive && !pp.IsDeleted && !pp.IsLocked)
                    .ToListAsync();

                if (!periods.Any() || !employees.Any())
                    return;

                var payrollPeriodEmployees = new List<PayrollPeriodEmployee>();
                foreach (var period in periods)
                {
                    foreach (var employee in employees)
                    {
                        payrollPeriodEmployees.Add(new PayrollPeriodEmployee
                        {
                            PayrollPeriodId = period.Id,
                            EmployeeId = employee.Id,
                            CreatedOn = DateTime.Now
                        });
                    }
                }

                context.PayrollPeriodEmployees.AddRange(payrollPeriodEmployees);
                await context.SaveChangesAsync();
            }
        }

        /// Seeds sample time cards for each employee in each seeded payroll period.
        /// Uses "Regular Day" with 8 hrs/day: 11 days for semi-monthly (88 hrs), 22 days for full month (176 hrs).
        private static async Task SeedTimeCardsAsync(ApplicationDbContext context)
        {
            if (context.PayrollPeriodTimeCards.Any())
                return;

            var periods = await context.PayrollPeriods
                .Where(pp => pp.IsActive && !pp.IsDeleted)
                .ToListAsync();
            var periodEmployees = await context.PayrollPeriodEmployees
                .Where(ppe => periods.Select(p => p.Id).Contains(ppe.PayrollPeriodId))
                .ToListAsync();
            var dayTypes = await context.DayTypes.ToListAsync();
            var regularDayType = dayTypes.FirstOrDefault(dt => dt.Name == "Regular Day");
            if (regularDayType == null)
                return;

            foreach (var period in periods)
            {
                var employeeIdsInPeriod = periodEmployees
                    .Where(ppe => ppe.PayrollPeriodId == period.Id)
                    .Select(ppe => ppe.EmployeeId)
                    .Distinct()
                    .ToList();
                int daysInPeriod = period.PeriodEndDate.DayNumber - period.PeriodStartDate.DayNumber + 1;
                // Approximate working days: semi-monthly (15 or 16 days) ~ 11; full month ~ 22
                decimal workingDays = daysInPeriod >= 28 ? 22M : 11M;
                decimal regularHours = workingDays * 8M;

                foreach (var empId in employeeIdsInPeriod)
                {
                    var timeCard = new PayrollPeriodTimeCardEntry
                    {
                        PayrollPeriodId = period.Id,
                        EmployeeId = empId,
                        LateMinutes = 0,
                        UndertimeMinutes = 0,
                        Notes = "Sample seed time card for testing",
                        CreatedOn = DateTime.Now,
                        UpdatedOn = DateTime.Now
                    };
                    timeCard.Details.Add(new PayrollPeriodTimeCardDetail
                    {
                        DayTypeId = regularDayType.Id,
                        RegularHours = regularHours,
                        OvertimeHours = 0,
                        NightDifferentialHours = 0,
                        NightDifferentialOvertimeHours = 0
                    });
                    context.PayrollPeriodTimeCards.Add(timeCard);
                }
            }

            await context.SaveChangesAsync();
        }

        /// Seeds one Draft PayrollRun for the "First Half" period so you can test the full flow:
        /// Payroll → open run → Process Payroll → view payslips (tax + gov't contributions applied).
        private static async Task SeedPayrollRunsAsync(ApplicationDbContext context)
        {
            if (context.PayrollRuns.Any())
                return;

            var firstHalfPeriod = await context.PayrollPeriods
                .Where(pp => pp.IsActive && !pp.IsDeleted && pp.Name.Contains("First Half"))
                .OrderBy(pp => pp.PeriodStartDate)
                .FirstOrDefaultAsync();

            if (firstHalfPeriod == null)
                return;

            var adminUser = await context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == "admin@payroll.com");

            var payrollRun = new PayrollRun
            {
                PayrollPeriodId = firstHalfPeriod.Id,
                Name = $"Payroll Run for {firstHalfPeriod.Name}",
                Description = $"Sample run for testing — process this run to see tax and government contributions.",
                Status = PayrollStatus.Draft,
                CreatedById = adminUser?.Id,
                CreatedOn = DateTime.Now,
                UpdatedById = adminUser?.Id,
                UpdatedOn = DateTime.Now
            };

            context.PayrollRuns.Add(payrollRun);
            await context.SaveChangesAsync();
        }

        private static async Task SeedAttendanceAndTimeEntriesAsync(ApplicationDbContext context)
        {
            // Attendance and time entries are now generated through the main DTR pipeline, not seed data.
            await Task.CompletedTask;
        }
    }
}