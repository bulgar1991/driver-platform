using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HomePage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "publish_consent",
                table: "reviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "driver_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    last_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    city = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    licence = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    has_own_vehicle = table.Column<bool>(type: "boolean", nullable: false),
                    vehicle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    years_experience = table.Column<int>(type: "integer", nullable: false),
                    languages = table.Column<List<string>>(type: "text[]", nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    client_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_driver_applications", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_driver_applications_phone",
                table: "driver_applications",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "ix_driver_applications_status_created_at",
                table: "driver_applications",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "driver_applications");

            migrationBuilder.DropColumn(
                name: "publish_consent",
                table: "reviews");
        }
    }
}
