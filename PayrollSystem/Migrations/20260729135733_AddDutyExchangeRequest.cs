using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayrollSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddDutyExchangeRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DutyExchangeRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationStructureId = table.Column<int>(type: "int", nullable: true),
                    DateFiled = table.Column<DateOnly>(type: "date", nullable: false),
                    FirstEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstOriginalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FirstOriginalShiftTypeId = table.Column<int>(type: "int", nullable: false),
                    FirstExchangedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FirstExchangedShiftTypeId = table.Column<int>(type: "int", nullable: false),
                    SecondEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SecondOriginalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SecondOriginalShiftTypeId = table.Column<int>(type: "int", nullable: false),
                    SecondExchangedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SecondExchangedShiftTypeId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SubmittedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SubmittedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyExchangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DutyExchangeRequests_Employees_FirstEmployeeId",
                        column: x => x.FirstEmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyExchangeRequests_Employees_SecondEmployeeId",
                        column: x => x.SecondEmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyExchangeRequests_OrganizationStructures_OrganizationStructureId",
                        column: x => x.OrganizationStructureId,
                        principalSchema: "dbo",
                        principalTable: "OrganizationStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyExchangeRequests_ShiftTypes_FirstExchangedShiftTypeId",
                        column: x => x.FirstExchangedShiftTypeId,
                        principalSchema: "dbo",
                        principalTable: "ShiftTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyExchangeRequests_ShiftTypes_FirstOriginalShiftTypeId",
                        column: x => x.FirstOriginalShiftTypeId,
                        principalSchema: "dbo",
                        principalTable: "ShiftTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyExchangeRequests_ShiftTypes_SecondExchangedShiftTypeId",
                        column: x => x.SecondExchangedShiftTypeId,
                        principalSchema: "dbo",
                        principalTable: "ShiftTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyExchangeRequests_ShiftTypes_SecondOriginalShiftTypeId",
                        column: x => x.SecondOriginalShiftTypeId,
                        principalSchema: "dbo",
                        principalTable: "ShiftTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyExchangeRequests_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyExchangeRequests_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DutyExchangeRequests_CreatedById",
                schema: "dbo",
                table: "DutyExchangeRequests",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DutyExchangeRequests_FirstEmployeeId_FirstOriginalDate",
                schema: "dbo",
                table: "DutyExchangeRequests",
                columns: new[] { "FirstEmployeeId", "FirstOriginalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyExchangeRequests_FirstExchangedShiftTypeId",
                schema: "dbo",
                table: "DutyExchangeRequests",
                column: "FirstExchangedShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyExchangeRequests_FirstOriginalShiftTypeId",
                schema: "dbo",
                table: "DutyExchangeRequests",
                column: "FirstOriginalShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyExchangeRequests_OrganizationStructureId",
                schema: "dbo",
                table: "DutyExchangeRequests",
                column: "OrganizationStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyExchangeRequests_SecondEmployeeId_SecondOriginalDate",
                schema: "dbo",
                table: "DutyExchangeRequests",
                columns: new[] { "SecondEmployeeId", "SecondOriginalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyExchangeRequests_SecondExchangedShiftTypeId",
                schema: "dbo",
                table: "DutyExchangeRequests",
                column: "SecondExchangedShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyExchangeRequests_SecondOriginalShiftTypeId",
                schema: "dbo",
                table: "DutyExchangeRequests",
                column: "SecondOriginalShiftTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyExchangeRequests_UpdatedById",
                schema: "dbo",
                table: "DutyExchangeRequests",
                column: "UpdatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DutyExchangeRequests",
                schema: "dbo");
        }
    }
}
