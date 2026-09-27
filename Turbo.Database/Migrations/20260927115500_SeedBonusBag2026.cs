using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

namespace Turbo.Database.Migrations;

/// <summary>
/// Adds Bonus Bag III from the current furniture data for the reception campaign.
/// Existing operator definitions are preserved.
/// </summary>
[DbContext(typeof(TurboDbContext))]
[Migration("20260927115500_SeedBonusBag2026")]
public sealed class SeedBonusBag2026 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO furniture_definitions
                (sprite_id, name, type, category, logic, width, length, stack_height,
                 can_stack, can_walk, can_sit, can_lay, can_recycle, can_trade, deleted_at)
            SELECT 18432, 'bonusbag26_3', 0, 1, 'default_floor', 1, 1, 1,
                   0, 0, 0, 0, 1, 1, NULL
            WHERE NOT EXISTS (
                SELECT 1 FROM furniture_definitions
                WHERE name = 'bonusbag26_3' OR (sprite_id = 18432 AND type = 0 AND category = 1)
            );
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep content that may now be referenced by player furniture or catalog offers.
    }
}
