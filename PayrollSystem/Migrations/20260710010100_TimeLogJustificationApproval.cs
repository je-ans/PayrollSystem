using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayrollSystem.Migrations
{
    /// <inheritdoc />
    public partial class TimeLogJustificationApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalRequestId",
                schema: "dbo",
                table: "TimeLogJustifications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeLogJustifications_ApprovalRequestId",
                schema: "dbo",
                table: "TimeLogJustifications",
                column: "ApprovalRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_TimeLogJustifications_ApprovalRequests_ApprovalRequestId",
                schema: "dbo",
                table: "TimeLogJustifications",
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
                name: "FK_TimeLogJustifications_ApprovalRequests_ApprovalRequestId",
                schema: "dbo",
                table: "TimeLogJustifications");

            migrationBuilder.DropIndex(
                name: "IX_TimeLogJustifications_ApprovalRequestId",
                schema: "dbo",
                table: "TimeLogJustifications");

            migrationBuilder.DropColumn(
                name: "ApprovalRequestId",
                schema: "dbo",
                table: "TimeLogJustifications");
        }
    }
}
