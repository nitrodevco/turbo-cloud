using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Gives the furni of Sulake's <c>HoleFurniture</c> the <c>floor_hole</c> logic: the Black
    /// Hole (<c>hole</c>, 2x2), the SnowStorm <c>hole1x1</c> and the internal <c>hole2</c> and
    /// <c>hole3</c>. A furni the hotel only has as colour variants is matched by the name before
    /// its <c>*</c>; only a definition still on a default logic is changed. Data only: the model
    /// is unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261008260000_MapFloorHoleFurni")]
    public partial class MapFloorHoleFurni : Migration
    {
        private const string LOGIC = "floor_hole";

        private static readonly string[] NAMES = ["hole", "hole1x1", "hole2", "hole3"];

        private static string Matches(string name) =>
            $"(`name` = '{name}' OR (`name` LIKE '%*%' AND SUBSTRING_INDEX(`name`, '*', 1) = '{name}'))";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var name in NAMES)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = '{LOGIC}' WHERE {Matches(name)} AND `logic` IN ('default_floor', 'none', '', '{LOGIC}');"
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var name in NAMES)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = 'default_floor' WHERE {Matches(name)} AND `logic` = '{LOGIC}';"
                );
        }
    }
}
