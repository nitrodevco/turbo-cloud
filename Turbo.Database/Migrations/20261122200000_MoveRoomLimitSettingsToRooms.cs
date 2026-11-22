using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// A room's limits are the room's settings alone. <c>Turbo:Navigator</c> had its own copy of
    /// the name, description, visitor and tag limits, which only a new room kept to, while every
    /// later change kept to <c>Turbo:Rooms</c>'; the copy is gone. An override staff saved in the
    /// panel for one of the old settings moves to its room setting, unless that has an override of
    /// its own (the one every change to a room already kept to); what is left of the old ones goes.
    /// Data only: the model is unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261122200000_MoveRoomLimitSettingsToRooms")]
    public partial class MoveRoomLimitSettingsToRooms : Migration
    {
        private static readonly (string From, string To)[] MOVES =
        [
            ("Turbo:Navigator:RoomNameMinLength", "Turbo:Rooms:RoomNameMinLength"),
            ("Turbo:Navigator:RoomNameMaxLength", "Turbo:Rooms:RoomNameMaxLength"),
            ("Turbo:Navigator:RoomDescriptionMaxLength", "Turbo:Rooms:RoomDescriptionMaxLength"),
            ("Turbo:Navigator:MaxPlayersLimit", "Turbo:Rooms:MaxPlayersLimit"),
            ("Turbo:Navigator:MaxTagsPerRoom", "Turbo:Rooms:RoomTagsMax"),
            ("Turbo:Navigator:MaxTagLength", "Turbo:Rooms:RoomTagMaxLength"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (from, to) in MOVES)
            {
                // A join, not a subquery: MySQL refuses an UPDATE that reads its own table in one.
                migrationBuilder.Sql(
                    $"""
                    UPDATE `server_settings` AS `moved`
                    LEFT JOIN `server_settings` AS `held` ON `held`.`setting_path` = '{to}'
                    SET `moved`.`setting_path` = '{to}'
                    WHERE `moved`.`setting_path` = '{from}' AND `held`.`id` IS NULL;
                    """
                );
                migrationBuilder.Sql(
                    $"DELETE FROM `server_settings` WHERE `setting_path` = '{from}';"
                );
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The navigator's copies are gone from the code; an override stays on the room's.
        }
    }
}
