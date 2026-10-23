using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayrollSystem.Migrations
{
    /// <inheritdoc />
    public partial class OvertimeRequestLunchBreak : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OvertimeRequests_EmployeeId_WorkDate_Status",
                schema: "dbo",
                table: "OvertimeRequests");

            migrationBuilder.RenameColumn(
                name: "WorkDate",
                schema: "dbo",
                table: "OvertimeRequests",
                newName: "StartDate");

            migrationBuilder.AddColumn<int>(
                name: "ApprovedBreakMinutes",
                schema: "dbo",
                table: "OvertimeRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ApprovedHasLunchBreak",
                schema: "dbo",
                table: "OvertimeRequests",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ApprovedLunchEnd",
                schema: "dbo",
                table: "OvertimeRequests",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ApprovedLunchStart",
                schema: "dbo",
                table: "OvertimeRequests",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BreakMinutes",
                schema: "dbo",
                table: "OvertimeRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                schema: "dbo",
                table: "OvertimeRequests",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<bool>(
                name: "HasLunchBreak",
                schema: "dbo",
                table: "OvertimeRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "LunchEnd",
                schema: "dbo",
                table: "OvertimeRequests",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "LunchStart",
                schema: "dbo",
                table: "OvertimeRequests",
                type: "time",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_EmployeeId_StartDate_EndDate_Status",
                schema: "dbo",
                table: "OvertimeRequests",
                columns: new[] { "EmployeeId", "StartDate", "EndDate", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OvertimeRequests_EmployeeId_StartDate_EndDate_Status",
                schema: "dbo",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "ApprovedBreakMinutes",
                schema: "dbo",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "ApprovedHasLunchBreak",
                schema: "dbo",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "ApprovedLunchEnd",
                schema: "dbo",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "ApprovedLunchStart",
                schema: "dbo",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "BreakMinutes",
                schema: "dbo",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "EndDate",
                schema: "dbo",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "HasLunchBreak",
                schema: "dbo",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "LunchEnd",
                schema: "dbo",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "LunchStart",
                schema: "dbo",
                table: "OvertimeRequests");

            migrationBuilder.RenameColumn(
                name: "StartDate",
                schema: "dbo",
                table: "OvertimeRequests",
                newName: "WorkDate");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeRequests_EmployeeId_WorkDate_Status",
                schema: "dbo",
                table: "OvertimeRequests",
                columns: new[] { "EmployeeId", "WorkDate", "Status" });
        }
    }
}
