using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Translations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "name_translations",
                table: "surcharge_rules",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "name_translations",
                table: "surcharge_rules");
        }
    }
}
