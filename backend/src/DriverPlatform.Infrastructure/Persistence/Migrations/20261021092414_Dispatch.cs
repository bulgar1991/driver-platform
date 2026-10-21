using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Dispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_assignments_vehicle_id",
                table: "assignments");

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "vehicles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "drivers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "internal_notes",
                table: "bookings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                table: "assignments",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelled_at",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ends_at",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateOnly>(
                name: "shift_date",
                table: "assignments",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "starts_at",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "ix_assignments_shift_date",
                table: "assignments",
                column: "shift_date");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_vehicle_id_starts_at",
                table: "assignments",
                columns: new[] { "vehicle_id", "starts_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_assignments_shift_date",
                table: "assignments");

            migrationBuilder.DropIndex(
                name: "ix_assignments_vehicle_id_starts_at",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "internal_notes",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "ends_at",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "shift_date",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "starts_at",
                table: "assignments");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_vehicle_id",
                table: "assignments",
                column: "vehicle_id");
        }
    }
}
