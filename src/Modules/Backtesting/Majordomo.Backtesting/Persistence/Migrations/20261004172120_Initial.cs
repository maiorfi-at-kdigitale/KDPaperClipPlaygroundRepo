using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Majordomo.Backtesting.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "backtesting");

            migrationBuilder.CreateTable(
                name: "backtest_runs",
                schema: "backtesting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parameter_set_version = table.Column<int>(type: "integer", nullable: false),
                    from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    slippage_points = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    result = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_backtest_runs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_backtest_runs_job_id",
                schema: "backtesting",
                table: "backtest_runs",
                column: "job_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backtest_runs",
                schema: "backtesting");
        }
    }
}
