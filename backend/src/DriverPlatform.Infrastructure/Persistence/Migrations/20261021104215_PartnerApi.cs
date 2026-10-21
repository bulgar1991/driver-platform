using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DriverPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PartnerApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "partners",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "api_key_created_at",
                table: "partners",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "api_key_last_used_at",
                table: "partners",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "partners",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "notify_passengers",
                table: "partners",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "price_adjustment_percent",
                table: "partners",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "secret_version",
                table: "partners",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "webhook_url",
                table: "partners",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "partner_webhooks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    @event = table.Column<string>(name: "event", type: "character varying(64)", maxLength: 64, nullable: false),
                    payload = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partner_webhooks", x => x.id);
                    table.ForeignKey(
                        name: "fk_partner_webhooks_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_partners_api_key_prefix",
                table: "partners",
                column: "api_key_prefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_partner_webhooks_delivered_at_next_attempt_at",
                table: "partner_webhooks",
                columns: new[] { "delivered_at", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_partner_webhooks_partner_id",
                table: "partner_webhooks",
                column: "partner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "partner_webhooks");

            migrationBuilder.DropIndex(
                name: "ix_partners_api_key_prefix",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "api_key_created_at",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "api_key_last_used_at",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "notify_passengers",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "price_adjustment_percent",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "secret_version",
                table: "partners");

            migrationBuilder.DropColumn(
                name: "webhook_url",
                table: "partners");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "partners",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);
        }
    }
}
