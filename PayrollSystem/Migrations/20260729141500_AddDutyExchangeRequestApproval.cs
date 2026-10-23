using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayrollSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddDutyExchangeRequestApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalRequestId",
                schema: "dbo",
                table: "DutyExchangeRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNotes",
                schema: "dbo",
                table: "DutyExchangeRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedById",
                schema: "dbo",
                table: "DutyExchangeRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedOn",
                schema: "dbo",
                table: "DutyExchangeRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "dbo",
                table: "DutyExchangeRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_DutyExchangeRequests_ApprovalRequestId",
                schema: "dbo",
                table: "DutyExchangeRequests",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyExchangeRequests_ReviewedById",
                schema: "dbo",
                table: "DutyExchangeRequests",
                column: "ReviewedById");

            migrationBuilder.AddForeignKey(
                name: "FK_DutyExchangeRequests_ApprovalRequests_ApprovalRequestId",
                schema: "dbo",
                table: "DutyExchangeRequests",
                column: "ApprovalRequestId",
                principalSchema: "dbo",
                principalTable: "ApprovalRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DutyExchangeRequests_User_ReviewedById",
                schema: "dbo",
                table: "DutyExchangeRequests",
                column: "ReviewedById",
                principalSchema: "dbo",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DutyExchangeRequests_ApprovalRequests_ApprovalRequestId",
                schema: "dbo",
                table: "DutyExchangeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_DutyExchangeRequests_User_ReviewedById",
                schema: "dbo",
                table: "DutyExchangeRequests");

            migrationBuilder.DropIndex(
                name: "IX_DutyExchangeRequests_ApprovalRequestId",
                schema: "dbo",
                table: "DutyExchangeRequests");

            migrationBuilder.DropIndex(
                name: "IX_DutyExchangeRequests_ReviewedById",
                schema: "dbo",
                table: "DutyExchangeRequests");

            migrationBuilder.DropColumn(
                name: "ApprovalRequestId",
                schema: "dbo",
                table: "DutyExchangeRequests");

            migrationBuilder.DropColumn(
                name: "ReviewNotes",
                schema: "dbo",
                table: "DutyExchangeRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedById",
                schema: "dbo",
                table: "DutyExchangeRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedOn",
                schema: "dbo",
                table: "DutyExchangeRequests");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "dbo",
                table: "DutyExchangeRequests");
        }
    }
}
