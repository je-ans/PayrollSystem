using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayrollSystem.Data;

#nullable disable

namespace PayrollSystem.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260715131500_AddOvertimeRequests")]
    public partial class AddOvertimeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OvertimeRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestedStartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    RequestedEndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    ApprovedStartTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    ApprovedEndTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SubmittedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovalRequestId = table.Column<int>(type: "int", nullable: true),
                    IsProcessedToPayroll = table.Column<bool>(type: "bit", nullable: false),
                    ProcessedToPayrollOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedToPayrollBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PayrollPeriodId = table.Column<int>(type: "int", nullable: true),
                    DayTypeId = table.Column<int>(type: "int", nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OvertimeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OvertimeRequests_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "dbo",
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OvertimeRequests_DayTypes_DayTypeId",
                        column: x => x.DayTypeId,
                        principalSchema: "dbo",
                        principalTable: "DayTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OvertimeRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OvertimeRequests_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalSchema: "dbo",
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OvertimeRequests_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OvertimeRequests_User_ReviewedById",
                        column: x => x.ReviewedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OvertimeRequests_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_ApprovalRequestId",
                schema: "dbo",
                table: "OvertimeRequests",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_CreatedById",
                schema: "dbo",
                table: "OvertimeRequests",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_DayTypeId",
                schema: "dbo",
                table: "OvertimeRequests",
                column: "DayTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_EmployeeId_WorkDate_Status",
                schema: "dbo",
                table: "OvertimeRequests",
                columns: new[] { "EmployeeId", "WorkDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_PayrollPeriodId",
                schema: "dbo",
                table: "OvertimeRequests",
                column: "PayrollPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_ReviewedById",
                schema: "dbo",
                table: "OvertimeRequests",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_Status_IsProcessedToPayroll",
                schema: "dbo",
                table: "OvertimeRequests",
                columns: new[] { "Status", "IsProcessedToPayroll" });

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_UpdatedById",
                schema: "dbo",
                table: "OvertimeRequests",
                column: "UpdatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OvertimeRequests",
                schema: "dbo");
        }
    }
}
