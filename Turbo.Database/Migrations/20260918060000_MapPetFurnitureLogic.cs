using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Points the stock Habbo pet furniture at the pet logics by definition name. Data only:
    /// the model is unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20260918060000_MapPetFurnitureLogic")]
    public partial class MapPetFurnitureLogic : Migration
    {
        private static readonly (string Logic, string Where)[] MAPPINGS =
        [
            (
                "pet_food",
                "name LIKE 'petfood%' OR name LIKE 'goodie%' OR name = 'pet_food_corn' OR name = 'horse_hay'"
            ),
            (
                "pet_drink",
                "name LIKE 'waterbowl%' OR name = 'pet_waterbottle' OR name = 'horse_trough'"
            ),
            (
                "pet_toy",
                "name LIKE 'toy1%' OR name = 'toy2' OR name LIKE 'pet_toy_%' OR name = 'pet_puppy_toy' OR name = 'pet_ufo_toy'"
            ),
            (
                "pet_nest",
                "name = 'nest' OR (name LIKE 'nest_%' AND name NOT LIKE 'nest_with_%') OR name LIKE 'pet_basket_%' OR name LIKE 'pet_blanket_%' OR name = 'horse_hayfloor'"
            ),
            ("pet_breeding_nest", "name LIKE 'pet_breeding_%'"),
            ("pet_saddle", "name LIKE 'horse_saddle%'"),
            ("pet_dye", "name LIKE 'horse_dye_%'"),
            ("pet_custom_part", "name LIKE 'horse_hairdye_%' OR name LIKE 'horse_hairstyle_%'"),
            ("monsterplant_seed", "name LIKE 'mnstr_seed%'"),
            ("pet_revive", "name LIKE 'mnstr_revival%'"),
            ("pet_fertilizer", "name LIKE 'mnstr_fert%'"),
            ("pet_package", "name LIKE 'petbox%'"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (logic, where) in MAPPINGS)
                migrationBuilder.Sql(
                    $"UPDATE furniture_definitions SET logic = '{logic}' WHERE logic = 'default_floor' AND ({where});"
                );

            // Rare seeds only sprout the rarer breeds.
            migrationBuilder.Sql(
                "UPDATE furniture_definitions SET extra_data = '{\"monsterplant_seed\":{\"MinRarityLevel\":1}}' WHERE name = 'mnstr_seed_rare' AND (extra_data IS NULL OR extra_data = '');"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (logic, _) in MAPPINGS)
                migrationBuilder.Sql(
                    $"UPDATE furniture_definitions SET logic = 'default_floor' WHERE logic = '{logic}';"
                );
        }
    }
}
