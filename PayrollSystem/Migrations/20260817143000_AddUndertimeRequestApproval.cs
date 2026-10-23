using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayrollSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddUndertimeRequestApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalRequestId",
                schema: "dbo",
                table: "UndertimeRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComputedUndertimeMinutesAtSubmit",
                schema: "dbo",
                table: "UndertimeRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNotes",
                schema: "dbo",
                table: "UndertimeRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedById",
                schema: "dbo",
                table: "UndertimeRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedOn",
                schema: "dbo",
                table: "UndertimeRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "dbo",
                table: "UndertimeRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE [dbo].[UndertimeRequests]
                SET [Status] = 1
                WHERE [Status] = 0
                """);

            migrationBuilder.CreateIndex(
                name: "IX_UndertimeRequests_ApprovalRequestId",
                schema: "dbo",
                table: "UndertimeRequests",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_UndertimeRequests_ReviewedById",
                schema: "dbo",
                table: "UndertimeRequests",
                column: "ReviewedById");

            migrationBuilder.AddForeignKey(
                name: "FK_UndertimeRequests_ApprovalRequests_ApprovalRequestId",
                schema: "dbo",
                table: "UndertimeRequests",
                column: "ApprovalRequestId",
                principalSchema: "dbo",
                principalTable: "ApprovalRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UndertimeRequests_User_ReviewedById",
                schema: "dbo",
                table: "UndertimeRequests",
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
                name: "FK_UndertimeRequests_ApprovalRequests_ApprovalRequestId",
                schema: "dbo",
                table: "UndertimeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_UndertimeRequests_User_ReviewedById",
                schema: "dbo",
                table: "UndertimeRequests");

            migrationBuilder.DropIndex(
                name: "IX_UndertimeRequests_ApprovalRequestId",
                schema: "dbo",
                table: "UndertimeRequests");

            migrationBuilder.DropIndex(
                name: "IX_UndertimeRequests_ReviewedById",
                schema: "dbo",
                table: "UndertimeRequests");

            migrationBuilder.DropColumn(
                name: "ApprovalRequestId",
                schema: "dbo",
                table: "UndertimeRequests");

            migrationBuilder.DropColumn(
                name: "ComputedUndertimeMinutesAtSubmit",
                schema: "dbo",
                table: "UndertimeRequests");

            migrationBuilder.DropColumn(
                name: "ReviewNotes",
                schema: "dbo",
                table: "UndertimeRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedById",
                schema: "dbo",
                table: "UndertimeRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedOn",
                schema: "dbo",
                table: "UndertimeRequests");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "dbo",
                table: "UndertimeRequests");
        }
    }
}
