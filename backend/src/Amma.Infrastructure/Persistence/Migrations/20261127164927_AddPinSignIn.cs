using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPinSignIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
    }
}
