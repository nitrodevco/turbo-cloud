using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Seeds the permission groups of <c>docs/permissions.md</c> §13 and carries
    /// <c>players.perk_flags</c> over into player nodes.
    /// <para>
    /// <c>default</c> grants what <c>SSOTicketMessageHandler</c> sends as allowed today, so nobody
    /// loses a perk when the projection replaces those literals. Everything is
    /// <c>INSERT IGNORE</c> against the unique indexes: a group or node an operator already made
    /// under the same name is left as it is.
    /// </para>
    /// <para>
    /// A set perk flag becomes a granted player node, but only for the flags <c>default</c> does
    /// not already grant; an unset flag becomes nothing, not a denial. The column was loaded and
    /// never read, so it is zero for nearly everyone, and reading zero as "denied" would take the
    /// camera and trading away from the whole hotel. <c>UnityTrade</c> has no node (no client
    /// reads it) and is dropped. The column itself stays until the projection no longer loads it.
    /// </para>
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20260928044000_SeedPermissions")]
    public partial class SeedPermissions : Migration
    {
        private const string GROUPS =
            "INSERT IGNORE INTO permission_groups (name, display_name, weight) VALUES ('default', 'Default', 0), ('ambassador', 'Ambassador', 20), ('helper', 'Helper', 30), ('moderator', 'Moderator', 50), ('admin', 'Administrator', 100);";

        private const string PARENTS =
            "INSERT IGNORE INTO permission_group_parents (group_id, parent_group_id) SELECT g.id, p.id FROM (SELECT 'ambassador' AS child, 'default' AS parent UNION ALL SELECT 'helper', 'default' UNION ALL SELECT 'moderator', 'helper' UNION ALL SELECT 'admin', 'moderator') s JOIN permission_groups g ON g.name = s.child JOIN permission_groups p ON p.name = s.parent;";

        private const string NODES =
            "INSERT IGNORE INTO permission_group_nodes (group_id, node, value) SELECT g.id, s.node, 1 FROM (SELECT 'default' AS grp, 'trade' AS node UNION ALL SELECT 'default', 'perk.camera' UNION ALL SELECT 'default', 'perk.citizen' UNION ALL SELECT 'default', 'perk.mouse_zoom' UNION ALL SELECT 'default', 'perk.navigator.thumbnail_camera' UNION ALL SELECT 'default', 'perk.navigator.phase_two' UNION ALL SELECT 'default', 'perk.call_on_helpers' UNION ALL SELECT 'default', 'perk.habbo_club_offer_beta' UNION ALL SELECT 'ambassador', 'role.ambassador' UNION ALL SELECT 'ambassador', 'chat.furni_chooser' UNION ALL SELECT 'helper', 'perk.guide_tool' UNION ALL SELECT 'helper', 'perk.judge_chat_reviews' UNION ALL SELECT 'moderator', 'room.*' UNION ALL SELECT 'moderator', 'moderation.tool' UNION ALL SELECT 'moderator', 'wired.menu' UNION ALL SELECT 'moderator', 'catalog.builders_club.without_membership' UNION ALL SELECT 'moderator', 'chat.style.staff' UNION ALL SELECT 'moderator', 'navigator.category.staff' UNION ALL SELECT 'admin', '*') s JOIN permission_groups g ON g.name = s.grp;";

        // PlayerPerkFlags: VoteInCompetitions = 1 << 1, JudgeChatReviews = 1 << 4,
        // UseGuideTool = 1 << 6, BuilderAtWork = 1 << 11, NavigatorPhaseOne2014 = 1 << 13.
        private const string PERK_FLAGS =
            "INSERT IGNORE INTO player_permission_nodes (player_id, node, value) SELECT p.id, s.node, 1 FROM players p JOIN (SELECT 2 AS flag, 'perk.vote_in_competitions' AS node UNION ALL SELECT 16, 'perk.judge_chat_reviews' UNION ALL SELECT 64, 'perk.guide_tool' UNION ALL SELECT 2048, 'room.floorplan.large' UNION ALL SELECT 8192, 'perk.navigator.phase_one') s ON (p.perk_flags & s.flag) <> 0;";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(GROUPS);
            migrationBuilder.Sql(PARENTS);
            migrationBuilder.Sql(NODES);
            migrationBuilder.Sql(PERK_FLAGS);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Seeded rows are indistinguishable from operator rows once edited; they stay, and
            // dropping the tables in AddPermissions' Down removes them anyway.
        }
    }
}
