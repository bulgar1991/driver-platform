using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "must_change_password",
                table: "staff_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "password_changed_at",
                table: "staff_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "security_stamp",
                table: "staff_users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            // Existing accounts get a random stamp; tokens issued before this release must sign in again.
            migrationBuilder.Sql("UPDATE staff_users SET security_stamp = md5(random()::text || id::text) WHERE security_stamp = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "must_change_password",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "password_changed_at",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "security_stamp",
                table: "staff_users");
        }
    }
}
