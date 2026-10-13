using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCfhIssueQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "picker_id",
                table: "cfh_reports",
                type: "int",
                nullable: true
            );

            migrationBuilder.AddColumn<byte>(
                name: "close_reason",
                table: "cfh_reports",
                type: "tinyint unsigned",
                nullable: true
            );

            migrationBuilder
                .CreateTable(
                    name: "moderator_tool_preferences",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        window_x = table.Column<int>(type: "int", nullable: false),
                        window_y = table.Column<int>(type: "int", nullable: false),
                        window_width = table.Column<int>(type: "int", nullable: false),
                        window_height = table.Column<int>(type: "int", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_moderator_tool_preferences", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_moderator_tool_preferences_player_id",
                table: "moderator_tool_preferences",
                column: "player_id",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "moderator_tool_preferences");

            migrationBuilder.DropColumn(name: "close_reason", table: "cfh_reports");

            migrationBuilder.DropColumn(name: "picker_id", table: "cfh_reports");
        }
    }
}
