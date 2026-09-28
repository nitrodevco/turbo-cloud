using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Denies <c>room.furni.steal</c> to the seeded <c>moderator</c> and <c>admin</c> groups. Their
    /// <c>room.*</c> and <c>*</c> grant it, and once it gates pick-ups every furni either picked up
    /// would go into their own inventory instead of back to its owner. An exact node beats a
    /// wildcard in the same group, so the denial holds; a hotel that wants a stealing rank grants
    /// the node on purpose. Each group needs its own row, because <c>admin</c>'s <c>*</c> is
    /// weighed before anything it inherits from <c>moderator</c>.
    /// <para>
    /// <c>admin</c> also loses <c>perk.navigator.phase_one</c>, which its <c>*</c> would grant beside
    /// the phase-two perk everyone has: the client would run the phase-one navigator paths for
    /// administrators only.
    /// </para>
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20260928080000_SeedDenyFurniSteal")]
    public partial class SeedDenyFurniSteal : Migration
    {
        private const string NODES =
            "INSERT IGNORE INTO permission_group_nodes (group_id, node, value, is_temporary) SELECT g.id, s.node, 0, 0 FROM (SELECT 'moderator' AS grp, 'room.furni.steal' AS node UNION ALL SELECT 'admin', 'room.furni.steal' UNION ALL SELECT 'admin', 'perk.navigator.phase_one') s JOIN permission_groups g ON g.name = s.grp;";

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
