using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Settles the seeded ladder on how retro hotels run staff (put to Jev, TypeSafe):
    /// <list type="bullet">
    /// <item>A <c>moderator</c> moderates but does not control every room: an exact denial of
    /// <c>room.control.any</c> beats its own <c>room.*</c>. <c>senior_moderator</c> (60), between
    /// <c>moderator</c> and the hotel's managers, holds it.</item>
    /// <item><c>community</c> is renamed <c>manager</c>, what retro hotels call the rank, and
    /// inherits <c>senior_moderator</c> in place of <c>moderator</c>.</item>
    /// <item><c>helper</c> inherits <c>ambassador</c>: helpers do an ambassador's job too.</item>
    /// </list>
    /// See <c>docs/permissions.md</c> §13.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20260928190000_RefineStaffLadder")]
    public partial class RefineStaffLadder : Migration
    {
        private const string SENIOR_MODERATOR =
            "INSERT IGNORE INTO permission_groups (name, display_name, weight) VALUES ('senior_moderator', 'Senior Moderator', 60);";

        private const string RENAME_COMMUNITY =
            "UPDATE permission_groups SET name = 'manager', display_name = 'Manager' WHERE name = 'community' AND NOT EXISTS (SELECT 1 FROM (SELECT name FROM permission_groups) existing WHERE existing.name = 'manager');";

        private const string OLD_PARENTS =
            "DELETE l FROM permission_group_parents l JOIN permission_groups g ON g.id = l.group_id JOIN permission_groups p ON p.id = l.parent_group_id WHERE (g.name = 'manager' AND p.name = 'moderator') OR (g.name = 'helper' AND p.name = 'default');";

        private const string PARENTS =
            "INSERT IGNORE INTO permission_group_parents (group_id, parent_group_id) SELECT g.id, p.id FROM (SELECT 'senior_moderator' AS child, 'moderator' AS parent UNION ALL SELECT 'manager', 'senior_moderator' UNION ALL SELECT 'helper', 'ambassador') s JOIN permission_groups g ON g.name = s.child JOIN permission_groups p ON p.name = s.parent;";

        private const string NODES =
            "INSERT IGNORE INTO permission_group_nodes (group_id, node, value, is_temporary) SELECT g.id, s.node, s.value, 0 FROM (SELECT 'moderator' AS grp, 'room.control.any' AS node, 0 AS value UNION ALL SELECT 'senior_moderator', 'room.control.any', 1) s JOIN permission_groups g ON g.name = s.grp;";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SENIOR_MODERATOR);
            migrationBuilder.Sql(RENAME_COMMUNITY);
            migrationBuilder.Sql(OLD_PARENTS);
            migrationBuilder.Sql(PARENTS);
            migrationBuilder.Sql(NODES);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Seeded rows are indistinguishable from operator rows once edited; they stay.
        }
    }
}
