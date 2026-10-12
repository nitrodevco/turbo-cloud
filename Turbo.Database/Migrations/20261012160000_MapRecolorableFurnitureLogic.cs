using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Gives Habbo's recolorable furni (the 2026-10 Builders Club building blocks and alphabet
    /// blocks, every class named <c>*_recolorable</c>, whose assets use the client's
    /// <c>furniture_recolorable</c> logic) the server's <c>recolorable</c> logic, so they keep a
    /// colour and answer the recolor window. As in <c>MapConfigurationFurniLogic</c>, only a
    /// definition still on the default logic, or already on this one, is changed, so running it
    /// again changes nothing and staff choices stand. Data only: the model is unchanged, so there
    /// is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261012160000_MapRecolorableFurnitureLogic")]
    public partial class MapRecolorableFurnitureLogic : Migration
    {
        private const string LOGIC = "recolorable";

        // `_` is a LIKE wildcard, so it is escaped; the ESCAPE clause reads the same in MySQL,
        // MariaDB and SQLite.
        private const string MATCH = "`name` LIKE '%!_recolorable' ESCAPE '!'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"UPDATE `furniture_definitions` SET `logic` = '{LOGIC}' WHERE {MATCH} AND `logic` IN ('default_floor', '{LOGIC}');"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"UPDATE `furniture_definitions` SET `logic` = 'default_floor' WHERE {MATCH} AND `logic` = '{LOGIC}';"
            );
        }
    }
}
