using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UsdAndExchangeRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "price_paid",
                table: "subscriptions",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "subscriptions",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "Usd");

            migrationBuilder.AddColumn<decimal>(
                name: "exchange_rate",
                table: "subscriptions",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "paid_amount",
                table: "subscriptions",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "opening_cash",
                table: "shifts",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "closing_cash",
                table: "shifts",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "private_training_monthly_fee",
                table: "settings",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<int>(
                name: "syp_rounding_step",
                table: "settings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<decimal>(
                name: "total",
                table: "sales",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "cash_received",
                table: "sales",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "sales",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "Usd");

            migrationBuilder.AddColumn<decimal>(
                name: "exchange_rate",
                table: "sales",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "paid_amount",
                table: "sales",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                table: "sale_lines",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "line_total",
                table: "sale_lines",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "price",
                table: "products",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "price",
                table: "plans",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            // Money is US dollars from now on. Payments made so far were in one currency: their paid amount is the price.
            migrationBuilder.Sql("UPDATE subscriptions SET paid_amount = price_paid;");
            migrationBuilder.Sql("UPDATE sales SET paid_amount = total;");

            // The default plans still at their old sample prices in pounds get the new sample prices in dollars.
            migrationBuilder.Sql(
                "UPDATE plans SET price = CASE months WHEN 1 THEN 20 WHEN 3 THEN 55 WHEN 6 THEN 100 ELSE 180 END " +
                "WHERE (months, price) IN ((1, 150000), (3, 400000), (6, 750000), (12, 1400000));");

            migrationBuilder.CreateTable(
                name: "exchange_rates",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    syp_per_usd = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    entered_by_id = table.Column<int>(type: "integer", nullable: false),
                    entered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exchange_rates", x => x.id);
                    table.CheckConstraint("ck_exchange_rates_rate", "syp_per_usd > 0");
                    table.ForeignKey(
                        name: "fk_exchange_rates_staff_entered_by_id",
                        column: x => x.entered_by_id,
                        principalTable: "staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "settings",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "private_training_monthly_fee", "syp_rounding_step" },
                values: new object[] { 25m, 1 });

            migrationBuilder.AddCheckConstraint(
                name: "ck_settings_syp_rounding_step",
                table: "settings",
                sql: "syp_rounding_step >= 1");

            migrationBuilder.CreateIndex(
                name: "ix_exchange_rates_date",
                table: "exchange_rates",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_exchange_rates_entered_at",
                table: "exchange_rates",
                column: "entered_at");

            migrationBuilder.CreateIndex(
                name: "ix_exchange_rates_entered_by_id",
                table: "exchange_rates",
                column: "entered_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exchange_rates");

            migrationBuilder.DropCheckConstraint(
                name: "ck_settings_syp_rounding_step",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "exchange_rate",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "paid_amount",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "syp_rounding_step",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "exchange_rate",
                table: "sales");

            migrationBuilder.DropColumn(
                name: "paid_amount",
                table: "sales");

            migrationBuilder.AlterColumn<long>(
                name: "price_paid",
                table: "subscriptions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,2)",
                oldPrecision: 14,
                oldScale: 2);

            migrationBuilder.AlterColumn<long>(
                name: "opening_cash",
                table: "shifts",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,2)",
                oldPrecision: 14,
                oldScale: 2);

            migrationBuilder.AlterColumn<long>(
                name: "closing_cash",
                table: "shifts",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,2)",
                oldPrecision: 14,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "private_training_monthly_fee",
                table: "settings",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,2)",
                oldPrecision: 14,
                oldScale: 2);

            migrationBuilder.AlterColumn<long>(
                name: "total",
                table: "sales",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,2)",
                oldPrecision: 14,
                oldScale: 2);

            migrationBuilder.AlterColumn<long>(
                name: "cash_received",
                table: "sales",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,2)",
                oldPrecision: 14,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "unit_price",
                table: "sale_lines",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,2)",
                oldPrecision: 14,
                oldScale: 2);

            migrationBuilder.AlterColumn<long>(
                name: "line_total",
                table: "sale_lines",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,2)",
                oldPrecision: 14,
                oldScale: 2);

            migrationBuilder.AlterColumn<long>(
                name: "price",
                table: "products",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,2)",
                oldPrecision: 14,
                oldScale: 2);

            migrationBuilder.AlterColumn<long>(
                name: "price",
                table: "plans",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,2)",
                oldPrecision: 14,
                oldScale: 2);

            migrationBuilder.UpdateData(
                table: "settings",
                keyColumn: "id",
                keyValue: 1,
                column: "private_training_monthly_fee",
                value: 100000L);
        }
    }
}
