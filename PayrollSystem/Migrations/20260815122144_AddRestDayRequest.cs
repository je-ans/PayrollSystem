using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayrollSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddRestDayRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SubmittedBy",
                schema: "dbo",
                table: "DutyExchangeRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ExchangeRestDayRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationStructureId = table.Column<int>(type: "int", nullable: true),
                    DateFiled = table.Column<DateOnly>(type: "date", nullable: false),
                    FirstEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SecondEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstRestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SecondRestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FirstRestShiftTypeId = table.Column<int>(type: "int", nullable: false),
                    SecondRestShiftTypeId = table.Column<int>(type: "int", nullable: false),
                    FirstWorkShiftTypeId = table.Column<int>(type: "int", nullable: false),
                    SecondWorkShiftTypeId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SubmittedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SubmittedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalRequestId = table.Column<int>(type: "int", nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRestDayRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExchangeRestDayRequests_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "dbo",
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRestDayRequests_Employees_FirstEmployeeId",
                        column: x => x.FirstEmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRestDayRequests_Employees_SecondEmployeeId",
                        column: x => x.SecondEmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRestDayRequests_OrganizationStructures_OrganizationStructureId",
                        column: x => x.OrganizationStructureId,
                        principalSchema: "dbo",
                        principalTable: "OrganizationStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRestDayRequests_ShiftTypes_FirstRestShiftTypeId",
                        column: x => x.FirstRestShiftTypeId,
                        principalSchema: "dbo",
                        principalTable: "ShiftTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRestDayRequests_ShiftTypes_FirstWorkShiftTypeId",
                        column: x => x.FirstWorkShiftTypeId,
                        principalSchema: "dbo",
                        principalTable: "ShiftTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRestDayRequests_ShiftTypes_SecondRestShiftTypeId",
                        column: x => x.SecondRestShiftTypeId,
                        principalSchema: "dbo",
                        principalTable: "ShiftTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRestDayRequests_ShiftTypes_SecondWorkShiftTypeId",
                        column: x => x.SecondWorkShiftTypeId,
                        principalSchema: "dbo",
                        principalTable: "ShiftTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRestDayRequests_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRestDayRequests_User_ReviewedById",
                        column: x => x.ReviewedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRestDayRequests_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRestDayRequests_ApprovalRequestId",
                schema: "dbo",
                table: "ExchangeRestDayRequests",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRestDayRequests_CreatedById",
                schema: "dbo",
                table: "ExchangeRestDayRequests",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRestDayRequests_FirstEmployeeId_FirstRestDate",
                schema: "dbo",
                table: "ExchangeRestDayRequests",
                columns: new[] { "FirstEmployeeId", "FirstRestDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRestDayRequests_FirstRestShiftTypeId",
                schema: "dbo",
                table: "ExchangeRestDayRequests",
                column: "FirstRestShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRestDayRequests_FirstWorkShiftTypeId",
                schema: "dbo",
                table: "ExchangeRestDayRequests",
                column: "FirstWorkShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRestDayRequests_OrganizationStructureId",
                schema: "dbo",
                table: "ExchangeRestDayRequests",
                column: "OrganizationStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRestDayRequests_ReviewedById",
                schema: "dbo",
                table: "ExchangeRestDayRequests",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRestDayRequests_SecondEmployeeId_SecondRestDate",
                schema: "dbo",
                table: "ExchangeRestDayRequests",
                columns: new[] { "SecondEmployeeId", "SecondRestDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRestDayRequests_SecondRestShiftTypeId",
                schema: "dbo",
                table: "ExchangeRestDayRequests",
                column: "SecondRestShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRestDayRequests_SecondWorkShiftTypeId",
                schema: "dbo",
                table: "ExchangeRestDayRequests",
                column: "SecondWorkShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRestDayRequests_UpdatedById",
                schema: "dbo",
                table: "ExchangeRestDayRequests",
                column: "UpdatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeRestDayRequests",
                schema: "dbo");

            migrationBuilder.AlterColumn<string>(
                name: "SubmittedBy",
                schema: "dbo",
                table: "DutyExchangeRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);
        }
    }
}
