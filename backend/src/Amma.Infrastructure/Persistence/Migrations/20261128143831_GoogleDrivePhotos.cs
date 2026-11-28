using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GoogleDrivePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DriveConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClientSecret = table.Column<byte[]>(type: "bytea", nullable: false),
                    RefreshToken = table.Column<byte[]>(type: "bytea", nullable: true),
                    FolderId = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Email = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriveConnections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriveConnections_UserId",
                table: "DriveConnections",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriveConnections");
        }
    }
}
