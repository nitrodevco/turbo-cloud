using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Gives Habbo's WIRED Links furni of 2026-10 the logics the server has for them. A hotel that
    /// imported them before the import gave a new definition the logic registered under its
    /// classname has them on the default logic, so the Link and its send, receive and speaker
    /// boxes were plain furniture whose windows never opened. A definition is matched by its name
    /// or its colour variants (<c>name*1</c>), and only one still on the default logic, or already
    /// on the one given, is changed, so running it again changes nothing and staff choices stand.
    /// Data only: the model is unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261021143000_MapWiredLinkLogic")]
    public partial class MapWiredLinkLogic : Migration
    {
        private static readonly string[] NAMES =
        [
            "wf_link",
            "wf_act_send_message",
            "wf_act_send_dataframe",
            "wf_trg_receive_message",
            "wf_trg_receive_dataframe",
            "wf_xtra_link_speaker",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var name in NAMES)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = '{name}' WHERE {Named(name)} AND `logic` IN ('default_floor', '{name}');"
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var name in NAMES)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = 'default_floor' WHERE {Named(name)} AND `logic` = '{name}';"
                );
        }

        /// <summary>The definition by its name, or its colour variants (<c>name*1</c>).</summary>
        private static string Named(string name) =>
            $"(`name` = '{name}' OR (`name` LIKE '%*%' AND SUBSTRING_INDEX(`name`, '*', 1) = '{name}'))";
    }
}
