using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Majordomo.Execution.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "execution");

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "execution",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    epic = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    direction = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    size = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    stop_level = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    profit_level = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    deal_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    deal_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reject_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_decision_id",
                schema: "execution",
                table: "orders",
                column: "decision_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_job_id_created_at",
                schema: "execution",
                table: "orders",
                columns: new[] { "job_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_status",
                schema: "execution",
                table: "orders",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "orders",
                schema: "execution");
        }
    }
}
