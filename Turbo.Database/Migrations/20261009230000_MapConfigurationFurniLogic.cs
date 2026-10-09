using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Gives two of Habbo's configuration furni the logic the server has for them, which they
    /// lacked, so they stood as plain furniture: the Room Area Hider (<c>conf_area_hide</c>, its
    /// dialog opened but neither its settings nor its switch reached the room) and the Invisible
    /// Furni Controller (<c>conf_invis_control</c>). Only a definition still on the default logic,
    /// or already on the one given, is changed, so running it again changes nothing and staff
    /// choices stand. Data only: the model is unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261009230000_MapConfigurationFurniLogic")]
    public partial class MapConfigurationFurniLogic : Migration
    {
        private static readonly (string Name, string Logic)[] LOGICS =
        [
            ("conf_area_hide", "area_hide"),
            ("conf_invis_control", "invisible_furni_control"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, logic) in LOGICS)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = '{logic}' WHERE `name` = '{name}' AND `logic` IN ('default_floor', '{logic}');"
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, logic) in LOGICS)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = 'default_floor' WHERE `name` = '{name}' AND `logic` = '{logic}';"
                );
        }
    }
}
