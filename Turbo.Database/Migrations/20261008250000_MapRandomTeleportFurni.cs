using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Gives the furni of Sulake's <c>RandomInRoomTeleportFurniture</c> the <c>random_teleport</c>
    /// logic: the Banzai teleporter (<c>bb_rnd_tele</c>), the Halloween 2013 tiles and the Ghost
    /// Hotel teleport. Hoppers, which send to another room, are Sulake's
    /// <c>RoomNetworkTeleportFurniture</c> and are not among them. A furni the hotel only has as
    /// colour variants is matched by the name before its <c>*</c>; only a definition still on a
    /// default logic is changed. Data only: the model is unchanged, so there is no designer
    /// snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261008250000_MapRandomTeleportFurni")]
    public partial class MapRandomTeleportFurni : Migration
    {
        private const string LOGIC = "random_teleport";

        private static readonly string[] NAMES =
        [
            "bb_rnd_tele",
            "hween13_tile1",
            "hween13_tile2",
            "room_gh15_rtele",
        ];

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
