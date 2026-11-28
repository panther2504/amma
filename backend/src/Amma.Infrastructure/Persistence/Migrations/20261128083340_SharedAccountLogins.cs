using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedAccountLogins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {



            migrationBuilder.AddColumn<Guid>(
                name: "LoginIdentityId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LoginIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Destination = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    PinHash = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FailedPinAttempts = table.Column<int>(type: "integer", nullable: false),
                    PinLockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginIdentities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginIdentities_Destination",
                table: "LoginIdentities",
                column: "Destination",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoginIdentities_UserId",
                table: "LoginIdentities",
                column: "UserId");

            // Keep existing PINs: every user who already has a PIN gets a login for their phone/e-mail.
            migrationBuilder.Sql(@"
                INSERT INTO ""LoginIdentities"" (""Id"", ""UserId"", ""Destination"", ""PinHash"", ""FailedPinAttempts"", ""PinLockedUntil"", ""LastLoginAt"", ""CreatedAt"", ""UpdatedAt"")
                SELECT gen_random_uuid(), u.""Id"", COALESCE(u.""Phone"", u.""Email""), u.""PinHash"", u.""FailedPinAttempts"", u.""PinLockedUntil"", u.""LastLoginAt"", now(), now()
                FROM ""Users"" u
                WHERE u.""PinHash"" IS NOT NULL AND COALESCE(u.""Phone"", u.""Email"") IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "FailedPinAttempts",
                table: "Users");
            migrationBuilder.DropColumn(
                name: "PinHash",
                table: "Users");
            migrationBuilder.DropColumn(
                name: "PinLockedUntil",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoginIdentities");

            migrationBuilder.DropColumn(
                name: "LoginIdentityId",
                table: "RefreshTokens");

            migrationBuilder.AddColumn<int>(
                name: "FailedPinAttempts",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PinHash",
                table: "Users",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PinLockedUntil",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
