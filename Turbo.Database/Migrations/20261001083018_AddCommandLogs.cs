using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Adds <c>command_logs</c>, and has the staff ladder's first rung log its chat commands: a
    /// use by anyone holding <c>command.log</c> is written down, so <c>trial_moderator</c> and
    /// everything that inherits it (<c>admin</c> holds <c>*</c>) are logged and nobody else is.
    /// <c>INSERT IGNORE</c> against the unique index, as the permission seed does: a hotel that
    /// already gave the group the node, or denied it, keeps what it chose.
    /// </summary>
    public partial class AddCommandLogs : Migration
    {
        private const string LOG_STAFF_COMMANDS =
            "INSERT IGNORE INTO permission_group_nodes (group_id, node, value, is_temporary) "
            + "SELECT g.id, 'command.log', 1, 0 FROM permission_groups g WHERE g.name = 'trial_moderator';";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "command_logs",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        room_id = table.Column<int>(type: "int", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        command = table
                            .Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        arguments = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        outcome = table
                            .Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        updated_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                        deleted_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: true)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_command_logs", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(LOG_STAFF_COMMANDS);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "command_logs");
        }
    }
}
