using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Gives <c>manager</c> <c>command.gift</c>, beside the <c>command.giveitem</c> it already
    /// holds: a present from the hotel is furni handed out, with a badge. <c>admin</c> holds
    /// <c>*</c>. <c>INSERT IGNORE</c> against the unique index, as the other seeds: a hotel that
    /// already gave the group the node, or denied it, keeps what it chose.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261009090000_GrantGiftCommand")]
    public partial class GrantGiftCommand : Migration
    {
        private const string GRANT =
            "INSERT IGNORE INTO permission_group_nodes (group_id, node, value, is_temporary) "
            + "SELECT g.id, 'command.gift', 1, 0 FROM permission_groups g WHERE g.name = 'manager';";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(GRANT);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A seeded row is indistinguishable from an operator's once edited; it stays.
        }
    }
}
