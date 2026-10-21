using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Payments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pdf_path",
                table: "invoices");

            migrationBuilder.AlterColumn<string>(
                name: "provider_transaction_id",
                table: "payments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "provider",
                table: "payments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "method",
                table: "payments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<string>(
                name: "checkout_session_id",
                table: "payments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expires_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "buyer",
                table: "invoices",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "lines",
                table: "invoices",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "pdf",
                table: "invoices",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "refund_id",
                table: "invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seller",
                table: "invoices",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<int>(
                name: "sequence",
                table: "invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "year",
                table: "invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "refunds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    provider_refund_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    provider_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    staff_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refunds", x => x.id);
                    table.ForeignKey(
                        name: "fk_refunds_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refunds_staff_users_staff_user_id",
                        column: x => x.staff_user_id,
                        principalTable: "staff_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payments_checkout_session_id",
                table: "payments",
                column: "checkout_session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_status_created_at",
                table: "payments",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_invoices_issued_at",
                table: "invoices",
                column: "issued_at");

            migrationBuilder.CreateIndex(
                name: "ix_invoices_refund_id",
                table: "invoices",
                column: "refund_id");

            migrationBuilder.CreateIndex(
                name: "ix_invoices_type_year_sequence",
                table: "invoices",
                columns: new[] { "type", "year", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refunds_payment_id",
                table: "refunds",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_refunds_staff_user_id",
                table: "refunds",
                column: "staff_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_invoices_refunds_refund_id",
                table: "invoices",
                column: "refund_id",
                principalTable: "refunds",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_invoices_refunds_refund_id",
                table: "invoices");

            migrationBuilder.DropTable(
                name: "refunds");

            migrationBuilder.DropIndex(
                name: "ix_payments_checkout_session_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_payments_status_created_at",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_invoices_issued_at",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "ix_invoices_refund_id",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "ix_invoices_type_year_sequence",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "checkout_session_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "expires_at",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "buyer",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "lines",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "pdf",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "refund_id",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "seller",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "sequence",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "year",
                table: "invoices");

            migrationBuilder.AlterColumn<string>(
                name: "provider_transaction_id",
                table: "payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "provider",
                table: "payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "method",
                table: "payments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pdf_path",
                table: "invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
