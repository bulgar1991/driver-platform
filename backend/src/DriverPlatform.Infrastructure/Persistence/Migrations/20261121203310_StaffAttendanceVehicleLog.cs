using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffAttendanceVehicleLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "automatic_gearbox",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "chassis_number",
                table: "vehicles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "vehicles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cost_per_km",
                table: "vehicles",
                type: "numeric(8,3)",
                precision: 8,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "default_driver_id",
                table: "vehicles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_dvd",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "has_wc",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "has_wifi",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "leasing_company",
                table: "vehicles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "registered_on",
                table: "vehicles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "wheelchair_ramp",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "drivers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_type",
                table: "drivers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Employee");

            migrationBuilder.AddColumn<DateOnly>(
                name: "date_of_birth",
                table: "drivers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "employee_number",
                table: "drivers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "exclude_from_attendance",
                table: "drivers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "hired_on",
                table: "drivers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "home_phone",
                table: "drivers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "hourly_cost",
                table: "drivers",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "job_function",
                table: "drivers",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "left_on",
                table: "drivers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "max_driving_hours",
                table: "drivers",
                type: "numeric(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notify_by",
                table: "drivers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "WhatsAppAndEmail");

            migrationBuilder.AddColumn<string>(
                name: "place_of_birth",
                table: "drivers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_code",
                table: "drivers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "attendance_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    makes_unavailable = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_expirations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    due_km = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicle_expirations", x => x.id);
                    table.ForeignKey(
                        name: "fk_vehicle_expirations_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_refuels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    odometer_km = table.Column<int>(type: "integer", nullable: false),
                    liters = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    station = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicle_refuels", x => x.id);
                    table.ForeignKey(
                        name: "fk_vehicle_refuels_drivers_driver_id",
                        column: x => x.driver_id,
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_vehicle_refuels_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attendance_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_attendance_entries_attendance_types_type_id",
                        column: x => x.type_id,
                        principalTable: "attendance_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_entries_drivers_driver_id",
                        column: x => x.driver_id,
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_default_driver_id",
                table: "vehicles",
                column: "default_driver_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_entries_date",
                table: "attendance_entries",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_entries_driver_id_date",
                table: "attendance_entries",
                columns: new[] { "driver_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendance_entries_type_id",
                table: "attendance_entries",
                column: "type_id");

            migrationBuilder.CreateIndex(
                name: "ix_vehicle_expirations_vehicle_id",
                table: "vehicle_expirations",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "ix_vehicle_refuels_driver_id",
                table: "vehicle_refuels",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "ix_vehicle_refuels_vehicle_id_date",
                table: "vehicle_refuels",
                columns: new[] { "vehicle_id", "date" });

            migrationBuilder.AddForeignKey(
                name: "fk_vehicles_drivers_default_driver_id",
                table: "vehicles",
                column: "default_driver_id",
                principalTable: "drivers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vehicles_drivers_default_driver_id",
                table: "vehicles");

            migrationBuilder.DropTable(
                name: "attendance_entries");

            migrationBuilder.DropTable(
                name: "vehicle_expirations");

            migrationBuilder.DropTable(
                name: "vehicle_refuels");

            migrationBuilder.DropTable(
                name: "attendance_types");

            migrationBuilder.DropIndex(
                name: "ix_vehicles_default_driver_id",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "automatic_gearbox",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "chassis_number",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "code",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "cost_per_km",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "default_driver_id",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "has_dvd",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "has_wc",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "has_wifi",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "leasing_company",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "registered_on",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "wheelchair_ramp",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "address",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "contract_type",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "date_of_birth",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "employee_number",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "exclude_from_attendance",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "hired_on",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "home_phone",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "hourly_cost",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "job_function",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "left_on",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "max_driving_hours",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "notify_by",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "place_of_birth",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "tax_code",
                table: "drivers");
        }
    }
}
