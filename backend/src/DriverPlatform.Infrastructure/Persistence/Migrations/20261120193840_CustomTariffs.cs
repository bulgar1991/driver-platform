using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomTariffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "vehicle_rates",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "name_de",
                table: "vehicle_rates",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_en",
                table: "vehicle_rates",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_fr",
                table: "vehicle_rates",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_it",
                table: "vehicle_rates",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "vehicle_rates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // The tariffs that already exist keep their usual order: Standard, Business, VIP, Bus.
            migrationBuilder.Sql("""
                UPDATE vehicle_rates SET sort_order = CASE category
                    WHEN 'Sedan' THEN 0 WHEN 'Business' THEN 1 WHEN 'Estate' THEN 2 WHEN 'Van' THEN 3 ELSE 10 END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_active",
                table: "vehicle_rates");

            migrationBuilder.DropColumn(
                name: "name_de",
                table: "vehicle_rates");

            migrationBuilder.DropColumn(
                name: "name_en",
                table: "vehicle_rates");

            migrationBuilder.DropColumn(
                name: "name_fr",
                table: "vehicle_rates");

            migrationBuilder.DropColumn(
                name: "name_it",
                table: "vehicle_rates");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "vehicle_rates");
        }
    }
}
