using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ZonePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "fixed_price_max_km",
                table: "zones",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pricing",
                table: "zones",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "rates",
                table: "zones",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fixed_price_max_km",
                table: "zones");

            migrationBuilder.DropColumn(
                name: "pricing",
                table: "zones");

            migrationBuilder.DropColumn(
                name: "rates",
                table: "zones");
        }
    }
}
