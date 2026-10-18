using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Gives the last two of Habbo's configuration furni the logic the server now has for them, so
    /// they stop standing as plain furniture: the Roller Speed Controller (<c>conf_queue_speed</c>)
    /// and the Wired Disabler (<c>conf_wired_disable</c>). As in <c>MapConfigurationFurniLogic</c>,
    /// only a definition still on the default logic, or already on the one given, is changed, so
    /// running it again changes nothing and staff choices stand. Data only: the model is
    /// unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261018163700_MapRollerSpeedAndWiredDisableLogic")]
    public partial class MapRollerSpeedAndWiredDisableLogic : Migration
    {
        private static readonly (string Name, string Logic)[] LOGICS =
        [
            ("conf_queue_speed", "roller_speed_control"),
            ("conf_wired_disable", "wired_disable"),
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
