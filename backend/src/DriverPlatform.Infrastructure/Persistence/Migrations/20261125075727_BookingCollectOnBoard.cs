using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BookingCollectOnBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "collect_on_board",
                table: "bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "collect_on_board",
                table: "bookings");
        }
    }
}
