using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffTwoFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "two_factor_email_code_expires_at",
                table: "staff_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "two_factor_email_code_hash",
                table: "staff_users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "two_factor_email_code_sent_at",
                table: "staff_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "two_factor_enabled_at",
                table: "staff_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "two_factor_last_step",
                table: "staff_users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "two_factor_pending_secret",
                table: "staff_users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "two_factor_recovery_codes",
                table: "staff_users",
                type: "text[]",
                nullable: false,
                // Existing accounts get an empty list (two-step sign-in off).
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "two_factor_secret",
                table: "staff_users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "two_factor_email_code_expires_at",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "two_factor_email_code_hash",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "two_factor_email_code_sent_at",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "two_factor_enabled_at",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "two_factor_last_step",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "two_factor_pending_secret",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "two_factor_recovery_codes",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "two_factor_secret",
                table: "staff_users");
        }
    }
}
