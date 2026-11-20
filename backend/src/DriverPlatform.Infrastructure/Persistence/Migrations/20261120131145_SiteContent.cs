using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SiteContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "site_contents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    it = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    de = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    fr = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    photo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    image_width = table.Column<int>(type: "integer", nullable: true),
                    image_height = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_contents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_site_contents_key",
                table: "site_contents",
                column: "key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "site_contents");
        }
    }
}
