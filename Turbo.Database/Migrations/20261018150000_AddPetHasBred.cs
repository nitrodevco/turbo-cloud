using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// A monsterplant breeds once ("Plants can be bred only once!") until a re-breeding potion:
    /// pets get <c>has_bred</c>, and Habbo's potions (<c>mnstr_rebreed*</c>) the server's
    /// <c>pet_rebreed</c> logic, each with the rarity band Habbo's furni data names
    /// ("Rebreeding Potion 2 (One-time use for a single 6-8 level plant)"). Only a definition
    /// still on <c>default_floor</c>, or already on this one, is changed, and only empty extra data
    /// is filled, so staff choices stand.
    /// </summary>
    public partial class AddPetHasBred : Migration
    {
        private const string LOGIC = "pet_rebreed";
        private const string NAMES = "`name` LIKE 'mnstr\\_rebreed%'";

        private static readonly (string Name, string Band)[] BANDS =
        [
            ("mnstr_rebreed", "{\"MaxRarityLevel\":5}"),
            ("mnstr_rebreed_2", "{\"MinRarityLevel\":6,\"MaxRarityLevel\":8}"),
            ("mnstr_rebreed_3", "{\"MinRarityLevel\":9}"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_bred",
                table: "pets",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.Sql(
                $"UPDATE `furniture_definitions` SET `logic` = '{LOGIC}' WHERE {NAMES} AND `logic` IN ('default_floor', '{LOGIC}');"
            );

            foreach (var (name, band) in BANDS)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `extra_data` = '{{\"{LOGIC}\":{band}}}' WHERE `name` = '{name}' AND (`extra_data` IS NULL OR `extra_data` = '');"
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"UPDATE `furniture_definitions` SET `extra_data` = NULL WHERE {NAMES} AND `extra_data` LIKE '{{\"{LOGIC}\":%';"
            );

            migrationBuilder.Sql(
                $"UPDATE `furniture_definitions` SET `logic` = 'default_floor' WHERE {NAMES} AND `logic` = '{LOGIC}';"
            );

            migrationBuilder.DropColumn(name: "has_bred", table: "pets");
        }
    }
}
