using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Adds <c>player_sanctions</c> and gives the staff ladder the operator commands that suit each
    /// rung: warnings and look-ups for <c>trial_moderator</c>, sanctions for <c>moderator</c>,
    /// hotel-wide announcements for <c>senior_moderator</c>, the economy and status for
    /// <c>manager</c>. Every staff rung may stay in during a maintenance. <c>admin</c> already holds <c>*</c>, so it has the rest (<c>:group</c>,
    /// <c>:perm</c>, <c>:maintenance</c>, <c>:shutdown</c>, <c>:reload</c>). <c>INSERT IGNORE</c>
    /// against the unique index, as the other seeds: a hotel that already gave a group the node,
    /// or denied it, keeps what it chose.
    /// </summary>
    public partial class AddPlayerSanctions : Migration
    {
        private const string GRANT_OPERATOR_COMMANDS =
            "INSERT IGNORE INTO permission_group_nodes (group_id, node, value, is_temporary) "
            + "SELECT g.id, n.node, 1, 0 FROM permission_groups g JOIN ("
            + " SELECT 'trial_moderator' AS grp, 'command.warn' AS node"
            + " UNION ALL SELECT 'trial_moderator', 'command.alert'"
            + " UNION ALL SELECT 'trial_moderator', 'command.roomalert'"
            + " UNION ALL SELECT 'trial_moderator', 'command.whois'"
            + " UNION ALL SELECT 'trial_moderator', 'command.follow'"
            + " UNION ALL SELECT 'trial_moderator', 'command.online'"
            + " UNION ALL SELECT 'trial_moderator', 'hotel.maintenance.bypass'"
            + " UNION ALL SELECT 'moderator', 'command.silence'"
            + " UNION ALL SELECT 'moderator', 'command.tradelock'"
            + " UNION ALL SELECT 'moderator', 'command.disconnect'"
            + " UNION ALL SELECT 'moderator', 'command.ban'"
            + " UNION ALL SELECT 'moderator', 'command.unban'"
            + " UNION ALL SELECT 'moderator', 'command.summon'"
            + " UNION ALL SELECT 'moderator', 'command.roomkickall'"
            + " UNION ALL SELECT 'moderator', 'command.roommute'"
            + " UNION ALL SELECT 'moderator', 'command.roomunmute'"
            + " UNION ALL SELECT 'moderator', 'command.online.list'"
            + " UNION ALL SELECT 'senior_moderator', 'command.alert.mass'"
            + " UNION ALL SELECT 'senior_moderator', 'command.hotelalert'"
            + " UNION ALL SELECT 'senior_moderator', 'command.eventalert'"
            + " UNION ALL SELECT 'senior_moderator', 'command.unloadroom'"
            + " UNION ALL SELECT 'senior_moderator', 'command.givebadge'"
            + " UNION ALL SELECT 'senior_moderator', 'command.takebadge'"
            + " UNION ALL SELECT 'manager', 'command.give'"
            + " UNION ALL SELECT 'manager', 'command.give.mass'"
            + " UNION ALL SELECT 'manager', 'command.giveitem'"
            + " UNION ALL SELECT 'manager', 'command.status'"
            + ") n ON n.grp = g.name;";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "player_sanctions",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        kind = table.Column<int>(type: "int", nullable: false),
                        reason = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        issuer_id = table.Column<int>(type: "int", nullable: true),
                        expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        revoked_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        revoked_by = table.Column<int>(type: "int", nullable: true),
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
                        table.PrimaryKey("PK_player_sanctions", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_player_sanctions_player_id_kind",
                table: "player_sanctions",
                columns: new[] { "player_id", "kind" }
            );

            migrationBuilder.Sql(GRANT_OPERATOR_COMMANDS);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "player_sanctions");
        }
    }
}
