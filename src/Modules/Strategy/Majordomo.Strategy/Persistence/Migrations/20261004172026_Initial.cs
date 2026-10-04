using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Majordomo.Strategy.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "strategy");

            migrationBuilder.CreateTable(
                name: "decisions",
                schema: "strategy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parameter_set_version = table.Column<int>(type: "integer", nullable: false),
                    epic = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    bar_close = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    forecast_fingerprint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    strength = table.Column<double>(type: "double precision", nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decisions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trading_jobs",
                schema: "strategy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status_before_pause = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    halt_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    parameter_set_version = table.Column<int>(type: "integer", nullable: false),
                    parameters = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trading_jobs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_decisions_job_id_sequence",
                schema: "strategy",
                table: "decisions",
                columns: new[] { "job_id", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_decisions_sequence",
                schema: "strategy",
                table: "decisions",
                column: "sequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trading_jobs_status",
                schema: "strategy",
                table: "trading_jobs",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "decisions",
                schema: "strategy");

            migrationBuilder.DropTable(
                name: "trading_jobs",
                schema: "strategy");
        }
    }
}
