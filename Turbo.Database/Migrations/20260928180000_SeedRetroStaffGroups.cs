using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Fills out the seeded groups to the ladder most retro hotels staff: <c>vip</c> for a hotel
    /// that sells one (higher limits, large floor plans), <c>builder</c> and <c>events</c> for staff
    /// who build rooms or run events without moderating, and <c>trial_moderator</c> below
    /// <c>moderator</c> (the moderation tool and kick/mute/ban, without controlling every room or
    /// the catalogue's staff powers). <c>moderator</c> now inherits from <c>trial_moderator</c>, and
    /// <c>community</c> from <c>builder</c> and <c>events</c> as well. Helpers may vote in
    /// competitions, which the client puts at helper level 2. See <c>docs/permissions.md</c> §13.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20260928180000_SeedRetroStaffGroups")]
    public partial class SeedRetroStaffGroups : Migration
    {
        private const string GROUPS =
            "INSERT IGNORE INTO permission_groups (name, display_name, weight) VALUES ('vip', 'VIP', 10), ('builder', 'Builder', 35), ('events', 'Event Staff', 40), ('trial_moderator', 'Trial Moderator', 45);";

        private const string MODERATOR_PARENT =
            "DELETE l FROM permission_group_parents l JOIN permission_groups g ON g.id = l.group_id JOIN permission_groups p ON p.id = l.parent_group_id WHERE g.name = 'moderator' AND p.name = 'helper';";

        private const string PARENTS =
            "INSERT IGNORE INTO permission_group_parents (group_id, parent_group_id) SELECT g.id, p.id FROM (SELECT 'vip' AS child, 'default' AS parent UNION ALL SELECT 'builder', 'default' UNION ALL SELECT 'events', 'default' UNION ALL SELECT 'trial_moderator', 'helper' UNION ALL SELECT 'moderator', 'trial_moderator' UNION ALL SELECT 'community', 'builder' UNION ALL SELECT 'community', 'events') s JOIN permission_groups g ON g.name = s.child JOIN permission_groups p ON p.name = s.parent;";

        private const string NODES =
            "INSERT IGNORE INTO permission_group_nodes (group_id, node, value, is_temporary) SELECT g.id, s.node, 1, 0 FROM ("
            + "SELECT 'vip' AS grp, 'room.floorplan.large' AS node"
            + " UNION ALL SELECT 'helper', 'perk.vote_in_competitions'"
            + " UNION ALL SELECT 'builder', 'room.floorplan.save_without_club'"
            + " UNION ALL SELECT 'builder', 'room.floorplan.large'"
            + " UNION ALL SELECT 'builder', 'room.furni.branding'"
            + " UNION ALL SELECT 'builder', 'wired.menu'"
            + " UNION ALL SELECT 'builder', 'chat.furni_chooser'"
            + " UNION ALL SELECT 'events', 'room.event.edit_any'"
            + " UNION ALL SELECT 'events', 'room.enter.locked'"
            + " UNION ALL SELECT 'events', 'room.enter.full'"
            + " UNION ALL SELECT 'events', 'room.furni.youtube_any'"
            + " UNION ALL SELECT 'events', 'room.furni.vimeo_edit'"
            + " UNION ALL SELECT 'events', 'chat.furni_chooser'"
            + " UNION ALL SELECT 'trial_moderator', 'moderation.tool'"
            + " UNION ALL SELECT 'trial_moderator', 'room.moderate.any'"
            + " UNION ALL SELECT 'trial_moderator', 'room.enter.locked'"
            + " UNION ALL SELECT 'trial_moderator', 'room.enter.full'"
            + " UNION ALL SELECT 'trial_moderator', 'chat.style.staff'"
            + " UNION ALL SELECT 'trial_moderator', 'perk.no_video_offers'"
            + ") s JOIN permission_groups g ON g.name = s.grp;";

        // Double the hotel's defaults (100 friends, 50 rooms, 30 favourites) and more for friends,
        // which is what VIP ranks sell; a hotel with other defaults edits them.
        private const string VIP_LIMITS =
            "INSERT IGNORE INTO permission_group_meta (group_id, meta_key, value, is_temporary) SELECT g.id, s.meta_key, s.value, 0 FROM (SELECT 'limit.friends' AS meta_key, '500' AS value UNION ALL SELECT 'limit.rooms', '100' UNION ALL SELECT 'limit.favourite_rooms', '60') s JOIN permission_groups g ON g.name = 'vip';";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(GROUPS);
            migrationBuilder.Sql(MODERATOR_PARENT);
            migrationBuilder.Sql(PARENTS);
            migrationBuilder.Sql(NODES);
            migrationBuilder.Sql(VIP_LIMITS);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Seeded rows are indistinguishable from operator rows once edited; they stay.
        }
    }
}
