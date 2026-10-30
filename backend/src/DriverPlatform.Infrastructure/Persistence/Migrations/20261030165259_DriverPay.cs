using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DriverPay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "driver_pay",
                table: "bookings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "driver_pay",
                table: "bookings");
        }
    }
}
