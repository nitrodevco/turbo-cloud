using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Gives the effect furni in Sulake's furni data (XML to 2021, Unity data since) their logic,
    /// by server class: <c>EffectBoxFurniture</c> <c>effect_box</c>, <c>AvatarEffectProviderFurniture</c>
    /// <c>effect_provider</c> (everyone's, as every one in the XML data is),
    /// <c>AvatarEffectAreaFurniture</c> <c>effect_area</c> and
    /// <c>SwitchStateActionWithAvatarEffectFurniture</c> <c>effect_tile</c>. The effect each puts on is
    /// its <c>customparams</c>, which the hotel's furnidata already gives the definition. A furni
    /// the hotel only has as colour variants (<c>name*1</c>) is matched by the name before its
    /// <c>*</c>; only a definition still on a default logic is changed. Data only: the model is
    /// unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261008240000_MapEffectFurni")]
    public partial class MapEffectFurni : Migration
    {
        private const string PROVIDER = "effect_provider";

        /// <summary>The class name and the logic.</summary>
        private static readonly (string Name, string Logic)[] EFFECT_FURNI =
        [
            ("ads_clearasil_tile1", "effect_tile"),
            ("ads_nick_faketile", "effect_tile"),
            ("bazaar_c17_flycarpet", "effect_tile"),
            ("bc_block_water", "effect_area"),
            ("celestial_c19_starshower", "effect_tile"),
            ("circus_c24_clowncar", "effect_tile"),
            ("circus_r24_clownmirror", "effect_tile"),
            ("coralking_r18_turtleride", "effect_tile"),
            ("cpunk15_gunvender", "effect_provider"),
            ("easter14_effectgiver", "effect_tile"),
            ("effect_faketile", "effect_tile"),
            ("fxbox_fx116", "effect_box"),
            ("fxbox_fx123", "effect_box"),
            ("fxbox_fx125", "effect_box"),
            ("fxbox_fx126", "effect_box"),
            ("fxbox_fx127", "effect_box"),
            ("fxbox_fx14", "effect_box"),
            ("fxbox_fx143", "effect_box"),
            ("fxbox_fx147", "effect_box"),
            ("fxbox_fx152", "effect_box"),
            ("fxbox_fx153", "effect_box"),
            ("fxbox_fx16", "effect_box"),
            ("fxbox_fx183", "effect_box"),
            ("fxbox_fx192", "effect_box"),
            ("fxbox_fx238", "effect_box"),
            ("fxbox_fx239", "effect_box"),
            ("fxbox_fx240", "effect_box"),
            ("fxbox_fx241", "effect_box"),
            ("fxbox_fx92", "effect_box"),
            ("greek_r19_tiledbath", "effect_area"),
            ("hblooza14_dance", "effect_area"),
            ("hblooza14_planepadb", "effect_tile"),
            ("hblooza14_planepadr", "effect_tile"),
            ("hblooza14_shotgall", "effect_provider"),
            ("hotel_c18_pool", "effect_area"),
            ("hs_dnctile_1", "effect_area"),
            ("hs_marswalk", "effect_area"),
            ("hween12_guillotine", "effect_tile"),
            ("hween_c17_bonfire", "effect_tile"),
            ("hween_c17_fallingrocks", "effect_tile"),
            ("hween_c23_cursedcatdoll", "effect_provider"),
            ("hween_c23_cursedeye", "effect_tile"),
            ("hween_r20_hourglass", "effect_area"),
            ("js_r16_shark", "effect_tile"),
            ("lt_c26_trunkswing", "effect_tile"),
            ("matic_sanitizer", "effect_tile"),
            ("mushroom_c21_bouncymushroom", "effect_area"),
            ("mystics_btile1", "effect_tile"),
            ("mystics_gtile1", "effect_tile"),
            ("nft_ff23_scooter", "effect_provider"),
            ("nft_ff23_scooter2", "effect_provider"),
            ("nft_ff23_scooter3", "effect_provider"),
            ("nft_greek_r19_tiledbath", "effect_area"),
            ("nft_h25_monkeybzn_pond", "effect_area"),
            ("nft_h26_discobed", "effect_area"),
            ("nft_val15_hottub", "effect_area"),
            ("olympics_c16_trampoline", "effect_area"),
            ("pirate_gunrack", "effect_provider"),
            ("pirate_swordrack", "effect_provider"),
            ("room_noob_fx1", "effect_tile"),
            ("room_noob_fx2", "effect_tile"),
            ("room_noob_fx3", "effect_tile"),
            ("room_noob_fx4", "effect_tile"),
            ("room_noob_fxremove", "effect_tile"),
            ("room_noob_pool", "effect_area"),
            ("skorea_c22_lightstick1", "effect_provider"),
            ("skorea_c22_lightstick2", "effect_provider"),
            ("skorea_c22_lightstick3", "effect_provider"),
            ("smiley_c23_trampoline1", "effect_area"),
            ("smiley_c23_trampoline2", "effect_area"),
            ("smiley_c23_trampoline3", "effect_area"),
            ("smiley_c23_trampoline4", "effect_area"),
            ("tiki_c15_leigiver", "effect_provider"),
            ("uni_wobench", "effect_tile"),
            ("val15_hottub", "effect_area"),
            ("vikings_weapon", "effect_provider"),
            ("wildwest_tarbucket", "effect_box"),
            ("xmas_c18_bounceycastle", "effect_area"),
            ("xmas_c24_baubletrap", "effect_tile"),
            ("xmas_c24_pogoball", "effect_area"),
            ("xmas_c24_presentrap", "effect_tile"),
        ];

        private static string Matches(string name) =>
            $"(`name` = '{name}' OR (`name` LIKE '%*%' AND SUBSTRING_INDEX(`name`, '*', 1) = '{name}'))";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, logic) in EFFECT_FURNI)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = '{logic}'"
                        + (logic == PROVIDER ? ", `usage_policy` = 2" : "")
                        + $" WHERE {Matches(name)} AND `logic` IN ('default_floor', 'none', '', '{logic}');"
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, logic) in EFFECT_FURNI)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = 'default_floor' WHERE {Matches(name)} AND `logic` = '{logic}';"
                );
        }
    }
}
