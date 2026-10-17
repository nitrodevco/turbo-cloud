using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Points the client's starter room pictures (<c>new.user.flow.room.image.url</c>) at the
    /// gamedata host's <c>/gamedata/starter-rooms/</c>, where they are served now: under
    /// <c>/gamedata/</c>, the one path the reverse proxy passes on to the gamedata host, so the old
    /// <c>/starter-rooms/</c> reached the website instead of the picture. The variable is written
    /// whenever a starter room is added or removed; this moves the address a hotel already has.
    /// Only a value on the old address is changed, so running it again changes nothing. Data only:
    /// the model is unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261017233000_MoveStarterRoomPictureAddress")]
    public partial class MoveStarterRoomPictureAddress : Migration
    {
        private const string KEY = "new.user.flow.room.image.url";
        private const string OLD_ROUTE = "/starter-rooms/";
        private const string NEW_ROUTE = "/gamedata/starter-rooms/";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"UPDATE `gamedata_variables` SET `value` = REPLACE(`value`, '{OLD_ROUTE}', '{NEW_ROUTE}') WHERE `variable_key` = '{KEY}' AND `value` LIKE '%{OLD_ROUTE}%' AND `value` NOT LIKE '%{NEW_ROUTE}%';"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"UPDATE `gamedata_variables` SET `value` = REPLACE(`value`, '{NEW_ROUTE}', '{OLD_ROUTE}') WHERE `variable_key` = '{KEY}';"
            );
        }
    }
}
