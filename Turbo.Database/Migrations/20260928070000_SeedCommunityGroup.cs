using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Puts the seeded groups on the security levels the real hotel uses, so every client —
    /// Flash, AIR, any Nitro — draws staff UI as its owners expect without knowing about nodes.
    /// <c>moderator</c> held <c>navigator.category.staff</c>, whose client level is 7, which put
    /// every moderator on 7 and showed them the staff-pick button among others. That node and
    /// <c>navigator.staff_pick</c> move to a new <c>community</c> group (weight 70) between
    /// <c>moderator</c> and <c>admin</c>, so the levels come out as ambassador 2, moderator 5,
    /// community 7, and <c>admin</c> is floored at 8 through <c>client.security_level</c>. The
    /// lower-level nodes a group's level makes the client offer anyway — no video offers at 1, the
    /// <c>:furni</c> chooser at 2 — are granted to the groups that reach them, so a seeded group
    /// is shown nothing the server refuses.
    /// <para>
    /// Only the seeded rows move: <c>INSERT IGNORE</c> leaves an operator's rows alone, and the two
    /// deletes take out exactly the moderator node and the admin-to-moderator link the first seed
    /// wrote.
    /// </para>
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20260928070000_SeedCommunityGroup")]
    public partial class SeedCommunityGroup : Migration
    {
        private const string GROUP =
            "INSERT IGNORE INTO permission_groups (name, display_name, weight) VALUES ('community', 'Community', 70);";

        private const string PARENTS =
            "INSERT IGNORE INTO permission_group_parents (group_id, parent_group_id) SELECT g.id, p.id FROM (SELECT 'community' AS child, 'moderator' AS parent UNION ALL SELECT 'admin', 'community') s JOIN permission_groups g ON g.name = s.child JOIN permission_groups p ON p.name = s.parent;";

        private const string NODES =
            "INSERT IGNORE INTO permission_group_nodes (group_id, node, value, is_temporary) SELECT g.id, s.node, 1, 0 FROM (SELECT 'community' AS grp, 'navigator.category.staff' AS node UNION ALL SELECT 'community', 'navigator.staff_pick' UNION ALL SELECT 'ambassador', 'perk.no_video_offers' UNION ALL SELECT 'moderator', 'perk.no_video_offers' UNION ALL SELECT 'moderator', 'chat.furni_chooser') s JOIN permission_groups g ON g.name = s.grp;";

        private const string ADMIN_LEVEL =
            "INSERT IGNORE INTO permission_group_meta (group_id, meta_key, value, is_temporary) SELECT g.id, 'client.security_level', '8', 0 FROM permission_groups g WHERE g.name = 'admin';";

        private const string MODERATOR_NODE =
            "DELETE n FROM permission_group_nodes n JOIN permission_groups g ON g.id = n.group_id WHERE g.name = 'moderator' AND n.node = 'navigator.category.staff' AND n.is_temporary = 0;";

        private const string ADMIN_PARENT =
            "DELETE l FROM permission_group_parents l JOIN permission_groups g ON g.id = l.group_id JOIN permission_groups p ON p.id = l.parent_group_id WHERE g.name = 'admin' AND p.name = 'moderator' AND EXISTS (SELECT 1 FROM permission_groups c WHERE c.name = 'community');";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(GROUP);
            migrationBuilder.Sql(PARENTS);
            migrationBuilder.Sql(NODES);
            migrationBuilder.Sql(ADMIN_LEVEL);
            migrationBuilder.Sql(MODERATOR_NODE);
            migrationBuilder.Sql(ADMIN_PARENT);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Seeded rows are indistinguishable from operator rows once edited; they stay.
        }
    }
}
