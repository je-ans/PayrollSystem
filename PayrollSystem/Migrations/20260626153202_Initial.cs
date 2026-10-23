using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayrollSystem.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "ApprovalWorkflows",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProcessType = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalWorkflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeductionTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeductionTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationStructures",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    Level = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationStructures_OrganizationStructures_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "dbo",
                        principalTable: "OrganizationStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShiftTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Hours = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    IsLeave = table.Column<bool>(type: "bit", nullable: false),
                    ColorHex = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsFlexible = table.Column<bool>(type: "bit", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    SpansNextDay = table.Column<bool>(type: "bit", nullable: false),
                    GracePeriodMinutes = table.Column<int>(type: "int", nullable: true),
                    HasLunchBreak = table.Column<bool>(type: "bit", nullable: false),
                    LunchStart = table.Column<TimeSpan>(type: "time", nullable: true),
                    LunchEnd = table.Column<TimeSpan>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalWorkflowSteps",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowId = table.Column<int>(type: "int", nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalWorkflowSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalWorkflowSteps_ApprovalWorkflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "dbo",
                        principalTable: "ApprovalWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "dbo",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalWorkflowStepApprovers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowStepId = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    OrganizationLevel = table.Column<int>(type: "int", nullable: false),
                    LeadershipRole = table.Column<int>(type: "int", nullable: true),
                    FunctionalRole = table.Column<int>(type: "int", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalWorkflowStepApprovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalWorkflowStepApprovers_ApprovalWorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalSchema: "dbo",
                        principalTable: "ApprovalWorkflowSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Addresses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseBlockLotNo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SubdivisionVillage = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Barangay = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CityMunicipality = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ZipCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Allowances",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AllowanceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AllowanceTypeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    IsTaxable = table.Column<bool>(type: "bit", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allowances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AllowanceTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllowanceTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessType = table.Column<int>(type: "int", nullable: false),
                    ReferenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentStepOrder = table.Column<int>(type: "int", nullable: true),
                    SubmittedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationAnchorId = table.Column<int>(type: "int", nullable: true),
                    WorkflowId = table.Column<int>(type: "int", nullable: false),
                    SubmittedByUserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalRequests_ApprovalWorkflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "dbo",
                        principalTable: "ApprovalWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalRequests_OrganizationStructures_OrganizationAnchorId",
                        column: x => x.OrganizationAnchorId,
                        principalSchema: "dbo",
                        principalTable: "OrganizationStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalRequestSteps",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApprovalRequestId = table.Column<int>(type: "int", nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    StepName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ApproverEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedOrganizationStructureId = table.Column<int>(type: "int", nullable: true),
                    ApproverDefinitionSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActionByUserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequestSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalRequestSteps_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "dbo",
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApprovalRequestSteps_OrganizationStructures_ResolvedOrganizationStructureId",
                        column: x => x.ResolvedOrganizationStructureId,
                        principalSchema: "dbo",
                        principalTable: "OrganizationStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AssignRolePermissions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<int>(type: "int", nullable: true),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignRolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignRolePermissions_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "dbo",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditTrails",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AffectedTable = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OldData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditTrails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BiometricLogs",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BiometricId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RawData = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimeEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HasError = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiometricLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DayTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    BasicMultiplier = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OvertimeMultiplier = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NightDifferentialMultiplier = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NightDifferentialOvertimeMultiplier = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DayTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Deductions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeductionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DeductionTypeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EffectiveStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deductions_DeductionTypes_DeductionTypeId",
                        column: x => x.DeductionTypeId,
                        principalSchema: "dbo",
                        principalTable: "DeductionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DepartmentDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Designations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesignationName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DesignationDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Designations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "User",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MiddleName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomizeName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    DesignationId = table.Column<int>(type: "int", nullable: true),
                    ProfilePicture = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomizeDesignation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PasswordChangedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                    table.ForeignKey(
                        name: "FK_User_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "dbo",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_User_Designations_DesignationId",
                        column: x => x.DesignationId,
                        principalSchema: "dbo",
                        principalTable: "Designations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_User_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "dbo",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GovernmentContributionTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    HasEmployerShare = table.Column<bool>(type: "bit", nullable: false),
                    CalculationMethod = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovernmentContributionTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GovernmentContributionTypes_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GovernmentContributionTypes_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PayrollConfigurations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkingDaysPerMonth = table.Column<int>(type: "int", nullable: false),
                    WorkingHoursPerDay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DefaultWorkStartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    DefaultWorkEndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    DefaultGracePeriodMinutes = table.Column<int>(type: "int", nullable: false),
                    DefaultBreakDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    DefaultHalfDayHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DefaultAutomaticPairingWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    UseWeekendAsDayOff = table.Column<bool>(type: "bit", nullable: false),
                    TaxTableYear = table.Column<int>(type: "int", nullable: false),
                    SSSTableYear = table.Column<int>(type: "int", nullable: false),
                    PhilHealthTableYear = table.Column<int>(type: "int", nullable: false),
                    PagIbigTableYear = table.Column<int>(type: "int", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyShortName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompanyLogoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompanyAddress1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompanyAddress2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompanyCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompanyProvince = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompanyZipCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CompanyContactNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyEmail = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CompanyWebsite = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompanyTIN = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompanySSSNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyPhilHealthNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyPagIbigNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollConfigurations_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PayrollConfigurations_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PayrollPeriods",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PeriodStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollPeriods_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PayrollPeriods_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RateTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RateBasis = table.Column<int>(type: "int", nullable: false),
                    MonthlySalary = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DailyRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    HourlyRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    EffectiveStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RateTypes_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RateTypes_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SystemInfos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SystemName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemInfos_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SystemInfos_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SystemTasks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TaskName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    OrderNumber = table.Column<int>(type: "int", nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemTasks_SystemTasks_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "dbo",
                        principalTable: "SystemTasks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SystemTasks_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SystemTasks_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TaxTables",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TaxYear = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    BracketType = table.Column<int>(type: "int", nullable: false),
                    IncomeBasis = table.Column<int>(type: "int", nullable: false),
                    BracketOrder = table.Column<int>(type: "int", nullable: false),
                    MinIncome = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxIncome = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FixedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BaseTaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EffectiveStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxTables_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TaxTables_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                schema: "dbo",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "dbo",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "dbo",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                schema: "dbo",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GovernmentContributionTables",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GovernmentContributionTypeId = table.Column<int>(type: "int", nullable: false),
                    MinSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EmployeeShare = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EmployerShare = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalContribution = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EmployeeSharePercentage = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    EmployerSharePercentage = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    MaxEmployeeContribution = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxEmployerContribution = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MinEmployeeContribution = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EffectiveStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovernmentContributionTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GovernmentContributionTables_GovernmentContributionTypes_GovernmentContributionTypeId",
                        column: x => x.GovernmentContributionTypeId,
                        principalSchema: "dbo",
                        principalTable: "GovernmentContributionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GovernmentContributionTables_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GovernmentContributionTables_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PayrollPeriodBatches",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollPeriodId = table.Column<int>(type: "int", nullable: false),
                    DepartmentDisplay = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmploymentTypeDisplay = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    EmployeeCount = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPeriodBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollPeriodBatches_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalSchema: "dbo",
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRuns",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollPeriodId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProcessedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TotalEmployees = table.Column<int>(type: "int", nullable: false),
                    TotalGrossPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalNetPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalBenefits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsFinalized = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollRuns_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalSchema: "dbo",
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollRuns_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PayrollRuns_User_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PayrollRuns_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DefaultRateTypeId = table.Column<int>(type: "int", nullable: true),
                    EmployeeIdNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Surname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameExtension = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Sex = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TelephoneNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MobileNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmploymentBasis = table.Column<int>(type: "int", nullable: false),
                    BiometricId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DateHired = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    GovContributionRule = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_RateTypes_DefaultRateTypeId",
                        column: x => x.DefaultRateTypeId,
                        principalSchema: "dbo",
                        principalTable: "RateTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Employees_User_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Employees_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Employees_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeAllowances",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AllowanceId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAllowances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeAllowances_Allowances_AllowanceId",
                        column: x => x.AllowanceId,
                        principalSchema: "dbo",
                        principalTable: "Allowances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeAllowances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeAllowances_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeAllowances_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDeductions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeductionId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDeductions_Deductions_DeductionId",
                        column: x => x.DeductionId,
                        principalSchema: "dbo",
                        principalTable: "Deductions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeDeductions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDeductions_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeDeductions_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDesignations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DesignationId = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDesignations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDesignations_Designations_DesignationId",
                        column: x => x.DesignationId,
                        principalSchema: "dbo",
                        principalTable: "Designations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDesignations_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeLoans",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeductionTypeId = table.Column<int>(type: "int", nullable: false),
                    LoanName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PrincipalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InterestRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TotalInstallments = table.Column<int>(type: "int", nullable: false),
                    InstallmentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmountToRepay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RemainingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InstallmentsPaid = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLoans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeLoans_DeductionTypes_DeductionTypeId",
                        column: x => x.DeductionTypeId,
                        principalSchema: "dbo",
                        principalTable: "DeductionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeLoans_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeLoans_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeLoans_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeOrganizationAssignments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationStructureId = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeOrganizationAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeOrganizationAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeOrganizationAssignments_OrganizationStructures_OrganizationStructureId",
                        column: x => x.OrganizationStructureId,
                        principalSchema: "dbo",
                        principalTable: "OrganizationStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeRoleAssignments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationStructureId = table.Column<int>(type: "int", nullable: false),
                    LeadershipRole = table.Column<int>(type: "int", nullable: true),
                    FunctionalRole = table.Column<int>(type: "int", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeRoleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeRoleAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeRoleAssignments_OrganizationStructures_OrganizationStructureId",
                        column: x => x.OrganizationStructureId,
                        principalSchema: "dbo",
                        principalTable: "OrganizationStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollRunId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegularHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NightDifferentialHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NightDifferentialOvertimeHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HolidayHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RestDayHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OvertimeRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NightDifferentialRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RegularPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OvertimePay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NightDifferentialPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HolidayPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RestDayPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrossPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalBenefits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxWithheld = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollEntries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollEntries_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalSchema: "dbo",
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollEntries_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PayrollEntries_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PayrollPeriodEmployees",
                schema: "dbo",
                columns: table => new
                {
                    PayrollPeriodId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPeriodEmployees", x => new { x.PayrollPeriodId, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_PayrollPeriodEmployees_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollPeriodEmployees_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalSchema: "dbo",
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollPeriodEmployees_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PayrollPeriodEmployees_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PayrollPeriodTimeCards",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollPeriodId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollPeriodBatchId = table.Column<int>(type: "int", nullable: true),
                    LateMinutes = table.Column<int>(type: "int", nullable: false),
                    UndertimeMinutes = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPeriodTimeCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollPeriodTimeCards_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollPeriodTimeCards_PayrollPeriodBatches_PayrollPeriodBatchId",
                        column: x => x.PayrollPeriodBatchId,
                        principalSchema: "dbo",
                        principalTable: "PayrollPeriodBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PayrollPeriodTimeCards_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalSchema: "dbo",
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollPeriodTimeCards_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PayrollPeriodTimeCards_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PersonalInformation",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Surname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameExtension = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlaceOfBirth = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Sex = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CivilStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Citizenship = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Height = table.Column<double>(type: "float", nullable: true),
                    Weight = table.Column<double>(type: "float", nullable: true),
                    BloodType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GsisIdNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PagIbigIdNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhilHealthNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SssNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TinNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgencyEmployeeNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResidentialAddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PermanentAddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TelephoneNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MobileNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmergencyContactName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmergencyContactRelationship = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EmergencyContactNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalInformation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonalInformation_Addresses_PermanentAddressId",
                        column: x => x.PermanentAddressId,
                        principalSchema: "dbo",
                        principalTable: "Addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalInformation_Addresses_ResidentialAddressId",
                        column: x => x.ResidentialAddressId,
                        principalSchema: "dbo",
                        principalTable: "Addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalInformation_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonalInformation_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PersonalInformation_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SalaryHistories",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeType = table.Column<int>(type: "int", nullable: false),
                    PreviousBasicSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewBasicSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SalaryDifference = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApprovedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryHistories_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalaryHistories_User_ApprovedById",
                        column: x => x.ApprovedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SalaryHistories_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SalaryHistories_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TimeEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryType = table.Column<int>(type: "int", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EntryTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    BiometricLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BiometricDeviceId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PairedEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ValidationNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsManualEntry = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeEntries_BiometricLogs_BiometricLogId",
                        column: x => x.BiometricLogId,
                        principalSchema: "dbo",
                        principalTable: "BiometricLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TimeEntries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimeEntries_TimeEntries_PairedEntryId",
                        column: x => x.PairedEntryId,
                        principalSchema: "dbo",
                        principalTable: "TimeEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimeLogJustifications",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RequestedAmIn = table.Column<TimeOnly>(type: "time", nullable: true),
                    RequestedLunchOut = table.Column<TimeOnly>(type: "time", nullable: true),
                    RequestedLunchIn = table.Column<TimeOnly>(type: "time", nullable: true),
                    RequestedPmOut = table.Column<TimeOnly>(type: "time", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SubmittedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ReviewedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeLogJustifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeLogJustifications_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimeLogJustifications_User_ReviewedById",
                        column: x => x.ReviewedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WorkSchedules",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationStructureId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalRequestId = table.Column<int>(type: "int", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkSchedules_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "dbo",
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkSchedules_Employees_SubmittedByEmployeeId",
                        column: x => x.SubmittedByEmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkSchedules_OrganizationStructures_OrganizationStructureId",
                        column: x => x.OrganizationStructureId,
                        principalSchema: "dbo",
                        principalTable: "OrganizationStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollDeductions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollEntryId = table.Column<int>(type: "int", nullable: false),
                    DeductionTypeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TaxableAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PrincipalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InterestAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BalanceRemaining = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InstallmentNumber = table.Column<int>(type: "int", nullable: true),
                    TotalInstallments = table.Column<int>(type: "int", nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollDeductions_DeductionTypes_DeductionTypeId",
                        column: x => x.DeductionTypeId,
                        principalSchema: "dbo",
                        principalTable: "DeductionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollDeductions_PayrollEntries_PayrollEntryId",
                        column: x => x.PayrollEntryId,
                        principalSchema: "dbo",
                        principalTable: "PayrollEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollDeductions_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PayrollDeductions_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PayrollEntryGovernmentContribution",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollEntryId = table.Column<int>(type: "int", nullable: false),
                    GovernmentContributionTypeId = table.Column<int>(type: "int", nullable: false),
                    ContributionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmployeeShare = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EmployerShare = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalContribution = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SalaryBasis = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollEntryGovernmentContribution", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollEntryGovernmentContribution_GovernmentContributionTypes_GovernmentContributionTypeId",
                        column: x => x.GovernmentContributionTypeId,
                        principalSchema: "dbo",
                        principalTable: "GovernmentContributionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollEntryGovernmentContribution_PayrollEntries_PayrollEntryId",
                        column: x => x.PayrollEntryId,
                        principalSchema: "dbo",
                        principalTable: "PayrollEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payslips",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollEntryId = table.Column<int>(type: "int", nullable: false),
                    PayslipNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GeneratedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PrintedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsSent = table.Column<bool>(type: "bit", nullable: false),
                    IsPrinted = table.Column<bool>(type: "bit", nullable: false),
                    SendAttempts = table.Column<int>(type: "int", nullable: false),
                    SendErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EmployeeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmployeeIdNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DepartmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DesignationName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RateTypeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PeriodStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GrossPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalBenefits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetPay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payslips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payslips_PayrollEntries_PayrollEntryId",
                        column: x => x.PayrollEntryId,
                        principalSchema: "dbo",
                        principalTable: "PayrollEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payslips_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Payslips_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PayrollPeriodTimeCardDetails",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimeCardId = table.Column<int>(type: "int", nullable: false),
                    DayTypeId = table.Column<int>(type: "int", nullable: false),
                    RegularHours = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NightDifferentialHours = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NightDifferentialOvertimeHours = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPeriodTimeCardDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollPeriodTimeCardDetails_DayTypes_DayTypeId",
                        column: x => x.DayTypeId,
                        principalSchema: "dbo",
                        principalTable: "DayTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollPeriodTimeCardDetails_PayrollPeriodTimeCards_TimeCardId",
                        column: x => x.TimeCardId,
                        principalSchema: "dbo",
                        principalTable: "PayrollPeriodTimeCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleChangeRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkScheduleId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    CurrentShiftTypeId = table.Column<int>(type: "int", nullable: false),
                    RequestedShiftTypeId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SubmittedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ReviewedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleChangeRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleChangeRequests_ShiftTypes_CurrentShiftTypeId",
                        column: x => x.CurrentShiftTypeId,
                        principalSchema: "dbo",
                        principalTable: "ShiftTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleChangeRequests_ShiftTypes_RequestedShiftTypeId",
                        column: x => x.RequestedShiftTypeId,
                        principalSchema: "dbo",
                        principalTable: "ShiftTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleChangeRequests_User_ReviewedById",
                        column: x => x.ReviewedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScheduleChangeRequests_WorkSchedules_WorkScheduleId",
                        column: x => x.WorkScheduleId,
                        principalSchema: "dbo",
                        principalTable: "WorkSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkScheduleId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    ShiftTypeId = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleEntries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleEntries_ShiftTypes_ShiftTypeId",
                        column: x => x.ShiftTypeId,
                        principalSchema: "dbo",
                        principalTable: "ShiftTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleEntries_WorkSchedules_WorkScheduleId",
                        column: x => x.WorkScheduleId,
                        principalSchema: "dbo",
                        principalTable: "WorkSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_CreatedById",
                schema: "dbo",
                table: "Addresses",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_UpdatedById",
                schema: "dbo",
                table: "Addresses",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Allowances_AllowanceTypeId",
                schema: "dbo",
                table: "Allowances",
                column: "AllowanceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Allowances_CreatedById",
                schema: "dbo",
                table: "Allowances",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Allowances_UpdatedById",
                schema: "dbo",
                table: "Allowances",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceTypes_CreatedById",
                schema: "dbo",
                table: "AllowanceTypes",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceTypes_UpdatedById",
                schema: "dbo",
                table: "AllowanceTypes",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_OrganizationAnchorId",
                schema: "dbo",
                table: "ApprovalRequests",
                column: "OrganizationAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_ProcessType_ReferenceId",
                schema: "dbo",
                table: "ApprovalRequests",
                columns: new[] { "ProcessType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_SubmittedByEmployeeId",
                schema: "dbo",
                table: "ApprovalRequests",
                column: "SubmittedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_WorkflowId",
                schema: "dbo",
                table: "ApprovalRequests",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequestSteps_ApprovalRequestId_StepOrder",
                schema: "dbo",
                table: "ApprovalRequestSteps",
                columns: new[] { "ApprovalRequestId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequestSteps_ApproverEmployeeId",
                schema: "dbo",
                table: "ApprovalRequestSteps",
                column: "ApproverEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequestSteps_ResolvedOrganizationStructureId",
                schema: "dbo",
                table: "ApprovalRequestSteps",
                column: "ResolvedOrganizationStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_ProcessType",
                schema: "dbo",
                table: "ApprovalWorkflows",
                column: "ProcessType",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflowStepApprovers_WorkflowStepId_Priority",
                schema: "dbo",
                table: "ApprovalWorkflowStepApprovers",
                columns: new[] { "WorkflowStepId", "Priority" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflowSteps_WorkflowId_StepOrder",
                schema: "dbo",
                table: "ApprovalWorkflowSteps",
                columns: new[] { "WorkflowId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignRolePermissions_CreatedById",
                schema: "dbo",
                table: "AssignRolePermissions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssignRolePermissions_RoleId",
                schema: "dbo",
                table: "AssignRolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignRolePermissions_TaskId",
                schema: "dbo",
                table: "AssignRolePermissions",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignRolePermissions_UpdatedById",
                schema: "dbo",
                table: "AssignRolePermissions",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrails_AffectedTable",
                schema: "dbo",
                table: "AuditTrails",
                column: "AffectedTable");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrails_Module_TimeStamp",
                schema: "dbo",
                table: "AuditTrails",
                columns: new[] { "Module", "TimeStamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrails_UserId_TimeStamp",
                schema: "dbo",
                table: "AuditTrails",
                columns: new[] { "UserId", "TimeStamp" });

            migrationBuilder.CreateIndex(
                name: "IX_BiometricLogs_EmployeeId_Timestamp_DeviceId",
                schema: "dbo",
                table: "BiometricLogs",
                columns: new[] { "EmployeeId", "Timestamp", "DeviceId" });

            migrationBuilder.CreateIndex(
                name: "IX_DayTypes_CreatedById",
                schema: "dbo",
                table: "DayTypes",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DayTypes_UpdatedById",
                schema: "dbo",
                table: "DayTypes",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Deductions_CreatedById",
                schema: "dbo",
                table: "Deductions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Deductions_DeductionTypeId",
                schema: "dbo",
                table: "Deductions",
                column: "DeductionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Deductions_UpdatedById",
                schema: "dbo",
                table: "Deductions",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_CreatedById",
                schema: "dbo",
                table: "Departments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_UpdatedById",
                schema: "dbo",
                table: "Departments",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Designations_CreatedById",
                schema: "dbo",
                table: "Designations",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Designations_UpdatedById",
                schema: "dbo",
                table: "Designations",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAllowances_AllowanceId_EmployeeId",
                schema: "dbo",
                table: "EmployeeAllowances",
                columns: new[] { "AllowanceId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAllowances_CreatedById",
                schema: "dbo",
                table: "EmployeeAllowances",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAllowances_EmployeeId",
                schema: "dbo",
                table: "EmployeeAllowances",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAllowances_UpdatedById",
                schema: "dbo",
                table: "EmployeeAllowances",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDeductions_CreatedById",
                schema: "dbo",
                table: "EmployeeDeductions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDeductions_DeductionId_EmployeeId",
                schema: "dbo",
                table: "EmployeeDeductions",
                columns: new[] { "DeductionId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDeductions_EmployeeId",
                schema: "dbo",
                table: "EmployeeDeductions",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDeductions_UpdatedById",
                schema: "dbo",
                table: "EmployeeDeductions",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDesignations_DesignationId",
                schema: "dbo",
                table: "EmployeeDesignations",
                column: "DesignationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDesignations_EmployeeId",
                schema: "dbo",
                table: "EmployeeDesignations",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLoans_CreatedById",
                schema: "dbo",
                table: "EmployeeLoans",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLoans_DeductionTypeId",
                schema: "dbo",
                table: "EmployeeLoans",
                column: "DeductionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLoans_EmployeeId",
                schema: "dbo",
                table: "EmployeeLoans",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLoans_UpdatedById",
                schema: "dbo",
                table: "EmployeeLoans",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOrganizationAssignments_EmployeeId_OrganizationStructureId",
                schema: "dbo",
                table: "EmployeeOrganizationAssignments",
                columns: new[] { "EmployeeId", "OrganizationStructureId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeOrganizationAssignments_OrganizationStructureId",
                schema: "dbo",
                table: "EmployeeOrganizationAssignments",
                column: "OrganizationStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRoleAssignments_EmployeeId",
                schema: "dbo",
                table: "EmployeeRoleAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRoleAssignments_OrganizationStructureId_EmployeeId",
                schema: "dbo",
                table: "EmployeeRoleAssignments",
                columns: new[] { "OrganizationStructureId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ApplicationUserId",
                schema: "dbo",
                table: "Employees",
                column: "ApplicationUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CreatedById",
                schema: "dbo",
                table: "Employees",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DefaultRateTypeId",
                schema: "dbo",
                table: "Employees",
                column: "DefaultRateTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UpdatedById",
                schema: "dbo",
                table: "Employees",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_GovernmentContributionTables_CreatedById",
                schema: "dbo",
                table: "GovernmentContributionTables",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_GovernmentContributionTables_GovernmentContributionTypeId_EffectiveStartDate_MinSalary",
                schema: "dbo",
                table: "GovernmentContributionTables",
                columns: new[] { "GovernmentContributionTypeId", "EffectiveStartDate", "MinSalary" });

            migrationBuilder.CreateIndex(
                name: "IX_GovernmentContributionTables_UpdatedById",
                schema: "dbo",
                table: "GovernmentContributionTables",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_GovernmentContributionTypes_CreatedById",
                schema: "dbo",
                table: "GovernmentContributionTypes",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_GovernmentContributionTypes_UpdatedById",
                schema: "dbo",
                table: "GovernmentContributionTypes",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationStructures_Code",
                schema: "dbo",
                table: "OrganizationStructures",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationStructures_ParentId",
                schema: "dbo",
                table: "OrganizationStructures",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollConfigurations_CreatedById",
                schema: "dbo",
                table: "PayrollConfigurations",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollConfigurations_UpdatedById",
                schema: "dbo",
                table: "PayrollConfigurations",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDeductions_CreatedById",
                schema: "dbo",
                table: "PayrollDeductions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDeductions_DeductionTypeId",
                schema: "dbo",
                table: "PayrollDeductions",
                column: "DeductionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDeductions_PayrollEntryId",
                schema: "dbo",
                table: "PayrollDeductions",
                column: "PayrollEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDeductions_UpdatedById",
                schema: "dbo",
                table: "PayrollDeductions",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_CreatedById",
                schema: "dbo",
                table: "PayrollEntries",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_EmployeeId",
                schema: "dbo",
                table: "PayrollEntries",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_PayrollRunId_EmployeeId",
                schema: "dbo",
                table: "PayrollEntries",
                columns: new[] { "PayrollRunId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_UpdatedById",
                schema: "dbo",
                table: "PayrollEntries",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntryGovernmentContribution_GovernmentContributionTypeId",
                schema: "dbo",
                table: "PayrollEntryGovernmentContribution",
                column: "GovernmentContributionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntryGovernmentContribution_PayrollEntryId",
                schema: "dbo",
                table: "PayrollEntryGovernmentContribution",
                column: "PayrollEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriodBatches_PayrollPeriodId",
                schema: "dbo",
                table: "PayrollPeriodBatches",
                column: "PayrollPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriodEmployees_CreatedById",
                schema: "dbo",
                table: "PayrollPeriodEmployees",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriodEmployees_EmployeeId",
                schema: "dbo",
                table: "PayrollPeriodEmployees",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriodEmployees_UpdatedById",
                schema: "dbo",
                table: "PayrollPeriodEmployees",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_CreatedById",
                schema: "dbo",
                table: "PayrollPeriods",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_PeriodStartDate_PeriodEndDate",
                schema: "dbo",
                table: "PayrollPeriods",
                columns: new[] { "PeriodStartDate", "PeriodEndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_UpdatedById",
                schema: "dbo",
                table: "PayrollPeriods",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriodTimeCardDetails_DayTypeId",
                schema: "dbo",
                table: "PayrollPeriodTimeCardDetails",
                column: "DayTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriodTimeCardDetails_TimeCardId_DayTypeId",
                schema: "dbo",
                table: "PayrollPeriodTimeCardDetails",
                columns: new[] { "TimeCardId", "DayTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriodTimeCards_CreatedById",
                schema: "dbo",
                table: "PayrollPeriodTimeCards",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriodTimeCards_EmployeeId",
                schema: "dbo",
                table: "PayrollPeriodTimeCards",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriodTimeCards_PayrollPeriodBatchId",
                schema: "dbo",
                table: "PayrollPeriodTimeCards",
                column: "PayrollPeriodBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriodTimeCards_PayrollPeriodId_EmployeeId",
                schema: "dbo",
                table: "PayrollPeriodTimeCards",
                columns: new[] { "PayrollPeriodId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriodTimeCards_UpdatedById",
                schema: "dbo",
                table: "PayrollPeriodTimeCards",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_CreatedById",
                schema: "dbo",
                table: "PayrollRuns",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_PayrollPeriodId_Status",
                schema: "dbo",
                table: "PayrollRuns",
                columns: new[] { "PayrollPeriodId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_ProcessedByUserId",
                schema: "dbo",
                table: "PayrollRuns",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_UpdatedById",
                schema: "dbo",
                table: "PayrollRuns",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_CreatedById",
                schema: "dbo",
                table: "Payslips",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_PayrollEntryId",
                schema: "dbo",
                table: "Payslips",
                column: "PayrollEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_PayslipNumber",
                schema: "dbo",
                table: "Payslips",
                column: "PayslipNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_UpdatedById",
                schema: "dbo",
                table: "Payslips",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalInformation_CreatedById",
                schema: "dbo",
                table: "PersonalInformation",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalInformation_EmployeeId",
                schema: "dbo",
                table: "PersonalInformation",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalInformation_PermanentAddressId",
                schema: "dbo",
                table: "PersonalInformation",
                column: "PermanentAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalInformation_ResidentialAddressId",
                schema: "dbo",
                table: "PersonalInformation",
                column: "ResidentialAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalInformation_UpdatedById",
                schema: "dbo",
                table: "PersonalInformation",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_RateTypes_CreatedById",
                schema: "dbo",
                table: "RateTypes",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_RateTypes_UpdatedById",
                schema: "dbo",
                table: "RateTypes",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "dbo",
                table: "Role",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                schema: "dbo",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryHistories_ApprovedById",
                schema: "dbo",
                table: "SalaryHistories",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryHistories_CreatedById",
                schema: "dbo",
                table: "SalaryHistories",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryHistories_EmployeeId",
                schema: "dbo",
                table: "SalaryHistories",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryHistories_UpdatedById",
                schema: "dbo",
                table: "SalaryHistories",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleChangeRequests_CurrentShiftTypeId",
                schema: "dbo",
                table: "ScheduleChangeRequests",
                column: "CurrentShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleChangeRequests_EmployeeId_WorkScheduleId_Day_Status",
                schema: "dbo",
                table: "ScheduleChangeRequests",
                columns: new[] { "EmployeeId", "WorkScheduleId", "Day", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleChangeRequests_RequestedShiftTypeId",
                schema: "dbo",
                table: "ScheduleChangeRequests",
                column: "RequestedShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleChangeRequests_ReviewedById",
                schema: "dbo",
                table: "ScheduleChangeRequests",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleChangeRequests_WorkScheduleId",
                schema: "dbo",
                table: "ScheduleChangeRequests",
                column: "WorkScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_EmployeeId",
                schema: "dbo",
                table: "ScheduleEntries",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_ShiftTypeId",
                schema: "dbo",
                table: "ScheduleEntries",
                column: "ShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_WorkScheduleId_EmployeeId_Day",
                schema: "dbo",
                table: "ScheduleEntries",
                columns: new[] { "WorkScheduleId", "EmployeeId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTypes_Code",
                schema: "dbo",
                table: "ShiftTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemInfos_CreatedById",
                schema: "dbo",
                table: "SystemInfos",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_SystemInfos_UpdatedById",
                schema: "dbo",
                table: "SystemInfos",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_SystemTasks_CreatedById",
                schema: "dbo",
                table: "SystemTasks",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_SystemTasks_ParentId",
                schema: "dbo",
                table: "SystemTasks",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemTasks_UpdatedById",
                schema: "dbo",
                table: "SystemTasks",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TaxTables_CreatedById",
                schema: "dbo",
                table: "TaxTables",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TaxTables_IncomeBasis_EffectiveStartDate_BracketOrder",
                schema: "dbo",
                table: "TaxTables",
                columns: new[] { "IncomeBasis", "EffectiveStartDate", "BracketOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxTables_UpdatedById",
                schema: "dbo",
                table: "TaxTables",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_BiometricLogId",
                schema: "dbo",
                table: "TimeEntries",
                column: "BiometricLogId",
                unique: true,
                filter: "[BiometricLogId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_EmployeeId",
                schema: "dbo",
                table: "TimeEntries",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_PairedEntryId",
                schema: "dbo",
                table: "TimeEntries",
                column: "PairedEntryId",
                unique: true,
                filter: "[PairedEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TimeLogJustifications_EmployeeId_EntryDate_Status",
                schema: "dbo",
                table: "TimeLogJustifications",
                columns: new[] { "EmployeeId", "EntryDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeLogJustifications_ReviewedById",
                schema: "dbo",
                table: "TimeLogJustifications",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "dbo",
                table: "User",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_User_DepartmentId",
                schema: "dbo",
                table: "User",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_User_DesignationId",
                schema: "dbo",
                table: "User",
                column: "DesignationId");

            migrationBuilder.CreateIndex(
                name: "IX_User_RoleId",
                schema: "dbo",
                table: "User",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "dbo",
                table: "User",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                schema: "dbo",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                schema: "dbo",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "dbo",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_ApprovalRequestId",
                schema: "dbo",
                table: "WorkSchedules",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_Month_Year_OrganizationStructureId",
                schema: "dbo",
                table: "WorkSchedules",
                columns: new[] { "Month", "Year", "OrganizationStructureId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_OrganizationStructureId",
                schema: "dbo",
                table: "WorkSchedules",
                column: "OrganizationStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_SubmittedByEmployeeId",
                schema: "dbo",
                table: "WorkSchedules",
                column: "SubmittedByEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Addresses_User_CreatedById",
                schema: "dbo",
                table: "Addresses",
                column: "CreatedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Addresses_User_UpdatedById",
                schema: "dbo",
                table: "Addresses",
                column: "UpdatedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Allowances_AllowanceTypes_AllowanceTypeId",
                schema: "dbo",
                table: "Allowances",
                column: "AllowanceTypeId",
                principalSchema: "dbo",
                principalTable: "AllowanceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Allowances_User_CreatedById",
                schema: "dbo",
                table: "Allowances",
                column: "CreatedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Allowances_User_UpdatedById",
                schema: "dbo",
                table: "Allowances",
                column: "UpdatedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AllowanceTypes_User_CreatedById",
                schema: "dbo",
                table: "AllowanceTypes",
                column: "CreatedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AllowanceTypes_User_UpdatedById",
                schema: "dbo",
                table: "AllowanceTypes",
                column: "UpdatedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalRequests_Employees_SubmittedByEmployeeId",
                schema: "dbo",
                table: "ApprovalRequests",
                column: "SubmittedByEmployeeId",
                principalSchema: "dbo",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalRequestSteps_Employees_ApproverEmployeeId",
                schema: "dbo",
                table: "ApprovalRequestSteps",
                column: "ApproverEmployeeId",
                principalSchema: "dbo",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AssignRolePermissions_SystemTasks_TaskId",
                schema: "dbo",
                table: "AssignRolePermissions",
                column: "TaskId",
                principalSchema: "dbo",
                principalTable: "SystemTasks",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AssignRolePermissions_User_CreatedById",
                schema: "dbo",
                table: "AssignRolePermissions",
                column: "CreatedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AssignRolePermissions_User_UpdatedById",
                schema: "dbo",
                table: "AssignRolePermissions",
                column: "UpdatedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditTrails_User_UserId",
                schema: "dbo",
                table: "AuditTrails",
                column: "UserId",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BiometricLogs_Employees_EmployeeId",
                schema: "dbo",
                table: "BiometricLogs",
                column: "EmployeeId",
                principalSchema: "dbo",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DayTypes_User_CreatedById",
                schema: "dbo",
                table: "DayTypes",
                column: "CreatedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DayTypes_User_UpdatedById",
                schema: "dbo",
                table: "DayTypes",
                column: "UpdatedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Deductions_User_CreatedById",
                schema: "dbo",
                table: "Deductions",
                column: "CreatedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Deductions_User_UpdatedById",
                schema: "dbo",
                table: "Deductions",
                column: "UpdatedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_User_CreatedById",
                schema: "dbo",
                table: "Departments",
                column: "CreatedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_User_UpdatedById",
                schema: "dbo",
                table: "Departments",
                column: "UpdatedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Designations_User_CreatedById",
                schema: "dbo",
                table: "Designations",
                column: "CreatedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Designations_User_UpdatedById",
                schema: "dbo",
                table: "Designations",
                column: "UpdatedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_User_CreatedById",
                schema: "dbo",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_Departments_User_UpdatedById",
                schema: "dbo",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_Designations_User_CreatedById",
                schema: "dbo",
                table: "Designations");

            migrationBuilder.DropForeignKey(
                name: "FK_Designations_User_UpdatedById",
                schema: "dbo",
                table: "Designations");

            migrationBuilder.DropTable(
                name: "ApprovalRequestSteps",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ApprovalWorkflowStepApprovers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AssignRolePermissions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AuditTrails",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EmployeeAllowances",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EmployeeDeductions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EmployeeDesignations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EmployeeLoans",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EmployeeOrganizationAssignments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EmployeeRoleAssignments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "GovernmentContributionTables",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PayrollConfigurations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PayrollDeductions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PayrollEntryGovernmentContribution",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PayrollPeriodEmployees",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PayrollPeriodTimeCardDetails",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Payslips",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PersonalInformation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RoleClaims",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SalaryHistories",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ScheduleChangeRequests",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ScheduleEntries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SystemInfos",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TaxTables",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TimeEntries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TimeLogJustifications",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UserClaims",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UserLogins",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UserTokens",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ApprovalWorkflowSteps",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SystemTasks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Allowances",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Deductions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "GovernmentContributionTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DayTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PayrollPeriodTimeCards",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PayrollEntries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Addresses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ShiftTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "WorkSchedules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "BiometricLogs",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AllowanceTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DeductionTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PayrollPeriodBatches",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PayrollRuns",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ApprovalRequests",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PayrollPeriods",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ApprovalWorkflows",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Employees",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OrganizationStructures",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RateTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "User",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Departments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Designations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Role",
                schema: "dbo");
        }
    }
}
