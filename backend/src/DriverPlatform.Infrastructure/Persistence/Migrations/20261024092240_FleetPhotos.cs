using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FleetPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "photo_id",
                table: "vehicles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "photo_id",
                table: "drivers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_photo_id",
                table: "vehicles",
                column: "photo_id");

            migrationBuilder.CreateIndex(
                name: "ix_drivers_photo_id",
                table: "drivers",
                column: "photo_id");

            migrationBuilder.AddForeignKey(
                name: "fk_drivers_photos_photo_id",
                table: "drivers",
                column: "photo_id",
                principalTable: "photos",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_vehicles_photos_photo_id",
                table: "vehicles",
                column: "photo_id",
                principalTable: "photos",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_drivers_photos_photo_id",
                table: "drivers");

            migrationBuilder.DropForeignKey(
                name: "fk_vehicles_photos_photo_id",
                table: "vehicles");

            migrationBuilder.DropTable(
                name: "photos");

            migrationBuilder.DropIndex(
                name: "ix_vehicles_photo_id",
                table: "vehicles");

            migrationBuilder.DropIndex(
                name: "ix_drivers_photo_id",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "photo_id",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "photo_id",
                table: "drivers");
        }
    }
}
