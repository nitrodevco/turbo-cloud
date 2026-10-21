using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Gives Habbo's room branding furni and room background toners the logics the server has
    /// for them, so their map and number data reach the client and survive a reload. Branding:
    /// every furni whose asset names the client logic <c>furniture_bg</c> (<c>ads_background</c>)
    /// or <c>furniture_bb</c> (<c>ads_mpu_160</c>, <c>ads_mpu_300</c>, <c>ads_mpu_720</c>); toners:
    /// <c>furniture_background_color</c> (<c>roombg_color</c>, <c>room_info15_bg</c>). A
    /// definition is matched by its name or its colour variants (<c>name*1</c>), and only one
    /// still on the default logic, or already on the one given, is changed, so running it again
    /// changes nothing and staff choices stand. Data only: the model is unchanged, so there is no
    /// designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261021100000_MapRoomBrandingAndTonerLogic")]
    public partial class MapRoomBrandingAndTonerLogic : Migration
    {
        private static readonly (string Name, string Logic)[] LOGICS =
        [
            ("ads_background", "room_branding"),
            ("ads_mpu_160", "room_branding"),
            ("ads_mpu_300", "room_branding"),
            ("ads_mpu_720", "room_branding"),
            ("roombg_color", "background_toner"),
            ("room_info15_bg", "background_toner"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, logic) in LOGICS)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = '{logic}' WHERE {Named(name)} AND `logic` IN ('default_floor', '{logic}');"
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, logic) in LOGICS)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = 'default_floor' WHERE {Named(name)} AND `logic` = '{logic}';"
                );
        }

        /// <summary>The definition by its name, or its colour variants (<c>name*1</c>).</summary>
        private static string Named(string name) =>
            $"(`name` = '{name}' OR (`name` LIKE '%*%' AND SUBSTRING_INDEX(`name`, '*', 1) = '{name}'))";
    }
}
