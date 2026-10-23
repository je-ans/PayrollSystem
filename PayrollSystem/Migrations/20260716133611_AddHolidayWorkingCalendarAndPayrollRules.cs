using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayrollSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddHolidayWorkingCalendarAndPayrollRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBreakPaid",
                schema: "dbo",
                table: "ShiftTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MinimumOvertimeMinutes",
                schema: "dbo",
                table: "PayrollConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "NightDifferentialEndTime",
                schema: "dbo",
                table: "PayrollConfigurations",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "NightDifferentialStartTime",
                schema: "dbo",
                table: "PayrollConfigurations",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "OvertimeRoundingRule",
                schema: "dbo",
                table: "PayrollConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequireOvertimeApproval",
                schema: "dbo",
                table: "PayrollConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Holidays",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    HolidayType = table.Column<int>(type: "int", nullable: false),
                    DayTypeId = table.Column<int>(type: "int", nullable: false),
                    IsRecurring = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Holidays_DayTypes_DayTypeId",
                        column: x => x.DayTypeId,
                        principalSchema: "dbo",
                        principalTable: "DayTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Holidays_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Holidays_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkingCalendars",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DayTypeId = table.Column<int>(type: "int", nullable: false),
                    HolidayId = table.Column<int>(type: "int", nullable: true),
                    IsWeekend = table.Column<bool>(type: "bit", nullable: false),
                    IsWorkingDay = table.Column<bool>(type: "bit", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingCalendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkingCalendars_DayTypes_DayTypeId",
                        column: x => x.DayTypeId,
                        principalSchema: "dbo",
                        principalTable: "DayTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkingCalendars_Holidays_HolidayId",
                        column: x => x.HolidayId,
                        principalSchema: "dbo",
                        principalTable: "Holidays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkingCalendars_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkingCalendars_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "dbo",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_CreatedById",
                schema: "dbo",
                table: "Holidays",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_Date",
                schema: "dbo",
                table: "Holidays",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_Date_IsActive",
                schema: "dbo",
                table: "Holidays",
                columns: new[] { "Date", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_DayTypeId",
                schema: "dbo",
                table: "Holidays",
                column: "DayTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_UpdatedById",
                schema: "dbo",
                table: "Holidays",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingCalendars_CreatedById",
                schema: "dbo",
                table: "WorkingCalendars",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingCalendars_Date",
                schema: "dbo",
                table: "WorkingCalendars",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkingCalendars_Date_DayTypeId",
                schema: "dbo",
                table: "WorkingCalendars",
                columns: new[] { "Date", "DayTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkingCalendars_DayTypeId",
                schema: "dbo",
                table: "WorkingCalendars",
                column: "DayTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingCalendars_HolidayId",
                schema: "dbo",
                table: "WorkingCalendars",
                column: "HolidayId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingCalendars_UpdatedById",
                schema: "dbo",
                table: "WorkingCalendars",
                column: "UpdatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkingCalendars",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Holidays",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "IsBreakPaid",
                schema: "dbo",
                table: "ShiftTypes");

            migrationBuilder.DropColumn(
                name: "MinimumOvertimeMinutes",
                schema: "dbo",
                table: "PayrollConfigurations");

            migrationBuilder.DropColumn(
                name: "NightDifferentialEndTime",
                schema: "dbo",
                table: "PayrollConfigurations");

            migrationBuilder.DropColumn(
                name: "NightDifferentialStartTime",
                schema: "dbo",
                table: "PayrollConfigurations");

            migrationBuilder.DropColumn(
                name: "OvertimeRoundingRule",
                schema: "dbo",
                table: "PayrollConfigurations");

            migrationBuilder.DropColumn(
                name: "RequireOvertimeApproval",
                schema: "dbo",
                table: "PayrollConfigurations");
        }
    }
}
