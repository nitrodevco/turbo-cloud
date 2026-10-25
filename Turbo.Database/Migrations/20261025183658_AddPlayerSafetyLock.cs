using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Adds <c>players.safety_locked</c>, Habbo's account safety lock, and gives
    /// <c>moderator</c> <c>:safetylock</c> / <c>:unsafetylock</c> as it has <c>:tradelock</c>.
    /// <c>INSERT IGNORE</c> as the other seeds: a hotel that already chose for the node keeps it.
    /// </summary>
    public partial class AddPlayerSafetyLock : Migration
    {
        private const string GRANT_SAFETYLOCK =
            "INSERT IGNORE INTO permission_group_nodes (group_id, node, value, is_temporary) "
            + "SELECT g.id, 'command.safetylock', 1, 0 FROM permission_groups g WHERE g.name = 'moderator';";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "safety_locked",
                table: "players",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.Sql(GRANT_SAFETYLOCK);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "safety_locked", table: "players");
        }
    }
}
