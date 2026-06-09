using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TicketingSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "EmailVerificationOTP", "FirstName", "InvitationAcceptedAt", "InvitationExpiresAt", "InvitationToken", "InvitedByUserId", "IsActive", "IsEmailVerified", "LastName", "OTPExpiresAt", "PasswordHash", "PasswordResetExpiresAt", "PasswordResetToken", "PhoneNumber", "Role", "Status", "TeamLeadUserId", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("550e8400-e29b-41d4-a716-446655440002"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "teamlead2@ticketing.com", "", "Mary", null, null, null, new Guid("550e8400-e29b-41d4-a716-446655440000"), true, true, "Wanjiku", null, "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.", null, null, "0722000002", 2, 2, null, new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("550e8400-e29b-41d4-a716-446655440003"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "teamlead3@ticketing.com", "", "Peter", null, null, null, new Guid("550e8400-e29b-41d4-a716-446655440000"), true, true, "Omondi", null, "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.", null, null, "0722000003", 2, 2, null, new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("550e8400-e29b-41d4-a716-446655440011"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "technician2@ticketing.com", "", "Alice", null, null, null, new Guid("550e8400-e29b-41d4-a716-446655440001"), true, true, "Mwangi", null, "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.", null, null, "0722000011", 4, 2, new Guid("550e8400-e29b-41d4-a716-446655440001"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("550e8400-e29b-41d4-a716-446655440012"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "technician3@ticketing.com", "", "James", null, null, null, new Guid("550e8400-e29b-41d4-a716-446655440001"), true, true, "Kariuki", null, "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.", null, null, "0722000012", 4, 2, new Guid("550e8400-e29b-41d4-a716-446655440001"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("550e8400-e29b-41d4-a716-446655440013"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "technician4@ticketing.com", "", "Susan", null, null, null, new Guid("550e8400-e29b-41d4-a716-446655440002"), true, true, "Njeri", null, "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.", null, null, "0722000013", 4, 2, new Guid("550e8400-e29b-41d4-a716-446655440002"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("550e8400-e29b-41d4-a716-446655440014"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "technician5@ticketing.com", "", "David", null, null, null, new Guid("550e8400-e29b-41d4-a716-446655440002"), true, true, "Otieno", null, "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.", null, null, "0722000014", 4, 2, new Guid("550e8400-e29b-41d4-a716-446655440002"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("550e8400-e29b-41d4-a716-446655440015"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc), "technician6@ticketing.com", "", "Grace", null, null, null, new Guid("550e8400-e29b-41d4-a716-446655440003"), true, true, "Achieng", null, "$2a$12$Sr60RhXPVBGoDobfKme1gu52ga9V97PKJ9VHdXEisTdnM271HNjj.", null, null, "0722000015", 4, 2, new Guid("550e8400-e29b-41d4-a716-446655440003"), new DateTime(2025, 3, 16, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_EventKey",
                table: "NotificationLogs",
                column: "EventKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationLogs");

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440011"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440012"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440013"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440014"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440015"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440002"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440003"));
        }
    }
}
