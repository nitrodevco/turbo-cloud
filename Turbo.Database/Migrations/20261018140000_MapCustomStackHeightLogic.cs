using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Gives Habbo's stack helper tiles (<c>tile_stackmagic*</c>) and walk tiles
    /// (<c>tile_walkmagic*</c>) the server's <c>custom_stack_height</c> logic, the one the helper's
    /// height, floor level, place on top and move up/down buttons are handled by. They were on
    /// <c>default_floor</c>, which ignores them all. Only a definition still on
    /// <c>default_floor</c>, or already on this one, is changed, so running it again changes nothing
    /// and staff choices stand. Down puts them back on <c>default_floor</c>. Data only: the model is
    /// unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261018140000_MapCustomStackHeightLogic")]
    public partial class MapCustomStackHeightLogic : Migration
    {
        private const string LOGIC = "custom_stack_height";
        private const string NAMES =
            "(`name` LIKE 'tile\\_stackmagic%' OR `name` LIKE 'tile\\_walkmagic%')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"UPDATE `furniture_definitions` SET `logic` = '{LOGIC}' WHERE {NAMES} AND `logic` IN ('default_floor', '{LOGIC}');"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"UPDATE `furniture_definitions` SET `logic` = 'default_floor' WHERE {NAMES} AND `logic` = '{LOGIC}';"
            );
        }
    }
}
