using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Gives Habbo's one-way gates (<c>one_way_door*1</c> to <c>*9</c> and
    /// <c>onewaydoor_c22_rosegold</c>) the server's <c>one_way_door</c> logic, so they let an avatar
    /// through from the front only. They were on the plain <c>gate</c> logic, which a use opens
    /// both ways. Only a definition still on <c>default_floor</c> or <c>gate</c>, or already on this
    /// one, is changed, so running it again changes nothing and staff choices stand. Down puts
    /// them back on <c>gate</c>. Data only: the model is unchanged, so there is no designer
    /// snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261018030000_MapOneWayDoorLogic")]
    public partial class MapOneWayDoorLogic : Migration
    {
        private const string LOGIC = "one_way_door";
        private const string NAMES =
            "`name` IN ('one_way_door*1', 'one_way_door*2', 'one_way_door*3', 'one_way_door*4', "
            + "'one_way_door*5', 'one_way_door*6', 'one_way_door*7', 'one_way_door*8', "
            + "'one_way_door*9', 'onewaydoor_c22_rosegold')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"UPDATE `furniture_definitions` SET `logic` = '{LOGIC}' WHERE {NAMES} AND `logic` IN ('default_floor', 'gate', '{LOGIC}');"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"UPDATE `furniture_definitions` SET `logic` = 'gate' WHERE {NAMES} AND `logic` = '{LOGIC}';"
            );
        }
    }
}
