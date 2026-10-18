using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// A list or map saved in the admin panel is now kept whole. Before, the binder added the
    /// configuration's items to those the config class starts with, so the panel kept only what
    /// went beyond them (an empty list for one saved as its defaults). Each such override takes
    /// the value staff saved, as the history kept it whole: the last change to the setting.
    /// Overrides with no history, and secrets (whose values are never kept), are left as they
    /// are. Data only: the model is unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261018230000_StoreListSettingsWhole")]
    public partial class StoreListSettingsWhole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE `server_settings` AS `s`
                JOIN `server_setting_changes` AS `c`
                    ON `c`.`id` = (
                        SELECT MAX(`l`.`id`) FROM `server_setting_changes` AS `l`
                        WHERE `l`.`setting_path` = `s`.`setting_path`
                    )
                SET `s`.`value` = `c`.`after`
                WHERE `c`.`secret` = 0
                    AND `c`.`after` IS NOT NULL
                    AND LEFT(TRIM(`s`.`value`), 1) IN ('[', '{')
                    AND LEFT(TRIM(`c`.`after`), 1) IN ('[', '{');
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // What went beyond the class's items can't be told apart in SQL: a whole list stays
            // whole, which the binder of before adds to the defaults again.
        }
    }
}
