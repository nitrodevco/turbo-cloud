using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Gives every monsterplant without one a body part. The client's monsterplant asset has no
    /// plant to draw without it (only a white blob), and planted seeds now roll one of its twelve
    /// body types; plants planted before keep a stable one picked from their id, in their own
    /// palette. Data only: the model is unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261008150000_GiveMonsterplantsABody")]
    public partial class GiveMonsterplantsABody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                "UPDATE `pets` SET `custom_parts` = CONCAT('1 ', 1 + MOD(`id`, 12), ' ', `palette_id`) WHERE `type_id` = 16 AND (`custom_parts` IS NULL OR `custom_parts` = '');"
            );

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A body given here cannot be told from one rolled at planting; it stays.
        }
    }
}
