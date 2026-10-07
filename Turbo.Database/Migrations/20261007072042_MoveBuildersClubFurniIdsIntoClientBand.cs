using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Builders Club furni were numbered from <c>0x7FFE_0000</c>, which the client reads as
    /// ordinary furni (its Builders Club band starts at <c>0x7FFF_0000</c>, temporary furni sit
    /// just below), so the info stand showed a borrowed item as the player's own. The band now
    /// matches the client's; ids already stored move up by the difference, which keeps them
    /// unique within their room. Ids are per room, so no other table names them.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261007072042_MoveBuildersClubFurniIdsIntoClientBand")]
    public partial class MoveBuildersClubFurniIdsIntoClientBand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 2147352576 = 0x7FFE_0000, 2147418112 = 0x7FFF_0000, 65536 = the difference.
            migrationBuilder.Sql(
                """
                UPDATE builders_club_furniture
                SET `room_object_id` = `room_object_id` + 65536
                WHERE `room_object_id` >= 2147352576 AND `room_object_id` < 2147418112
                ORDER BY `room_object_id` DESC;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE builders_club_furniture
                SET `room_object_id` = `room_object_id` - 65536
                WHERE `room_object_id` >= 2147418112
                ORDER BY `room_object_id` ASC;
                """
            );
        }
    }
}
