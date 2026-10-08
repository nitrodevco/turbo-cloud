using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;
using static Turbo.Database.Furniture.StockFurniture;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Gives Habbo's furni the behaviour their server class has in Sulake's furni data (github
    /// 80O/FurniData: the XML data to 2021, the Unity data since), which a hotel otherwise has to
    /// set by hand: every new item is plain furniture until staff give it a logic.
    /// <list type="bullet">
    /// <item><c>crackable</c>: the six crackable classes. Each gets its asset's state count, its usage
    /// (<c>&lt;everyone-can-use/&gt;</c> makes it everyone's, else room rights), whether it is walked on
    /// (<c>&lt;stand/&gt;</c>: piñatas), and a <c>crackable</c> section from its product parameter and
    /// class: the hits that crack it, the effect a hit needs, its hit achievements, who gets what
    /// is inside (whoever cracks a <c>Public</c> one, else the owner) and where it goes (the
    /// inventory for <c>CrackableInventoryProductRewardFurniture</c>, else the tile). What each
    /// holds is not in Sulake's data: the Habbo Club and Builders Club boxes hold 14 or 31 days,
    /// the rest what the Habbox Wiki's campaign pages, their illustrations and the hotel's own
    /// texts say, with their odds where given and equal chances where not. Growth and evolution
    /// chains give the next stage (Farm's seeds, Easter Garden's and Stranded Jungle's flowers,
    /// Christmas Citadel's creatures, Fairytale Easter's storybooks and eggs, the Matroyoshka
    /// dolls, Rune Rocks, Winter Palace's boxes); some give several things at once
    /// (<c>draws</c>: the Coral Kingdom chests, the Plushie Crafting Box). One with no known
    /// contents takes hits but is never cracked.</item>
    /// <item><c>vending_machine</c>: <c>VendingMachineFurni</c>, <c>IceCreamMachineFurni</c> (both
    /// animate state 1) and <c>HandItemProviderFurni</c> (static), with the hand items their
    /// <c>&lt;drinks&gt;</c> list; for those newer than the XML data, an NFT copy's original's, the
    /// hotel's own <c>handitemN</c> name for an item added with it, or the Habbox Wiki's list. All
    /// everyone's, as 277 of the 280 in the XML data are.</item>
    /// <item><c>effect_box</c>, <c>effect_provider</c> (everyone's), <c>effect_area</c>,
    /// <c>effect_tile</c>: <c>EffectBoxFurniture</c>, <c>AvatarEffectProviderFurniture</c>,
    /// <c>AvatarEffectAreaFurniture</c>, <c>SwitchStateActionWithAvatarEffectFurniture</c>; the effect is
    /// the definition's <c>customparams</c>, which the hotel's furnidata already carries.</item>
    /// <item><c>random_teleport</c>: <c>RandomInRoomTeleportFurniture</c> (the Banzai teleporter).</item>
    /// <item><c>floor_hole</c>: <c>HoleFurniture</c> (the Black Hole).</item>
    /// </list>
    /// A furni the hotel only has as colour variants (<c>rare_icecream*0</c> to <c>*12</c>) is matched
    /// by the name before its <c>*</c>. Only a definition still on a default logic, or already on
    /// the one given, is changed, so running it again changes nothing and staff choices stand.
    /// The furni and what each is given are <see cref="Furniture.StockFurniture"/>'s, which the
    /// furnidata import gives a definition it makes too.
    /// Data only: the model is unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261008170000_MapFurnitureBehaviours")]
    public partial class MapFurnitureBehaviours : Migration
    {
        private const string CRACKABLE = "crackable";
        private const string VENDING_MACHINE = "vending_machine";
        private const string EFFECT_PROVIDER = "effect_provider";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, states, usage, walkable, crackable) in CRACKABLES)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = '{CRACKABLE}', `total_states` = {states}, `usage_policy` = {usage}, "
                        + (walkable ? "`can_walk` = 1, " : "")
                        + $"`extra_data` = {WithSection("crackable", crackable)} WHERE {Unmapped(name, CRACKABLE)};"
                );

            foreach (var (name, usage, vending) in VENDING)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = '{VENDING_MACHINE}', `usage_policy` = {usage}, "
                        + $"`extra_data` = {WithSection("vending", vending)} WHERE {Unmapped(name, VENDING_MACHINE)};"
                );

            foreach (var (name, logic) in LOGICS)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = '{logic}'"
                        + (logic == EFFECT_PROVIDER ? ", `usage_policy` = 2" : "")
                        + $" WHERE {Unmapped(name, logic)};"
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to plain furniture without the section; the state count, usage and walkability
            // written before are not kept anywhere, and stay.
            foreach (var (name, _, _, _, _) in CRACKABLES)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = 'default_floor', `extra_data` = {WithoutSection("crackable")} WHERE {Mapped(name, CRACKABLE)};"
                );

            foreach (var (name, _, _) in VENDING)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = 'default_floor', `extra_data` = {WithoutSection("vending")} WHERE {Mapped(name, VENDING_MACHINE)};"
                );

            foreach (var (name, logic) in LOGICS)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = 'default_floor' WHERE {Mapped(name, logic)};"
                );
        }

        /// <summary>The definition by its name, or its colour variants (<c>name*1</c>).</summary>
        private static string Named(string name) =>
            $"(`name` = '{name}' OR (`name` LIKE '%*%' AND SUBSTRING_INDEX(`name`, '*', 1) = '{name}'))";

        private static string Unmapped(string name, string logic) =>
            $"{Named(name)} AND `logic` IN ('default_floor', 'none', '', '{logic}')";

        private static string Mapped(string name, string logic) =>
            $"{Named(name)} AND `logic` = '{logic}'";

        /// <summary>The extra data with the section set, kept beside whatever else it holds.</summary>
        private static string WithSection(string section, string json) =>
            $"IF(JSON_VALID(`extra_data`), JSON_SET(`extra_data`, '$.{section}', JSON_EXTRACT('{json}', '$')), "
            + $"JSON_OBJECT('{section}', JSON_EXTRACT('{json}', '$')))";

        /// <summary>The extra data without the section; nothing when that was all it held.</summary>
        private static string WithoutSection(string section) =>
            $"IF(JSON_VALID(`extra_data`) AND JSON_LENGTH(JSON_REMOVE(`extra_data`, '$.{section}')) > 0, "
            + $"JSON_REMOVE(`extra_data`, '$.{section}'), IF(JSON_VALID(`extra_data`), NULL, `extra_data`))";
    }
}
