using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// <c>MapVendingFurni</c> and <c>MapNewerVendingFurni</c> named nine furni by their Sulake class
    /// name, but the hotel's furnidata only has them as colour variants (<c>rare_icecream*0</c> to
    /// <c>*12</c>, <c>ads_711*1</c> to <c>*7</c>, ...), so those definitions were left on the default
    /// logic: the ice cream machines handed nothing out. This maps every variant of them the same
    /// way, by the name before its <c>*</c>. Data only: the model is unchanged, so there is no
    /// designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261008230000_MapVendingFurniColourVariants")]
    public partial class MapVendingFurniColourVariants : Migration
    {
        private const string LOGIC = "vending_machine";

        /// <summary>The class name, the usage policy (2 everyone), the <c>vending</c> section.</summary>
        private static readonly (string Name, int Usage, string Vending)[] VENDING =
        [
            ("ads_711", 2, """{"handItems":[33]}"""),
            ("ads_calip_cola", 2, """{"handItems":[26]}"""),
            ("bonusrare20_3", 2, """{"handItems":[53],"animates":false}"""),
            ("diner_gumvendor", 2, """{"handItems":[67,67,67,69,69,69,68],"animates":false}"""),
            ("minirare_icecream", 2, """{"handItems":[4]}"""),
            ("nft_rare_colourable_icecream", 2, """{"handItems":[4]}"""),
            ("nft_rare_icecream", 2, """{"handItems":[4]}"""),
            ("rare_colourable_icecream", 2, """{"handItems":[4]}"""),
            ("rare_icecream", 2, """{"handItems":[4]}"""),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, usage, vending) in VENDING)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = '{LOGIC}', `usage_policy` = {usage}, "
                        + $"`extra_data` = IF(JSON_VALID(`extra_data`), JSON_SET(`extra_data`, '$.vending', JSON_EXTRACT('{vending}', '$')), "
                        + $"JSON_OBJECT('vending', JSON_EXTRACT('{vending}', '$'))) "
                        + $"WHERE `name` LIKE '%*%' AND SUBSTRING_INDEX(`name`, '*', 1) = '{name}' "
                        + $"AND `logic` IN ('default_floor', 'none', '', '{LOGIC}');"
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, _, _) in VENDING)
                migrationBuilder.Sql(
                    "UPDATE `furniture_definitions` SET `logic` = 'default_floor', `extra_data` = "
                        + "IF(JSON_VALID(`extra_data`) AND JSON_LENGTH(JSON_REMOVE(`extra_data`, '$.vending')) > 0, JSON_REMOVE(`extra_data`, '$.vending'), "
                        + $"IF(JSON_VALID(`extra_data`), NULL, `extra_data`)) WHERE `name` LIKE '%*%' AND SUBSTRING_INDEX(`name`, '*', 1) = '{name}' AND `logic` = '{LOGIC}';"
                );
        }
    }
}
