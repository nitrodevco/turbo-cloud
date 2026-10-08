using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Points the stock Habbo badge displays at the badge display logic by definition name, so
    /// their badge, owner and date are read as the string array the client draws. Data only:
    /// the model is unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261007200000_MapBadgeDisplayLogic")]
    public partial class MapBadgeDisplayLogic : Migration
    {
        private const string LOGIC = "badge_display";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                $"UPDATE `furniture_definitions` SET `logic` = '{LOGIC}' WHERE `logic` = 'default_floor' AND `name` LIKE 'badge_display%';"
            );

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                $"UPDATE `furniture_definitions` SET `logic` = 'default_floor' WHERE `logic` = '{LOGIC}';"
            );
    }
}
