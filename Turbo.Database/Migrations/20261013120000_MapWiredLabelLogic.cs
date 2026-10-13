using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Gives Habbo's WIRED label (<c>wf_label</c>, released 2026-10-13; its asset uses the client's
    /// <c>furniture_label</c> logic) the server's <c>wf_label</c> logic, named after the class as
    /// every wired furni's is, so it keeps a text and answers the label bubble and its settings.
    /// As in <c>MapRecolorableFurnitureLogic</c>, only a definition still on the default logic, or
    /// already on this one, is changed, so running it again changes nothing and staff choices
    /// stand. Data only: the model is unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261013120000_MapWiredLabelLogic")]
    public partial class MapWiredLabelLogic : Migration
    {
        private const string NAME = "wf_label";
        private const string LOGIC = "wf_label";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"UPDATE `furniture_definitions` SET `logic` = '{LOGIC}' WHERE `name` = '{NAME}' AND `logic` IN ('default_floor', '{LOGIC}');"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"UPDATE `furniture_definitions` SET `logic` = 'default_floor' WHERE `name` = '{NAME}' AND `logic` = '{LOGIC}';"
            );
        }
    }
}
