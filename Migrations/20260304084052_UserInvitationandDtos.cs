using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class UserInvitationandDtos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InvitationAcceptedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvitationExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitationToken",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvitedByUserId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamLeadUserId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_InvitedByUserId",
                table: "Users",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TeamLeadUserId",
                table: "Users",
                column: "TeamLeadUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_InvitedByUserId",
                table: "Users",
                column: "InvitedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_TeamLeadUserId",
                table: "Users",
                column: "TeamLeadUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_InvitedByUserId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_TeamLeadUserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_InvitedByUserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TeamLeadUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "InvitationAcceptedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "InvitationExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "InvitationToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "InvitedByUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TeamLeadUserId",
                table: "Users");
        }
    }
}
