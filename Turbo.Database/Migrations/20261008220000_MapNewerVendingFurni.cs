using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Maps the hand-item furni Habbo released after Sulake's XML furni data stops, whose Unity data
    /// gives their server class (<c>VendingMachineFurni</c>, <c>IceCreamMachineFurni</c> or
    /// <c>HandItemProviderFurni</c>, so whether they animate) but not their <c>&lt;drinks&gt;</c>.
    /// What each hands out is, in order of evidence:
    /// <list type="bullet">
    /// <item>an NFT copy's original's <c>&lt;drinks&gt;</c> (<c>nft_samovar</c> pours the samovar's tea);</item>
    /// <item>the hotel's own name for a hand item added with it (<c>handitem169</c> "Coxinha" for the Plate
    /// of Coxinhas, <c>handitem1120</c> "Diamond Blade" for The Diamond Blade); an ice cream machine's
    /// ice cream (4), as every one in Sulake's data but the Calippo gives;</item>
    /// <item>the Habbox Wiki's list of hand items per furni.</item>
    /// </list>
    /// The furni with none of these (some fridges and NFT drinks machines) are left as they are.
    /// They are everyone's to use, as 277 of the 280 in Sulake's data are. Only a definition still on a
    /// default logic (or already vending) is changed. Data only: the model is unchanged, so there is
    /// no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261008220000_MapNewerVendingFurni")]
    public partial class MapNewerVendingFurni : Migration
    {
        private const string LOGIC = "vending_machine";

        /// <summary>Name, the usage policy (2 everyone), the <c>vending</c> section.</summary>
        private static readonly (string Name, int Usage, string Vending)[] VENDING =
        [
            ("ads_drsports_fballvend", 2, """{"handItems":[1106],"animates":false}"""),
            ("ads_drsports_microphone", 2, """{"handItems":[1105],"animates":false}"""),
            ("ads_drsports_microphone2", 2, """{"handItems":[1105],"animates":false}"""),
            ("ads_dtlrare_gold", 2, """{"handItems":[1093],"animates":false}"""),
            ("ads_dtlrare_gold2", 2, """{"handItems":[1093],"animates":false}"""),
            ("ads_fanta_vendingmachine", 2, """{"handItems":[147],"animates":false}"""),
            ("antique_c21_magnifyinglass", 2, """{"handItems":[148],"animates":false}"""),
            ("arcade_r23_bearbot", 2, """{"handItems":[155],"animates":false}"""),
            ("br_c25_cocoatree", 2, """{"handItems":[1115],"animates":false}"""),
            ("brbirthday_c25_coxinha", 2, """{"handItems":[169],"animates":false}"""),
            (
                "bubblejuice_r21_juicedispenser",
                2,
                """{"handItems":[19,30,66,114],"animates":false}"""
            ),
            ("catcafe_c23_candyapples", 2, """{"handItems":[156],"animates":false}"""),
            ("disco_c26_slay", 2, """{"handItems":[1124],"animates":false}"""),
            ("disco_r26_sharkzone", 2, """{"handItems":[1125],"animates":false}"""),
            ("ducket_c26_honeyfountain", 2, """{"handItems":[1121],"animates":false}"""),
            ("ducket_c26_honeystall", 2, """{"handItems":[1121],"animates":false}"""),
            ("easter_c24_springsticks", 2, """{"handItems":[1108],"animates":false}"""),
            ("fall_c23_campfire", 2, """{"handItems":[165],"animates":false}"""),
            ("frbirthday_c24_potatosack", 2, """{"handItems":[168],"animates":false}"""),
            ("game_c26_handheldvendingmchn", 2, """{"handItems":[1118],"animates":false}"""),
            ("gift_vend_phone", 2, """{"handItems":[1104],"animates":false}"""),
            ("habbo25_ltd25_duckymachine", 2, """{"handItems":[1116],"animates":false}"""),
            ("habbo25_r25_bdaycake", 2, """{"handItems":[171],"animates":false}"""),
            ("hhistory_c24_indestructiblephone", 2, """{"handItems":[1110],"animates":false}"""),
            ("hobbies_r26_diamondblade", 2, """{"handItems":[1120],"animates":false}"""),
            ("hween_r25_mushroommchn", 2, """{"handItems":[175],"animates":false}"""),
            ("hygge_c25_knifeblock", 2, """{"handItems":[1117],"animates":false}"""),
            ("icecream_c21_machine", 2, """{"handItems":[4,75,76,77],"animates":false}"""),
            ("ktchn_hlthNut", 2, """{"handItems":[3]}"""),
            ("lt_ltd26_woodjuicedispenser", 2, """{"handItems":[181],"animates":false}"""),
            ("lt_r26_gemstatue", 2, """{"handItems":[1123],"animates":false}"""),
            ("lt_r26_icecream", 2, """{"handItems":[180],"animates":false}"""),
            ("minirare_icecream", 2, """{"handItems":[4]}"""),
            (
                "mode_gold_fridge",
                2,
                """{"handItems":[3,39,42,43,66,96,129,130,133,134,168],"animates":false}"""
            ),
            ("nft_c23_kryptomon_pinksyrup", 2, """{"handItems":[154]}"""),
            ("nft_eco_fruits1", 2, """{"handItems":[36,37,38,39]}"""),
            ("nft_eco_fruits2", 2, """{"handItems":[36,37,38,39]}"""),
            ("nft_eco_fruits3", 2, """{"handItems":[36,37,38,39]}"""),
            ("nft_eco_tree1", 2, """{"handItems":[38]}"""),
            ("nft_eco_tree2", 2, """{"handItems":[36]}"""),
            ("nft_emerald_heart_2000", 2, """{"handItems":[1114],"animates":false}"""),
            ("nft_exe_icecream", 2, """{"handItems":[4]}"""),
            ("nft_gold_c15_arc_hole", 2, """{"handItems":[34],"animates":false}"""),
            ("nft_h23_icecreamcone1", 2, """{"handItems":[160],"animates":false}"""),
            ("nft_h23_icecreamcone2", 2, """{"handItems":[161],"animates":false}"""),
            ("nft_h23_icecreamcone3", 2, """{"handItems":[162],"animates":false}"""),
            ("nft_h23_juicebox1", 2, """{"handItems":[157],"animates":false}"""),
            ("nft_h23_juicebox2", 2, """{"handItems":[158],"animates":false}"""),
            ("nft_h23_juicebox3", 2, """{"handItems":[159],"animates":false}"""),
            ("nft_h25_xmasicm", 2, """{"handItems":[4]}"""),
            ("nft_hal_cauldron", 2, """{"handItems":[3]}"""),
            ("nft_hblooza_bubblejuice", 2, """{"handItems":[19],"animates":false}"""),
            ("nft_hblooza_candyfloss", 2, """{"handItems":[79,80],"animates":false}"""),
            ("nft_hblooza_hotdog", 2, """{"handItems":[81],"animates":false}"""),
            ("nft_hblooza_popcorn", 2, """{"handItems":[63],"animates":false}"""),
            ("nft_hc_btlr", 2, """{"handItems":[24]}"""),
            ("nft_hween_r18_antiquechemset", 2, """{"handItems":[44]}"""),
            (
                "nft_ktchn15_bubblejuicerack",
                2,
                """{"handItems":[24,29,50,74,101],"animates":false}"""
            ),
            ("nft_ktchn15_coffeemaker", 2, """{"handItems":[41,53],"animates":false}"""),
            ("nft_ktchn15_fridge", 2, """{"handItems":[3,36,37,38,39]}"""),
            (
                "nft_matic_dispenser",
                2,
                """{"handItems":[1032,1033,1034,1035,1036,1037,1038,1032,1033,1034,1035,1036,1037,1038,1,3,28,29,34,36,37,38,39,58,70,71,1013,1014,1015,1019,1029,1051,1031],"animates":false}"""
            ),
            ("nft_md_limukaappi", 2, """{"handItems":[19]}"""),
            ("nft_mocchamaster", 2, """{"handItems":[8,9,10,11,12,13,14,15,16,17]}"""),
            ("nft_paris15_cake", 2, """{"handItems":[96],"animates":false}"""),
            ("nft_pirate_navdesk", 2, """{"handItems":[82],"animates":false}"""),
            ("nft_rare_blackrosegold_icecream", 2, """{"handItems":[4],"animates":false}"""),
            ("nft_rare_colourable_icecream", 2, """{"handItems":[4]}"""),
            ("nft_rare_icecream", 2, """{"handItems":[4]}"""),
            ("nft_samovar", 2, """{"handItems":[1]}"""),
            ("nft_sc24_floatingduck", 2, """{"handItems":[1111],"animates":false}"""),
            ("nft_sc24_goldfishfountain", 2, """{"handItems":[1109],"animates":false}"""),
            ("nft_val_cauldron", 2, """{"handItems":[25]}"""),
            ("nft_xm09_cocoa", 2, """{"handItems":[15]}"""),
            ("nft_xmas11_btlr", 2, """{"handItems":[15]}"""),
            ("nft_xmas12_nutcracker", 2, """{"handItems":[60],"animates":false}"""),
            ("nft_xmas13_icecream", 2, """{"handItems":[4]}"""),
            ("nft_xmas_r21_luxuryhotchocolate", 2, """{"handItems":[149],"animates":false}"""),
            ("nl_c25_meatballs", 2, """{"handItems":[170],"animates":false}"""),
            ("pj_r26_engagementring", 2, """{"handItems":[1119],"animates":false}"""),
            ("pj_r26_rosesamovar", 2, """{"handItems":[178],"animates":false}"""),
            ("rainbow_ltd23_icecream", 2, """{"handItems":[163],"animates":false}"""),
            ("rare_r22_clawmachine", 2, """{"handItems":[1098,1099,1100],"animates":false}"""),
            ("sanrio_c22_hktoaster", 2, """{"handItems":[153],"animates":false}"""),
            ("school_c22_equipment", 2, """{"handItems":[1101,1102,1103],"animates":false}"""),
            ("skorea_c22_dalgona", 2, """{"handItems":[150,151,152],"animates":false}"""),
            ("stellar_c23_astralbow", 2, """{"handItems":[1107],"animates":false}"""),
            ("vwave_c21_barglasses", 2, """{"handItems":[35,40,50,74,101],"animates":false}"""),
            ("xmas_c21_advent7", 2, """{"handItems":[1081],"animates":false}"""),
            ("xmas_c24_batterycandle", 2, """{"handItems":[1113],"animates":false}"""),
            ("xmas_c24_drawpad", 2, """{"handItems":[1112],"animates":false}"""),
            ("xmas_r21_luxuryhotchocolate", 2, """{"handItems":[149],"animates":false}"""),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, usage, vending) in VENDING)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = '{LOGIC}', `usage_policy` = {usage}, "
                        + $"`extra_data` = IF(JSON_VALID(`extra_data`), JSON_SET(`extra_data`, '$.vending', JSON_EXTRACT('{vending}', '$')), "
                        + $"JSON_OBJECT('vending', JSON_EXTRACT('{vending}', '$'))) "
                        + $"WHERE `name` = '{name}' AND `logic` IN ('default_floor', 'none', '', '{LOGIC}');"
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, _, _) in VENDING)
                migrationBuilder.Sql(
                    "UPDATE `furniture_definitions` SET `logic` = 'default_floor', `extra_data` = "
                        + "IF(JSON_VALID(`extra_data`) AND JSON_LENGTH(JSON_REMOVE(`extra_data`, '$.vending')) > 0, JSON_REMOVE(`extra_data`, '$.vending'), "
                        + $"IF(JSON_VALID(`extra_data`), NULL, `extra_data`)) WHERE `name` = '{name}' AND `logic` = '{LOGIC}';"
                );
        }
    }
}
