using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandExecutionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AddColumn<string>(
                    name: "batch_target_results_json",
                    table: "command_logs",
                    type: "json",
                    maxLength: 512,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "confirmation_id",
                table: "command_logs",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "execution_id",
                table: "command_logs",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "parent_execution_id",
                table: "command_logs",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci"
            );

            migrationBuilder
                .AddColumn<string>(
                    name: "resolved_audience_json",
                    table: "command_logs",
                    type: "json",
                    maxLength: 512,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .AddColumn<string>(
                    name: "source",
                    table: "command_logs",
                    type: "varchar(16)",
                    maxLength: 16,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "batch_target_results_json", table: "command_logs");

            migrationBuilder.DropColumn(name: "confirmation_id", table: "command_logs");

            migrationBuilder.DropColumn(name: "execution_id", table: "command_logs");

            migrationBuilder.DropColumn(name: "parent_execution_id", table: "command_logs");

            migrationBuilder.DropColumn(name: "resolved_audience_json", table: "command_logs");

            migrationBuilder.DropColumn(name: "source", table: "command_logs");
        }
    }
}
