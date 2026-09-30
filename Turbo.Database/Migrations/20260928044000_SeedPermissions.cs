using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Seeds the permission groups of <c>docs/permissions.md</c> §13 and carries
    /// <c>players.perk_flags</c> over into player nodes. Everything is <c>INSERT IGNORE</c> against
    /// the unique indexes: a group or row an operator already made under the same name is left as
    /// it is.
    /// <list type="bullet">
    /// <item><c>default</c> grants what <c>SSOTicketMessageHandler</c> used to send as allowed, so
    /// nobody loses a perk when the projection replaces those literals, plus <c>trade</c> and
    /// <c>chat.speak</c>, which a hotel mute denies.</item>
    /// <item>The staff ladder sits on the security levels the real hotel uses, so every client —
    /// Flash, AIR, any Nitro — draws staff UI as its owners expect without knowing about nodes:
    /// ambassador and helper 2, moderators 5, <c>manager</c> 7, and <c>admin</c> floored at 8 through
    /// <c>client.security_level</c>. Each group on the ladder holds the lower-level nodes its level
    /// makes the client offer anyway (no video offers at 1, the <c>:furni</c> chooser at 2).</item>
    /// <item>A <c>moderator</c> moderates but does not control every room: an exact denial of
    /// <c>room.control.any</c> beats its own <c>room.*</c>, and <c>senior_moderator</c> holds
    /// it.</item>
    /// <item><c>moderator</c> and <c>admin</c> are denied <c>room.furni.steal</c>, which their
    /// wildcards would grant: every furni either picked up would go into their own inventory
    /// instead of back to its owner. Each needs its own row, because <c>admin</c>'s <c>*</c> is
    /// weighed before anything it inherits. <c>admin</c> is also denied
    /// <c>perk.navigator.phase_one</c>, or the client would run the phase-one navigator paths for
    /// administrators only.</item>
    /// <item><c>vip</c>, <c>builder</c>, <c>events</c> and <c>trial_moderator</c> are the
    /// specialist ranks most retro hotels staff. They hold only their job.</item>
    /// </list>
    /// <para>
    /// A set perk flag becomes a granted player node, but only for the flags <c>default</c> does
    /// not already grant; an unset flag becomes nothing, not a denial. The column was loaded and
    /// never read, so it is zero for nearly everyone, and reading zero as "denied" would take the
    /// camera and trading away from the whole hotel. <c>UnityTrade</c> has no node (no client
    /// reads it) and is dropped.
    /// </para>
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20260928044000_SeedPermissions")]
    public partial class SeedPermissions : Migration
    {
        private const string GROUPS =
            "INSERT IGNORE INTO permission_groups (name, display_name, weight) VALUES"
            + " ('default', 'Default', 0),"
            + " ('vip', 'VIP', 10),"
            + " ('ambassador', 'Ambassador', 20),"
            + " ('helper', 'Helper', 30),"
            + " ('builder', 'Builder', 35),"
            + " ('events', 'Event Staff', 40),"
            + " ('trial_moderator', 'Trial Moderator', 45),"
            + " ('moderator', 'Moderator', 50),"
            + " ('senior_moderator', 'Senior Moderator', 60),"
            + " ('manager', 'Manager', 70),"
            + " ('admin', 'Administrator', 100);";

        private const string PARENTS =
            "INSERT IGNORE INTO permission_group_parents (group_id, parent_group_id) SELECT g.id, p.id FROM ("
            + "SELECT 'vip' AS child, 'default' AS parent"
            + " UNION ALL SELECT 'ambassador', 'default'"
            + " UNION ALL SELECT 'helper', 'ambassador'"
            + " UNION ALL SELECT 'builder', 'default'"
            + " UNION ALL SELECT 'events', 'default'"
            + " UNION ALL SELECT 'trial_moderator', 'helper'"
            + " UNION ALL SELECT 'moderator', 'trial_moderator'"
            + " UNION ALL SELECT 'senior_moderator', 'moderator'"
            + " UNION ALL SELECT 'manager', 'senior_moderator'"
            + " UNION ALL SELECT 'manager', 'builder'"
            + " UNION ALL SELECT 'manager', 'events'"
            + " UNION ALL SELECT 'admin', 'manager'"
            + ") s JOIN permission_groups g ON g.name = s.child JOIN permission_groups p ON p.name = s.parent;";

        private const string NODES =
            "INSERT IGNORE INTO permission_group_nodes (group_id, node, value, is_temporary) SELECT g.id, s.node, s.value, 0 FROM ("
            + "SELECT 'default' AS grp, 'trade' AS node, 1 AS value"
            + " UNION ALL SELECT 'default', 'chat.speak', 1"
            + " UNION ALL SELECT 'default', 'perk.camera', 1"
            + " UNION ALL SELECT 'default', 'perk.citizen', 1"
            + " UNION ALL SELECT 'default', 'perk.mouse_zoom', 1"
            + " UNION ALL SELECT 'default', 'perk.navigator.thumbnail_camera', 1"
            + " UNION ALL SELECT 'default', 'perk.navigator.phase_two', 1"
            + " UNION ALL SELECT 'default', 'perk.call_on_helpers', 1"
            + " UNION ALL SELECT 'default', 'perk.habbo_club_offer_beta', 1"
            + " UNION ALL SELECT 'vip', 'room.floorplan.large', 1"
            + " UNION ALL SELECT 'ambassador', 'role.ambassador', 1"
            + " UNION ALL SELECT 'ambassador', 'chat.furni_chooser', 1"
            + " UNION ALL SELECT 'ambassador', 'perk.no_video_offers', 1"
            + " UNION ALL SELECT 'helper', 'perk.guide_tool', 1"
            + " UNION ALL SELECT 'helper', 'perk.judge_chat_reviews', 1"
            + " UNION ALL SELECT 'helper', 'perk.vote_in_competitions', 1"
            + " UNION ALL SELECT 'builder', 'room.floorplan.save_without_club', 1"
            + " UNION ALL SELECT 'builder', 'room.floorplan.large', 1"
            + " UNION ALL SELECT 'builder', 'room.furni.branding', 1"
            + " UNION ALL SELECT 'builder', 'wired.menu', 1"
            + " UNION ALL SELECT 'builder', 'chat.furni_chooser', 1"
            + " UNION ALL SELECT 'events', 'room.event.edit_any', 1"
            + " UNION ALL SELECT 'events', 'room.enter.locked', 1"
            + " UNION ALL SELECT 'events', 'room.enter.full', 1"
            + " UNION ALL SELECT 'events', 'room.furni.youtube_any', 1"
            + " UNION ALL SELECT 'events', 'room.furni.vimeo_edit', 1"
            + " UNION ALL SELECT 'events', 'chat.furni_chooser', 1"
            + " UNION ALL SELECT 'trial_moderator', 'moderation.tool', 1"
            + " UNION ALL SELECT 'trial_moderator', 'room.moderate.any', 1"
            + " UNION ALL SELECT 'trial_moderator', 'room.enter.locked', 1"
            + " UNION ALL SELECT 'trial_moderator', 'room.enter.full', 1"
            + " UNION ALL SELECT 'trial_moderator', 'chat.style.staff', 1"
            + " UNION ALL SELECT 'trial_moderator', 'perk.no_video_offers', 1"
            + " UNION ALL SELECT 'moderator', 'room.*', 1"
            + " UNION ALL SELECT 'moderator', 'room.control.any', 0"
            + " UNION ALL SELECT 'moderator', 'room.furni.steal', 0"
            + " UNION ALL SELECT 'moderator', 'moderation.tool', 1"
            + " UNION ALL SELECT 'moderator', 'wired.menu', 1"
            + " UNION ALL SELECT 'moderator', 'catalog.builders_club.without_membership', 1"
            + " UNION ALL SELECT 'moderator', 'catalog.guild.any_group', 1"
            + " UNION ALL SELECT 'moderator', 'catalog.gift.hide_sender', 1"
            + " UNION ALL SELECT 'moderator', 'guild.delete_any', 1"
            + " UNION ALL SELECT 'moderator', 'chat.style.staff', 1"
            + " UNION ALL SELECT 'moderator', 'chat.furni_chooser', 1"
            + " UNION ALL SELECT 'moderator', 'perk.no_video_offers', 1"
            + " UNION ALL SELECT 'senior_moderator', 'room.control.any', 1"
            + " UNION ALL SELECT 'manager', 'navigator.category.staff', 1"
            + " UNION ALL SELECT 'manager', 'navigator.staff_pick', 1"
            + " UNION ALL SELECT 'admin', '*', 1"
            + " UNION ALL SELECT 'admin', 'room.furni.steal', 0"
            + " UNION ALL SELECT 'admin', 'perk.navigator.phase_one', 0"
            + ") s JOIN permission_groups g ON g.name = s.grp;";

        // Double the hotel's defaults (100 friends, 50 rooms, 30 favourites) and more for friends,
        // which is what VIP ranks sell; a hotel with other defaults edits them.
        private const string META =
            "INSERT IGNORE INTO permission_group_meta (group_id, meta_key, value, is_temporary) SELECT g.id, s.meta_key, s.value, 0 FROM ("
            + "SELECT 'vip' AS grp, 'limit.friends' AS meta_key, '500' AS value"
            + " UNION ALL SELECT 'vip', 'limit.rooms', '100'"
            + " UNION ALL SELECT 'vip', 'limit.favourite_rooms', '60'"
            + " UNION ALL SELECT 'admin', 'client.security_level', '8'"
            + ") s JOIN permission_groups g ON g.name = s.grp;";

        // PlayerPerkFlags: VoteInCompetitions = 1 << 1, JudgeChatReviews = 1 << 4,
        // UseGuideTool = 1 << 6, BuilderAtWork = 1 << 11, NavigatorPhaseOne2014 = 1 << 13.
        private const string PERK_FLAGS =
            "INSERT IGNORE INTO player_permission_nodes (player_id, node, value, is_temporary) SELECT p.id, s.node, 1, 0 FROM players p JOIN (SELECT 2 AS flag, 'perk.vote_in_competitions' AS node UNION ALL SELECT 16, 'perk.judge_chat_reviews' UNION ALL SELECT 64, 'perk.guide_tool' UNION ALL SELECT 2048, 'room.floorplan.large' UNION ALL SELECT 8192, 'perk.navigator.phase_one') s ON (p.perk_flags & s.flag) <> 0;";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(GROUPS);
            migrationBuilder.Sql(PARENTS);
            migrationBuilder.Sql(NODES);
            migrationBuilder.Sql(META);
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
