using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Grants the nodes the audit of <c>docs/permissions.md</c> §17 added. <c>default</c> gets
    /// <c>chat.speak</c>, which a hotel mute denies: without it every player would be muted the
    /// moment something gates on it. <c>moderator</c> gets the group, gift and group-furni nodes;
    /// the new <c>room.*</c> nodes already reach it through its wildcard, and <c>admin</c> holds
    /// <c>*</c>. <c>INSERT IGNORE</c>, like <c>SeedPermissions</c>: an operator's row stays.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20260928060000_SeedAuditPermissionNodes")]
    public partial class SeedAuditPermissionNodes : Migration
    {
        private const string NODES =
            "INSERT IGNORE INTO permission_group_nodes (group_id, node, value, is_temporary) SELECT g.id, s.node, 1, 0 FROM (SELECT 'default' AS grp, 'chat.speak' AS node UNION ALL SELECT 'moderator', 'guild.delete_any' UNION ALL SELECT 'moderator', 'catalog.gift.hide_sender' UNION ALL SELECT 'moderator', 'catalog.guild.any_group') s JOIN permission_groups g ON g.name = s.grp;";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(NODES);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Seeded rows are indistinguishable from operator rows once edited; they stay.
        }
    }
}
