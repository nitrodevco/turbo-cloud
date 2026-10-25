using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledTaskRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "scheduled_task_runs",
                    columns: table => new
                    {
                        task_key = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        scheduled_for = table.Column<DateTime>(
                            type: "datetime(6)",
                            nullable: false
                        ),
                        started_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        duration_ms = table.Column<long>(type: "bigint", nullable: false),
                        outcome = table
                            .Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        error = table
                            .Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        manual = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_scheduled_task_runs", x => x.task_key);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "scheduled_task_runs");
        }
    }
}
