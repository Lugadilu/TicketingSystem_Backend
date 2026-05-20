using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TicketingSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedAll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "EmailVerificationOTP", "FirstName", "InvitationAcceptedAt", "InvitationExpiresAt", "InvitationToken", "InvitedByUserId", "IsActive", "IsEmailVerified", "LastName", "OTPExpiresAt", "PasswordHash", "PasswordResetExpiresAt", "PasswordResetToken", "PhoneNumber", "Role", "Status", "TeamLeadUserId", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("550e8400-e29b-41d4-a716-446655440001"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "teamlead1@ticketing.com", "", "John", null, null, null, new Guid("550e8400-e29b-41d4-a716-446655440000"), true, true, "Manager", null, "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.", null, null, "0722000001", 2, 2, null, new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("550e8400-e29b-41d4-a716-446655440020"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "customer1@ticketing.com", "", "Frank", null, null, null, null, true, true, "Client", null, "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.", null, null, "0722000020", 6, 2, null, new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("550e8400-e29b-41d4-a716-446655440030"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "support@ticketing.com", "", "Burton", null, null, null, null, true, true, "Support", null, "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.", null, null, "0722000030", 3, 2, null, new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("550e8400-e29b-41d4-a716-446655440010"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "technician1@ticketing.com", "", "Caleb", null, null, null, new Guid("550e8400-e29b-41d4-a716-446655440001"), true, true, "Tech", null, "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.", null, null, "0722000010", 4, 2, new Guid("550e8400-e29b-41d4-a716-446655440001"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440010"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440020"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440030"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440001"));
        }
    }
}
