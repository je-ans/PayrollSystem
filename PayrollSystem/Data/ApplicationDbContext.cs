using System.Reflection.Emit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Models;
using PayrollSystem.Models.BuildUp;
using PayrollSystem.Models.Workflow;
using PayrollSystem.Utility;

namespace PayrollSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }

        public DbSet<AuditTrail> AuditTrails { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Designation> Designations { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<PersonalInformation> PersonalInformation { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<RateType> RateTypes { get; set; }
        public DbSet<DayType> DayTypes { get; set; }
        public DbSet<AssignRolePermission> AssignRolePermissions { get; set; }
        public DbSet<SystemTask> SystemTasks { get; set; }
        public DbSet<SystemInfo> SystemInfos { get; set; }

        // Time & Attendance
        public DbSet<BiometricLog> BiometricLogs { get; set; }
        public DbSet<TimeEntry> TimeEntries { get; set; }
        public DbSet<TimeLogJustification> TimeLogJustifications { get; set; }
        public DbSet<OvertimeRequest> OvertimeRequests { get; set; }
        public DbSet<UndertimeRequest> UndertimeRequests { get; set; }
        public DbSet<DutyExchangeRequest> DutyExchangeRequests { get; set; }
        public DbSet<ExchangeRestDayRequest> ExchangeRestDayRequests { get; set; }
        public DbSet<Holiday> Holidays { get; set; }
        public DbSet<WorkingCalendar> WorkingCalendars { get; set; }

        // Payroll System
        public DbSet<PayrollPeriod> PayrollPeriods { get; set; }
        public DbSet<PayrollRun> PayrollRuns { get; set; }
        public DbSet<PayrollEntry> PayrollEntries { get; set; }
        public DbSet<PayrollPeriodTimeCardEntry> PayrollPeriodTimeCards { get; set; }
        public DbSet<PayrollPeriodTimeCardDetail> PayrollPeriodTimeCardDetails { get; set; }
        public DbSet<Payslip> Payslips { get; set; }
        public DbSet<PayrollDeduction> PayrollDeductions { get; set; }
        public DbSet<Deduction> Deductions { get; set; }
        public DbSet<GovernmentContributionType> GovernmentContributionTypes { get; set; }
        public DbSet<GovernmentContributionTable> GovernmentContributionTables { get; set; }
        public DbSet<TaxTable> TaxTables { get; set; }
        public DbSet<PayrollConfiguration> PayrollConfigurations { get; set; }

        // Employee Compensation
        public DbSet<AllowanceType> AllowanceTypes { get; set; }
        public DbSet<Allowance> Allowances { get; set; }
        public DbSet<EmployeeAllowance> EmployeeAllowances { get; set; }
        public DbSet<SalaryHistory> SalaryHistories { get; set; }
        public DbSet<EmployeeLoan> EmployeeLoans { get; set; }
        public DbSet<DeductionType> DeductionTypes { get; set; }
        public DbSet<EmployeeDeduction> EmployeeDeductions { get; set; }

        public DbSet<PayrollPeriodEmployee> PayrollPeriodEmployees { get; set; }
        public DbSet<PayrollPeriodBatch> PayrollPeriodBatches { get; set; }

        public DbSet<ShiftType> ShiftTypes { get; set; }
        public DbSet<WorkSchedule> WorkSchedules { get; set; }
        public DbSet<ScheduleEntry> ScheduleEntries { get; set; }
        public DbSet<ScheduleChangeRequest> ScheduleChangeRequests { get; set; }

        // Build-Up: Organizational structure
        public DbSet<OrganizationStructure> OrganizationStructures { get; set; }
        public DbSet<EmployeeOrganizationAssignment> EmployeeOrganizationAssignments { get; set; }
        public DbSet<EmployeeDesignation> EmployeeDesignations { get; set; }
        public DbSet<EmployeeRoleAssignment> EmployeeRoleAssignments { get; set; }

        // Workflow
        public DbSet<ApprovalWorkflow> ApprovalWorkflows { get; set; }
        public DbSet<ApprovalWorkflowStep> ApprovalWorkflowSteps { get; set; }
        public DbSet<ApprovalWorkflowStepApprover> ApprovalWorkflowStepApprovers { get; set; }
        public DbSet<ApprovalRequest> ApprovalRequests { get; set; }
        public DbSet<ApprovalRequestStep> ApprovalRequestSteps { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {

            base.OnModelCreating(builder);

            builder.HasDefaultSchema("dbo");

            // ========== IDENTITY TABLES ==========
            builder.Entity<ApplicationUser>(entity => { entity.ToTable(name: "User"); });
            builder.Entity<ApplicationRole>(entity =>
            {
                entity.ToTable(name: "Role");
                entity.Property(r => r.Id).ValueGeneratedOnAdd();
                entity.Property(r => r.Description).HasMaxLength(500).IsRequired();
                entity.Property(r => r.IsActive).HasDefaultValue(true);
                entity.Property(r => r.IsDeleted).HasDefaultValue(false);
                entity.Property(r => r.Remarks).HasMaxLength(500).IsRequired();
            });
            builder.Entity<IdentityUserRole<string>>(entity => { entity.ToTable(name: "UserRoles"); });
            builder.Entity<IdentityUserClaim<string>>(entity => { entity.ToTable(name: "UserClaims"); });
            builder.Entity<IdentityUserLogin<string>>(entity => { entity.ToTable(name: "UserLogins"); });
            builder.Entity<IdentityRoleClaim<string>>(entity => { entity.ToTable(name: "RoleClaims"); });
            builder.Entity<IdentityUserToken<string>>(entity => { entity.ToTable(name: "UserTokens"); });

            // ========== RELATIONSHIPS ==========

            // ApplicationUser relationships
            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Role)
                .WithMany()
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Designation)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DesignationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Employee)
                .WithOne(e => e.ApplicationUser)
                .HasForeignKey<Employee>(e => e.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // AssignRolePermission relationships
            builder.Entity<AssignRolePermission>()
                .HasOne(arp => arp.Role)
                .WithMany()
                .HasForeignKey(arp => arp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // Designation relationships
            builder.Entity<Designation>()
                .HasOne(d => d.CreatedBy)
                .WithMany()
                .HasForeignKey(d => d.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Designation>()
                .HasOne(d => d.UpdatedBy)
                .WithMany()
                .HasForeignKey(d => d.UpdatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Department relationships
            builder.Entity<Department>()
                .HasOne(d => d.CreatedBy)
                .WithMany()
                .HasForeignKey(d => d.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Department>()
                .HasOne(d => d.UpdatedBy)
                .WithMany()
                .HasForeignKey(d => d.UpdatedById)
                .OnDelete(DeleteBehavior.Restrict);


            // === ROLE PERMISSION INDEXES ===
            builder.Entity<AssignRolePermission>()
                .HasIndex(a => a.RoleId);
            builder.Entity<AssignRolePermission>()
                .HasIndex(a => a.TaskId);

            // === AUDIT TRAIL INDEXES ===
            builder.Entity<AuditTrail>()
                .HasIndex(a => new { a.UserId, a.TimeStamp });
            builder.Entity<AuditTrail>()
                .HasIndex(a => new { a.Module, a.TimeStamp });
            builder.Entity<AuditTrail>()
                .HasIndex(a => a.AffectedTable);

            // === USER INDEXES ===
            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.RoleId);
            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.DepartmentId);
            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.DesignationId);

            // PersonalInformation relationships
            builder.Entity<PersonalInformation>()
                .HasOne(pi => pi.Employee)
                .WithOne(e => e.PersonalInformation)
                .HasForeignKey<PersonalInformation>(pi => pi.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PersonalInformation>()
                .HasOne(pi => pi.ResidentialAddress)
                .WithMany()
                .HasForeignKey(pi => pi.ResidentialAddressId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PersonalInformation>()
                .HasOne(pi => pi.PermanentAddress)
                .WithMany()
                .HasForeignKey(pi => pi.PermanentAddressId)
                .OnDelete(DeleteBehavior.Restrict);


            // ========== TIME & ATTENDANCE RELATIONSHIPS ==========


            // BiometricLog relationships
            builder.Entity<BiometricLog>()
                .HasOne(b => b.Employee)
                .WithMany()
                .HasForeignKey(b => b.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BiometricLog>()
                .HasIndex(b => new { b.EmployeeId, b.Timestamp, b.DeviceId });

            // TimeEntry relationships (BiometricLog one-to-one: TimeEntry.BiometricLogId is FK)
            builder.Entity<TimeEntry>()
                .HasOne(te => te.Employee)
                .WithMany()
                .HasForeignKey(te => te.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TimeEntry>()
                .HasOne(te => te.BiometricLog)
                .WithOne(b => b.TimeEntry)
                .HasForeignKey<TimeEntry>(te => te.BiometricLogId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<TimeEntry>()
                .HasOne(te => te.PairedEntry)
                .WithOne()
                .HasForeignKey<TimeEntry>(te => te.PairedEntryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TimeLogJustification>(e =>
            {
                e.HasIndex(j => new { j.EmployeeId, j.EntryDate, j.Status });
                e.HasIndex(j => j.ApprovalRequestId);

                e.HasOne(j => j.Employee)
                    .WithMany()
                    .HasForeignKey(j => j.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(j => j.ReviewedBy)
                    .WithMany()
                    .HasForeignKey(j => j.ReviewedById)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(j => j.ApprovalRequest)
                    .WithMany()
                    .HasForeignKey(j => j.ApprovalRequestId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<OvertimeRequest>(e =>
            {
                e.ToTable("OvertimeRequests", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Reason).HasMaxLength(2000);
                e.Property(x => x.ReviewNotes).HasMaxLength(1000);
                e.Property(x => x.SubmittedBy).HasMaxLength(256);
                e.Property(x => x.ReviewedById).HasMaxLength(450);
                e.Property(x => x.CreatedById).HasMaxLength(450);
                e.Property(x => x.UpdatedById).HasMaxLength(450);
                e.Property(x => x.ProcessedToPayrollBy).HasMaxLength(256);
                e.HasIndex(x => new { x.EmployeeId, x.StartDate, x.EndDate, x.Status });
                e.HasIndex(x => x.ApprovalRequestId);
                e.HasIndex(x => new { x.Status, x.IsProcessedToPayroll });

                e.HasOne(x => x.Employee)
                    .WithMany()
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Restrict (not SetNull) on User FKs — SQL Server rejects multiple cascade/set-null paths to User.
                e.HasOne(x => x.ReviewedBy)
                    .WithMany()
                    .HasForeignKey(x => x.ReviewedById)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.ApprovalRequest)
                    .WithMany()
                    .HasForeignKey(x => x.ApprovalRequestId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.PayrollPeriod)
                    .WithMany()
                    .HasForeignKey(x => x.PayrollPeriodId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.DayType)
                    .WithMany()
                    .HasForeignKey(x => x.DayTypeId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.CreatedBy)
                    .WithMany()
                    .HasForeignKey(x => x.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.UpdatedBy)
                    .WithMany()
                    .HasForeignKey(x => x.UpdatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<UndertimeRequest>(e =>
            {
                e.ToTable("UndertimeRequests", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Purpose).HasMaxLength(2000);
                e.Property(x => x.ReviewNotes).HasMaxLength(1000);
                e.Property(x => x.SubmittedBy).HasMaxLength(256);
                e.Property(x => x.ReviewedById).HasMaxLength(450);
                e.Property(x => x.DurationHours).HasColumnType("decimal(5,2)");
                e.Property(x => x.TotalHours).HasColumnType("decimal(5,2)");
                e.Property(x => x.CreatedById).HasMaxLength(450);
                e.Property(x => x.UpdatedById).HasMaxLength(450);
                e.HasIndex(x => new { x.EmployeeId, x.DateOfUndertime });
                e.HasIndex(x => x.ApprovalRequestId);

                e.HasOne(x => x.Employee)
                    .WithMany()
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.ReviewedBy)
                    .WithMany()
                    .HasForeignKey(x => x.ReviewedById)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.ApprovalRequest)
                    .WithMany()
                    .HasForeignKey(x => x.ApprovalRequestId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.CreatedBy)
                    .WithMany()
                    .HasForeignKey(x => x.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.UpdatedBy)
                    .WithMany()
                    .HasForeignKey(x => x.UpdatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<DutyExchangeRequest>(e =>
            {
                e.ToTable("DutyExchangeRequests", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Reason).HasMaxLength(2000);
                e.Property(x => x.ReviewNotes).HasMaxLength(1000);
                e.Property(x => x.SubmittedBy).HasMaxLength(256);
                e.Property(x => x.ReviewedById).HasMaxLength(450);
                e.Property(x => x.CreatedById).HasMaxLength(450);
                e.Property(x => x.UpdatedById).HasMaxLength(450);
                e.HasIndex(x => new { x.FirstEmployeeId, x.FirstOriginalDate });
                e.HasIndex(x => new { x.SecondEmployeeId, x.SecondOriginalDate });
                e.HasIndex(x => x.ApprovalRequestId);

                e.HasOne(x => x.OrganizationStructure)
                    .WithMany()
                    .HasForeignKey(x => x.OrganizationStructureId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.FirstEmployee)
                    .WithMany()
                    .HasForeignKey(x => x.FirstEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.SecondEmployee)
                    .WithMany()
                    .HasForeignKey(x => x.SecondEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.FirstOriginalShiftType)
                    .WithMany()
                    .HasForeignKey(x => x.FirstOriginalShiftTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.FirstExchangedShiftType)
                    .WithMany()
                    .HasForeignKey(x => x.FirstExchangedShiftTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.SecondOriginalShiftType)
                    .WithMany()
                    .HasForeignKey(x => x.SecondOriginalShiftTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.SecondExchangedShiftType)
                    .WithMany()
                    .HasForeignKey(x => x.SecondExchangedShiftTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.ReviewedBy)
                    .WithMany()
                    .HasForeignKey(x => x.ReviewedById)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.ApprovalRequest)
                    .WithMany()
                    .HasForeignKey(x => x.ApprovalRequestId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.CreatedBy)
                    .WithMany()
                    .HasForeignKey(x => x.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.UpdatedBy)
                    .WithMany()
                    .HasForeignKey(x => x.UpdatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ExchangeRestDayRequest>(e =>
            {
                e.ToTable("ExchangeRestDayRequests", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Reason).HasMaxLength(2000);
                e.Property(x => x.ReviewNotes).HasMaxLength(1000);
                e.Property(x => x.SubmittedBy).HasMaxLength(256);
                e.Property(x => x.ReviewedById).HasMaxLength(450);
                e.Property(x => x.CreatedById).HasMaxLength(450);
                e.Property(x => x.UpdatedById).HasMaxLength(450);
                e.HasIndex(x => new { x.FirstEmployeeId, x.FirstRestDate });
                e.HasIndex(x => new { x.SecondEmployeeId, x.SecondRestDate });
                e.HasIndex(x => x.ApprovalRequestId);

                e.HasOne(x => x.OrganizationStructure)
                    .WithMany()
                    .HasForeignKey(x => x.OrganizationStructureId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.FirstEmployee)
                    .WithMany()
                    .HasForeignKey(x => x.FirstEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.SecondEmployee)
                    .WithMany()
                    .HasForeignKey(x => x.SecondEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.FirstRestShiftType)
                    .WithMany()
                    .HasForeignKey(x => x.FirstRestShiftTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.SecondRestShiftType)
                    .WithMany()
                    .HasForeignKey(x => x.SecondRestShiftTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.FirstWorkShiftType)
                    .WithMany()
                    .HasForeignKey(x => x.FirstWorkShiftTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.SecondWorkShiftType)
                    .WithMany()
                    .HasForeignKey(x => x.SecondWorkShiftTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.ReviewedBy)
                    .WithMany()
                    .HasForeignKey(x => x.ReviewedById)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.ApprovalRequest)
                    .WithMany()
                    .HasForeignKey(x => x.ApprovalRequestId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.CreatedBy)
                    .WithMany()
                    .HasForeignKey(x => x.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.UpdatedBy)
                    .WithMany()
                    .HasForeignKey(x => x.UpdatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Holiday>(e =>
            {
                e.ToTable("Holidays", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasMaxLength(150).IsRequired();
                e.Property(x => x.Description).HasMaxLength(500);
                e.HasIndex(x => x.Date);
                e.HasIndex(x => new { x.Date, x.IsActive });

                e.HasOne(x => x.DayType)
                    .WithMany()
                    .HasForeignKey(x => x.DayTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.CreatedBy)
                    .WithMany()
                    .HasForeignKey(x => x.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.UpdatedBy)
                    .WithMany()
                    .HasForeignKey(x => x.UpdatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<WorkingCalendar>(e =>
            {
                e.ToTable("WorkingCalendars", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Remarks).HasMaxLength(500);
                e.HasIndex(x => x.Date).IsUnique();
                e.HasIndex(x => new { x.Date, x.DayTypeId });

                e.HasOne(x => x.DayType)
                    .WithMany()
                    .HasForeignKey(x => x.DayTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Holiday)
                    .WithMany()
                    .HasForeignKey(x => x.HolidayId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.CreatedBy)
                    .WithMany()
                    .HasForeignKey(x => x.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.UpdatedBy)
                    .WithMany()
                    .HasForeignKey(x => x.UpdatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ScheduleChangeRequest>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Reason).HasMaxLength(2000);
                e.Property(x => x.ReviewNotes).HasMaxLength(1000);
                e.Property(x => x.SubmittedBy).HasMaxLength(256);
                e.HasIndex(x => new { x.EmployeeId, x.WorkScheduleId, x.Day, x.Status });
                e.HasIndex(x => x.ApprovalRequestId);

                e.HasOne(x => x.Employee)
                    .WithMany()
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.WorkSchedule)
                    .WithMany()
                    .HasForeignKey(x => x.WorkScheduleId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.CurrentShiftType)
                    .WithMany()
                    .HasForeignKey(x => x.CurrentShiftTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.RequestedShiftType)
                    .WithMany()
                    .HasForeignKey(x => x.RequestedShiftTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.ReviewedBy)
                    .WithMany()
                    .HasForeignKey(x => x.ReviewedById)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.ApprovalRequest)
                    .WithMany()
                    .HasForeignKey(x => x.ApprovalRequestId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ========== PAYROLL RELATIONSHIPS ==========

            // PayrollPeriod relationships
            builder.Entity<PayrollPeriod>()
                .HasIndex(pp => new { pp.PeriodStartDate, pp.PeriodEndDate });

            // PayrollRun relationships
            builder.Entity<PayrollRun>()
                .HasOne(pr => pr.PayrollPeriod)
                .WithMany(pp => pp.PayrollRuns)
                .HasForeignKey(pr => pr.PayrollPeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PayrollRun>()
                .HasOne(pr => pr.ProcessedByUser)
                .WithMany()
                .HasForeignKey(pr => pr.ProcessedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<PayrollRun>()
                .HasIndex(pr => new { pr.PayrollPeriodId, pr.Status });

            // PayrollEntry relationships
            builder.Entity<PayrollEntry>()
                .HasOne(pe => pe.PayrollRun)
                .WithMany(pr => pr.PayrollEntries)
                .HasForeignKey(pe => pe.PayrollRunId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PayrollEntry>()
                .HasOne(pe => pe.Employee)
                .WithMany()
                .HasForeignKey(pe => pe.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PayrollEntry>()
                .HasIndex(pe => new { pe.PayrollRunId, pe.EmployeeId })
                .IsUnique();

            // Payslip relationships
            builder.Entity<Payslip>()
                .HasOne(p => p.PayrollEntry)
                .WithMany()
                .HasForeignKey(p => p.PayrollEntryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payslip>()
                .HasIndex(p => p.PayslipNumber)
                .IsUnique();

            builder.Entity<Payslip>()
                .HasIndex(p => new { p.PayrollEntryId });

            // Payroll entry deduction lines
            builder.Entity<PayrollDeduction>()
                .HasOne(d => d.PayrollEntry)
                .WithMany(pe => pe.Deductions)
                .HasForeignKey(d => d.PayrollEntryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Recurring employee deduction (master + assignments)
            builder.Entity<Deduction>()
                .HasOne(d => d.DeductionType)
                .WithMany()
                .HasForeignKey(d => d.DeductionTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // TaxTable relationships
            builder.Entity<TaxTable>()
                .HasIndex(tt => new { tt.IncomeBasis, tt.EffectiveStartDate, tt.BracketOrder });

            builder.Entity<GovernmentContributionTable>()
                .HasIndex(g => new { g.GovernmentContributionTypeId, g.EffectiveStartDate, g.MinSalary });

            // Allowance relationships
            builder.Entity<Allowance>()
                .HasOne(a => a.AllowanceType)
                .WithMany(at => at.Allowances)
                .HasForeignKey(a => a.AllowanceTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<EmployeeAllowance>()
                .HasOne(ea => ea.Allowance)
                .WithMany(a => a.EmployeeAssignments)
                .HasForeignKey(ea => ea.AllowanceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<EmployeeAllowance>()
                .HasOne(ea => ea.Employee)
                .WithMany(e => e.Allowances)
                .HasForeignKey(ea => ea.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<EmployeeAllowance>()
                .HasIndex(ea => new { ea.AllowanceId, ea.EmployeeId })
                .IsUnique();

            // Salary History relationships
            builder.Entity<SalaryHistory>()
                .HasOne(sh => sh.Employee)
                .WithMany()
                .HasForeignKey(sh => sh.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SalaryHistory>()
                .HasOne(sh => sh.ApprovedBy)
                .WithMany()
                .HasForeignKey(sh => sh.ApprovedById)
                .OnDelete(DeleteBehavior.SetNull);

            // Employee loan relationships
            builder.Entity<EmployeeLoan>()
                .HasOne(el => el.Employee)
                .WithMany(e => e.EmployeeLoans)
                .HasForeignKey(el => el.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<EmployeeLoan>()
                .HasOne(el => el.DeductionType)
                .WithMany()
                .HasForeignKey(el => el.DeductionTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<EmployeeDeduction>()
                .HasOne(ed => ed.Deduction)
                .WithMany(d => d.EmployeeAssignments)
                .HasForeignKey(ed => ed.DeductionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<EmployeeDeduction>()
                .HasOne(ed => ed.Employee)
                .WithMany(e => e.EmployeeDeductions)
                .HasForeignKey(ed => ed.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<EmployeeDeduction>()
                .HasIndex(ed => new { ed.DeductionId, ed.EmployeeId })
                .IsUnique();

            // Configure many-to-many relationship between PayrollPeriod and Employee
            builder.Entity<PayrollPeriodEmployee>()
                .HasKey(ppe => new { ppe.PayrollPeriodId, ppe.EmployeeId });

            builder.Entity<PayrollPeriodEmployee>()
                .HasOne(ppe => ppe.PayrollPeriod)
                .WithMany(pp => pp.PayrollPeriodEmployees)
                .HasForeignKey(ppe => ppe.PayrollPeriodId);

            builder.Entity<PayrollPeriodEmployee>()
                .HasOne(ppe => ppe.Employee)
                .WithMany(e => e.PayrollPeriodEmployees)
                .HasForeignKey(ppe => ppe.EmployeeId);

            // Payroll period batches (multi-batch Assign & Time Card)
            builder.Entity<PayrollPeriodBatch>()
                .HasOne(b => b.PayrollPeriod)
                .WithMany(pp => pp.PayrollPeriodBatches)
                .HasForeignKey(b => b.PayrollPeriodId)
                .OnDelete(DeleteBehavior.Cascade);

            // Manual time card entries per period/employee
            builder.Entity<PayrollPeriodTimeCardEntry>()
                .HasKey(tc => tc.Id);

            // Enforce one time card per (PayrollPeriod, Employee)
            builder.Entity<PayrollPeriodTimeCardEntry>()
                .HasIndex(tc => new { tc.PayrollPeriodId, tc.EmployeeId })
                .IsUnique();

            builder.Entity<PayrollPeriodTimeCardEntry>()
                .HasOne(tc => tc.PayrollPeriod)
                .WithMany()
                .HasForeignKey(tc => tc.PayrollPeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PayrollPeriodTimeCardEntry>()
                .HasOne(tc => tc.Employee)
                .WithMany()
                .HasForeignKey(tc => tc.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PayrollPeriodTimeCardEntry>()
                .HasOne(tc => tc.PayrollPeriodBatch)
                .WithMany()
                .HasForeignKey(tc => tc.PayrollPeriodBatchId)
                .OnDelete(DeleteBehavior.SetNull);

            // Per-day-type detail lines (child of time card)
            builder.Entity<PayrollPeriodTimeCardDetail>()
                .HasKey(d => d.Id);

            builder.Entity<PayrollPeriodTimeCardDetail>()
                .HasOne(d => d.TimeCard)
                .WithMany(tc => tc.Details)
                .HasForeignKey(d => d.TimeCardId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PayrollPeriodTimeCardDetail>()
                .HasOne(d => d.DayType)
                .WithMany()
                .HasForeignKey(d => d.DayTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Enforce one detail row per (TimeCard, DayType)
            builder.Entity<PayrollPeriodTimeCardDetail>()
                .HasIndex(d => new { d.TimeCardId, d.DayTypeId })
                .IsUnique();

            // ── ShiftType ─────────────────────────────────────────────────────
            builder.Entity<ShiftType>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Code).HasMaxLength(50).IsRequired();
                e.Property(x => x.Label).HasMaxLength(100).IsRequired();
                e.Property(x => x.Hours).HasColumnType("decimal(5,2)");
                e.Property(x => x.ColorHex).HasMaxLength(10);
                e.HasIndex(x => x.Code).IsUnique();
            });

            // ── WorkSchedule ──────────────────────────────────────────────────
            builder.Entity<WorkSchedule>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.CreatedBy).HasMaxLength(100);
                e.Property(x => x.LastModifiedBy).HasMaxLength(100);
                e.Property(x => x.LockedBy).HasMaxLength(100);
                e.Property(x => x.SubmittedBy).HasMaxLength(100);
                e.Property(x => x.ApprovedBy).HasMaxLength(100);
                e.Property(x => x.RejectedBy).HasMaxLength(100);
                e.Property(x => x.RejectionReason).HasMaxLength(1000);
                e.HasIndex(x => new { x.Month, x.Year, x.OrganizationStructureId }).IsUnique();

                e.HasOne(x => x.OrganizationStructure)
                    .WithMany()
                    .HasForeignKey(x => x.OrganizationStructureId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.ApprovalRequest)
                    .WithMany()
                    .HasForeignKey(x => x.ApprovalRequestId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.SubmittedByEmployee)
                    .WithMany()
                    .HasForeignKey(x => x.SubmittedByEmployeeId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ── ScheduleEntry ─────────────────────────────────────────────────
            builder.Entity<ScheduleEntry>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.CreatedBy).HasMaxLength(100);
                e.Property(x => x.Remarks).HasMaxLength(200);

                // Unique: one shift per employee per day per schedule
                e.HasIndex(x => new { x.WorkScheduleId, x.EmployeeId, x.Day }).IsUnique();

                e.HasOne(x => x.WorkSchedule)
                    .WithMany(x => x.Entries)
                    .HasForeignKey(x => x.WorkScheduleId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.ShiftType)
                    .WithMany(x => x.ScheduleEntries)
                    .HasForeignKey(x => x.ShiftTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── OrganizationStructure (hierarchical build-up) ─────────────────
            builder.Entity<OrganizationStructure>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Code).HasMaxLength(50).IsRequired();
                e.Property(x => x.Name).HasMaxLength(300).IsRequired();
                e.HasIndex(x => x.Code).IsUnique();

                e.HasOne(x => x.Parent)
                    .WithMany(x => x.Children)
                    .HasForeignKey(x => x.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<EmployeeOrganizationAssignment>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.EmployeeId, x.OrganizationStructureId }).IsUnique();
                e.Ignore(x => x.IsActive);

                e.HasOne(x => x.Employee)
                    .WithMany(x => x.OrganizationAssignments)
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.OrganizationStructure)
                    .WithMany()
                    .HasForeignKey(x => x.OrganizationStructureId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<EmployeeDesignation>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.EmployeeId);

                e.HasOne(x => x.Employee)
                    .WithMany(x => x.Designations)
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Designation)
                    .WithMany()
                    .HasForeignKey(x => x.DesignationId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<EmployeeRoleAssignment>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.OrganizationStructureId, x.EmployeeId });
                e.Ignore(x => x.IsActive);

                e.HasOne(x => x.Employee)
                    .WithMany(x => x.RoleAssignments)
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.OrganizationStructure)
                    .WithMany()
                    .HasForeignKey(x => x.OrganizationStructureId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ApprovalWorkflow>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
                e.HasIndex(x => x.ProcessType).IsUnique()
                    .HasFilter("[IsActive] = 1");
            });

            builder.Entity<ApprovalWorkflowStep>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
                e.HasIndex(x => new { x.WorkflowId, x.StepOrder }).IsUnique();

                e.HasOne(x => x.Workflow)
                    .WithMany(x => x.Steps)
                    .HasForeignKey(x => x.WorkflowId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ApprovalWorkflowStepApprover>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.WorkflowStepId, x.Priority }).IsUnique();

                e.HasOne(x => x.WorkflowStep)
                    .WithMany(x => x.Approvers)
                    .HasForeignKey(x => x.WorkflowStepId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ApprovalRequest>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.ReferenceId).HasMaxLength(100).IsRequired();
                e.Property(x => x.SubmittedByUserName).HasMaxLength(100);
                e.HasIndex(x => new { x.ProcessType, x.ReferenceId });

                e.HasOne(x => x.Workflow)
                    .WithMany()
                    .HasForeignKey(x => x.WorkflowId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.SubmittedByEmployee)
                    .WithMany()
                    .HasForeignKey(x => x.SubmittedByEmployeeId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.OrganizationAnchor)
                    .WithMany()
                    .HasForeignKey(x => x.OrganizationAnchorId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<ApprovalRequestStep>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.StepName).HasMaxLength(200).IsRequired();
                e.Property(x => x.ApproverDefinitionSummary).HasMaxLength(500);
                e.Property(x => x.Remarks).HasMaxLength(1000);
                e.Property(x => x.ActionByUserName).HasMaxLength(100);
                e.HasIndex(x => new { x.ApprovalRequestId, x.StepOrder }).IsUnique();

                e.HasOne(x => x.ApprovalRequest)
                    .WithMany(x => x.Steps)
                    .HasForeignKey(x => x.ApprovalRequestId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.ApproverEmployee)
                    .WithMany()
                    .HasForeignKey(x => x.ApproverEmployeeId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.ResolvedOrganizationStructure)
                    .WithMany()
                    .HasForeignKey(x => x.ResolvedOrganizationStructureId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

        }
    }
}