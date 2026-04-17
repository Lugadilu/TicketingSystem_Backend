using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdminSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "EmailVerificationOTP", "FirstName", "InvitationAcceptedAt", "InvitationExpiresAt", "InvitationToken", "InvitedByUserId", "IsActive", "IsEmailVerified", "LastName", "OTPExpiresAt", "PasswordHash", "PasswordResetExpiresAt", "PasswordResetToken", "PhoneNumber", "Role", "Status", "TeamLeadUserId", "UpdatedAt" },
                values: new object[] { new Guid("550e8400-e29b-41d4-a716-446655440000"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "admin@ticketing.com", "", "System", null, null, null, null, true, true, "Administrator", null, "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.", null, null, "0000000000", 1, 2, null, new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440000"));
        }
    }
}
