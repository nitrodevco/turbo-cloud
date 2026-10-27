using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "approvals",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        kind = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        plugin_key = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        summary = table
                            .Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        arguments = table
                            .Column<string>(type: "text", maxLength: 16000, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        requester_kind = table
                            .Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        requester_player_id = table.Column<int>(type: "int", nullable: true),
                        requester_name = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        node = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        state = table
                            .Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        decided_by_player_id = table.Column<int>(type: "int", nullable: true),
                        decided_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        note = table
                            .Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        executed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        result = table
                            .Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_approvals", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_approvals_kind",
                table: "approvals",
                column: "kind"
            );

            migrationBuilder.CreateIndex(
                name: "IX_approvals_state_expires_at",
                table: "approvals",
                columns: new[] { "state", "expires_at" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "approvals");
        }
    }
}
