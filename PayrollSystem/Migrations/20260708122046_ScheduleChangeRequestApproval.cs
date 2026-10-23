using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayrollSystem.Migrations
{
    /// <inheritdoc />
    public partial class ScheduleChangeRequestApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalRequestId",
                schema: "dbo",
                table: "ScheduleChangeRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleChangeRequests_ApprovalRequestId",
                schema: "dbo",
                table: "ScheduleChangeRequests",
                column: "ApprovalRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduleChangeRequests_ApprovalRequests_ApprovalRequestId",
                schema: "dbo",
                table: "ScheduleChangeRequests",
                column: "ApprovalRequestId",
                principalSchema: "dbo",
                principalTable: "ApprovalRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduleChangeRequests_ApprovalRequests_ApprovalRequestId",
                schema: "dbo",
                table: "ScheduleChangeRequests");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleChangeRequests_ApprovalRequestId",
                schema: "dbo",
                table: "ScheduleChangeRequests");

            migrationBuilder.DropColumn(
                name: "ApprovalRequestId",
                schema: "dbo",
                table: "ScheduleChangeRequests");
        }
    }
}
