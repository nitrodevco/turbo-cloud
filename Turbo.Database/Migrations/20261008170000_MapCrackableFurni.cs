using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Points every crackable furni Sulake's furni data defines at the crackable logic, with the
    /// state count of its asset and how it opens under <c>crackable</c> in the definition's extra
    /// data. Each row is Sulake's product parameter (reward set, target, required effect, hit
    /// achievements) with what its server class decides:
    /// <list type="bullet">
    /// <item>a box opened by a single hit (<c>target</c> 1) is hit by its owner alone, so nobody
    /// else can open somebody's tradable gift for them; anything that takes more hits is hit by
    /// anyone in the room, as the client offers its Use button to everyone;</item>
    /// <item><c>PublicCrackableProductRewardFurniture</c> gives what it holds to whoever cracks
    /// it, the others to the furni's owner;</item>
    /// <item><c>CrackableInventoryProductRewardFurniture</c> puts its reward in the inventory, the
    /// others on the crackable's tile;</item>
    /// <item>the Habbo Club and Builders Club boxes (<c>hcgift_1/2</c>, <c>bcgift_1/2</c>) hold 14
    /// and 31 days of their membership.</item>
    /// </list>
    /// The furni reward sets' contents are not in Sulake's data, so those crackables take hits but
    /// are not cracked until a hotel lists their <c>rewards</c>. A definition this hotel does not
    /// have is left alone. Data only: the model is unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261008170000_MapCrackableFurni")]
    public partial class MapCrackableFurni : Migration
    {
        private const string LOGIC = "crackable";

        /// <summary>Name, the asset's state count, the <c>crackable</c> section.</summary>
        private static readonly (string Name, int States, string Crackable)[] CRACKABLES =
        [
            (
                "bc_gift_14days",
                3,
                """{"rewardSet":"bcgift_1","target":1,"hitBy":"Owner","subscription":"BuildersClub","subscriptionDays":14}"""
            ),
            (
                "bc_gift_31days",
                3,
                """{"rewardSet":"bcgift_2","target":1,"hitBy":"Owner","subscription":"BuildersClub","subscriptionDays":31}"""
            ),
            ("bonusbag16_1", 3, """{"rewardSet":"bonusrares16_1","target":1,"hitBy":"Owner"}"""),
            ("bonusbag16_2", 3, """{"rewardSet":"bonusrares16_2","target":1,"hitBy":"Owner"}"""),
            ("bonusbag16_3", 3, """{"rewardSet":"bonusrares16_3","target":1,"hitBy":"Owner"}"""),
            ("bonusbag17_1", 3, """{"rewardSet":"bonusrares17_1","target":1,"hitBy":"Owner"}"""),
            ("bonusbag17_2", 3, """{"rewardSet":"bonusrares17_2","target":1,"hitBy":"Owner"}"""),
            ("bonusbag17_3", 3, """{"rewardSet":"bonusrares17_3","target":1,"hitBy":"Owner"}"""),
            ("bonusbag17_4", 3, """{"rewardSet":"bonusrares17_4","target":1,"hitBy":"Owner"}"""),
            ("bonusbag18_1", 3, """{"rewardSet":"bonusrare18_1","target":1,"hitBy":"Owner"}"""),
            ("bonusbag18_2", 3, """{"rewardSet":"bonusrare18_2","target":1,"hitBy":"Owner"}"""),
            ("bonusbag18_3", 3, """{"rewardSet":"bonusrare18_3","target":1,"hitBy":"Owner"}"""),
            ("bonusbag18_4", 3, """{"rewardSet":"bonusrare18_4","target":1,"hitBy":"Owner"}"""),
            ("bonusbag19_1", 3, """{"rewardSet":"bonusrare19_1","target":1,"hitBy":"Owner"}"""),
            ("bonusbag19_2", 3, """{"rewardSet":"bonusrare19_2","target":1,"hitBy":"Owner"}"""),
            ("bonusbag19_3", 3, """{"rewardSet":"bonusrare19_3","target":1,"hitBy":"Owner"}"""),
            ("bonusbag19_4", 3, """{"rewardSet":"bonusrare19_4","target":1,"hitBy":"Owner"}"""),
            ("bonusbag20_1", 3, """{"rewardSet":"bonusrares20_1","target":1,"hitBy":"Owner"}"""),
            ("bonusbag20_2", 3, """{"rewardSet":"bonusrares20_2","target":1,"hitBy":"Owner"}"""),
            ("bonusbag20_3", 3, """{"rewardSet":"bonusrares20_3","target":1,"hitBy":"Owner"}"""),
            ("bonusbag20_4", 3, """{"rewardSet":"bonusrares20_4","target":1,"hitBy":"Owner"}"""),
            ("bonusbag21_1", 3, """{"rewardSet":"bonusrares21_1","target":1,"hitBy":"Owner"}"""),
            ("bonusbag21_2", 3, """{"rewardSet":"bonusrares21_2","target":1,"hitBy":"Owner"}"""),
            ("bonusbag21_3", 3, """{"rewardSet":"bonusrares21_3","target":1,"hitBy":"Owner"}"""),
            ("bonusbag21_4", 3, """{"rewardSet":"bonusrares21_4","target":1,"hitBy":"Owner"}"""),
            (
                "booster_c19_box1",
                3,
                """{"rewardSet":"booster19_1_blue","target":1,"hitBy":"Owner","rewardPlacement":"Inventory"}"""
            ),
            (
                "booster_c19_box2",
                3,
                """{"rewardSet":"booster19_1_red","target":1,"hitBy":"Owner","rewardPlacement":"Inventory"}"""
            ),
            (
                "booster_c20_box",
                3,
                """{"rewardSet":"booster20_1","target":1,"hitBy":"Owner","rewardPlacement":"Inventory"}"""
            ),
            (
                "coralking_c18_treasurechest",
                3,
                """{"rewardSet":"coralking_1","target":1,"hitBy":"Owner","rewardPlacement":"Inventory"}"""
            ),
            (
                "coralking_r18_goldenchest",
                3,
                """{"rewardSet":"coralking_2","target":1,"hitBy":"Owner","rewardPlacement":"Inventory"}"""
            ),
            ("diamond_c18_giftbox", 3, """{"rewardSet":"diamond18","target":1,"hitBy":"Owner"}"""),
            (
                "easter13_egg_0",
                15,
                """{"rewardSet":"egg_p1","target":1000,"hitBy":"Anyone","incrementalHitAchievement":"EggCracker","finalHitAchievement":"EggMaster"}"""
            ),
            (
                "easter13_egg_1",
                15,
                """{"rewardSet":"egg_p2","target":5000,"hitBy":"Anyone","incrementalHitAchievement":"EggCracker","finalHitAchievement":"EggMaster"}"""
            ),
            (
                "easter13_egg_2",
                15,
                """{"rewardSet":"egg_p3","target":10000,"hitBy":"Anyone","incrementalHitAchievement":"EggCracker","finalHitAchievement":"EggMaster"}"""
            ),
            (
                "easter13_egg_3",
                15,
                """{"rewardSet":"egg_p4","target":20000,"hitBy":"Anyone","incrementalHitAchievement":"EggCracker","finalHitAchievement":"EggMaster"}"""
            ),
            (
                "easter_c17_egg",
                21,
                """{"rewardSet":"easter17_6","target":10,"hitBy":"Anyone","rewardTo":"Cracker"}"""
            ),
            (
                "easter_c17_floweringbush",
                3,
                """{"rewardSet":"easter17_5","target":1,"hitBy":"Owner","requiredEffectId":192,"finalHitAchievement":"Farmer"}"""
            ),
            (
                "easter_c17_leafsprout",
                3,
                """{"rewardSet":"easter17_4","target":1,"hitBy":"Owner","requiredEffectId":192,"finalHitAchievement":"Farmer"}"""
            ),
            (
                "easter_c17_sapling",
                3,
                """{"rewardSet":"easter17_3","target":1,"hitBy":"Owner","requiredEffectId":192}"""
            ),
            ("easter_c17_seedbag", 3, """{"rewardSet":"easter17_1","target":1,"hitBy":"Owner"}"""),
            (
                "easter_c17_seeds",
                3,
                """{"rewardSet":"easter17_2","target":1,"hitBy":"Owner","requiredEffectId":192}"""
            ),
            (
                "easter_c18_lupin1",
                13,
                """{"rewardSet":"easter18_3","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist"}"""
            ),
            (
                "easter_c18_lupin2",
                13,
                """{"rewardSet":"easter18_3","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist"}"""
            ),
            (
                "easter_c18_lupin3",
                13,
                """{"rewardSet":"easter18_3","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist"}"""
            ),
            (
                "easter_c18_lupin4",
                13,
                """{"rewardSet":"easter18_3","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist"}"""
            ),
            (
                "easter_c18_rose1",
                13,
                """{"rewardSet":"easter18_1","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist"}"""
            ),
            (
                "easter_c18_rose2",
                13,
                """{"rewardSet":"easter18_1","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist"}"""
            ),
            (
                "easter_c18_rose3",
                13,
                """{"rewardSet":"easter18_1","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist"}"""
            ),
            (
                "easter_c18_rose4",
                13,
                """{"rewardSet":"easter18_1","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist"}"""
            ),
            (
                "easter_c18_seedpacklupin",
                3,
                """{"rewardSet":"easter18_seed3","target":1,"hitBy":"Owner"}"""
            ),
            (
                "easter_c18_seedpackrose",
                3,
                """{"rewardSet":"easter18_seed1","target":1,"hitBy":"Owner"}"""
            ),
            (
                "easter_c18_seedpacksnowdrop",
                3,
                """{"rewardSet":"easter18_seed4","target":1,"hitBy":"Owner"}"""
            ),
            (
                "easter_c18_seedpacktulip",
                3,
                """{"rewardSet":"easter18_seed2","target":1,"hitBy":"Owner"}"""
            ),
            (
                "easter_c18_snowdrop1",
                13,
                """{"rewardSet":"easter18_4","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist"}"""
            ),
            (
                "easter_c18_snowdrop2",
                13,
                """{"rewardSet":"easter18_4","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist"}"""
            ),
            (
                "easter_c18_snowdrop3",
                13,
                """{"rewardSet":"easter18_4","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist"}"""
            ),
            (
                "easter_c18_snowdrop4",
                13,
                """{"rewardSet":"easter18_4","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist"}"""
            ),
            (
                "easter_c18_tulip1",
                13,
                """{"rewardSet":"easter18_2","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist"}"""
            ),
            (
                "easter_c18_tulip2",
                13,
                """{"rewardSet":"easter18_2","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist"}"""
            ),
            (
                "easter_c18_tulip3",
                13,
                """{"rewardSet":"easter18_2","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist"}"""
            ),
            (
                "easter_c18_tulip4",
                13,
                """{"rewardSet":"easter18_2","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist"}"""
            ),
            (
                "easter_c19_ancientbook",
                3,
                """{"rewardSet":"easter19_5","target":1,"hitBy":"Owner","requiredEffectId":186}"""
            ),
            (
                "easter_c19_babyent",
                25,
                """{"rewardSet":"easter19_3","target":12,"hitBy":"Anyone","requiredEffectId":186,"finalHitAchievement":"EasterCreatures"}"""
            ),
            (
                "easter_c19_babyhippogriff",
                25,
                """{"rewardSet":"easter19_4","target":12,"hitBy":"Anyone","requiredEffectId":186,"finalHitAchievement":"EasterCreatures"}"""
            ),
            (
                "easter_c19_babykelpie",
                25,
                """{"rewardSet":"easter19_2","target":12,"hitBy":"Anyone","requiredEffectId":186,"finalHitAchievement":"EasterCreatures"}"""
            ),
            ("easter_c19_book1", 3, """{"rewardSet":"easter19_6","target":1,"hitBy":"Owner"}"""),
            ("easter_c19_book2", 3, """{"rewardSet":"easter19_7","target":1,"hitBy":"Owner"}"""),
            ("easter_c19_book3", 3, """{"rewardSet":"easter19_8","target":1,"hitBy":"Owner"}"""),
            ("easter_c19_book4", 3, """{"rewardSet":"easter19_9","target":1,"hitBy":"Owner"}"""),
            (
                "easter_c19_forrestegg",
                25,
                """{"rewardSet":"easter19_1","target":12,"hitBy":"Anyone","requiredEffectId":186,"finalHitAchievement":"EasterCreatures"}"""
            ),
            (
                "easter_c20_darkprize1",
                3,
                """{"rewardSet":"easter20_dark1","target":1,"hitBy":"Owner","requiredEffectId":186}"""
            ),
            (
                "easter_c20_darkprize2",
                3,
                """{"rewardSet":"easter20_dark2","target":1,"hitBy":"Owner","requiredEffectId":186}"""
            ),
            (
                "easter_c20_darkprize3",
                3,
                """{"rewardSet":"easter20_dark3","target":1,"hitBy":"Owner","requiredEffectId":186}"""
            ),
            (
                "easter_c20_darkprize4",
                3,
                """{"rewardSet":"easter20_dark4","target":1,"hitBy":"Owner","requiredEffectId":186}"""
            ),
            (
                "easter_c20_darkrock",
                3,
                """{"rewardSet":"easter20_dark0","target":1,"hitBy":"Owner","requiredEffectId":183}"""
            ),
            (
                "easter_c20_lightprize1",
                3,
                """{"rewardSet":"easter20_light1","target":1,"hitBy":"Owner","requiredEffectId":186}"""
            ),
            (
                "easter_c20_lightprize2",
                3,
                """{"rewardSet":"easter20_light2","target":1,"hitBy":"Owner","requiredEffectId":186}"""
            ),
            (
                "easter_c20_lightprize3",
                3,
                """{"rewardSet":"easter20_light3","target":1,"hitBy":"Owner","requiredEffectId":186}"""
            ),
            (
                "easter_c20_lightprize4",
                3,
                """{"rewardSet":"easter20_light4","target":1,"hitBy":"Owner","requiredEffectId":186}"""
            ),
            (
                "easter_c20_lightrock",
                3,
                """{"rewardSet":"easter20_light0","target":1,"hitBy":"Owner","requiredEffectId":183}"""
            ),
            ("easter_r16_crackable", 3, """{"rewardSet":"easter16","target":1,"hitBy":"Owner"}"""),
            (
                "fest_c19_bprintcrackable",
                3,
                """{"rewardSet":"fest19_1","target":1,"hitBy":"Owner"}"""
            ),
            ("gold_rare_crackable", 3, """{"rewardSet":"gold_1","target":1,"hitBy":"Owner"}"""),
            (
                "habbo15_crackable",
                1,
                """{"rewardSet":"habboanv_1","target":1,"hitBy":"Owner","incrementalHitAchievement":"PinataWhacker","finalHitAchievement":"PinataWhacker"}"""
            ),
            (
                "habbo15_pumpkin1",
                3,
                """{"rewardSet":"habbocalypse_1","target":1,"hitBy":"Owner"}"""
            ),
            (
                "habbo15_pumpkin2",
                3,
                """{"rewardSet":"habbocalypse_2","target":1,"hitBy":"Owner"}"""
            ),
            (
                "habbo15_rare_crackable",
                1,
                """{"rewardSet":"habboanv_2","target":1,"hitBy":"Owner","incrementalHitAchievement":"PinataWhacker","finalHitAchievement":"PinataWhacker"}"""
            ),
            (
                "habbo20_c20_crackable",
                3,
                """{"rewardSet":"habbo20_common","target":1,"hitBy":"Owner"}"""
            ),
            (
                "habbo20_r20_crackable",
                3,
                """{"rewardSet":"habbo20_rare","target":1,"hitBy":"Owner"}"""
            ),
            (
                "hblooza14_pinata1",
                9,
                """{"rewardSet":"pinata1","target":100,"hitBy":"Anyone","incrementalHitAchievement":"PinataWhacker","finalHitAchievement":"PinataBreaker"}"""
            ),
            (
                "hblooza14_pinata2",
                9,
                """{"rewardSet":"pinata1","target":100,"hitBy":"Anyone","incrementalHitAchievement":"PinataWhacker","finalHitAchievement":"PinataBreaker"}"""
            ),
            (
                "hblooza14_pinata3",
                9,
                """{"rewardSet":"pinata1","target":100,"hitBy":"Anyone","incrementalHitAchievement":"PinataWhacker","finalHitAchievement":"PinataBreaker"}"""
            ),
            (
                "hblooza14_pinata4",
                9,
                """{"rewardSet":"green_pinata","target":100,"hitBy":"Anyone","incrementalHitAchievement":"PinataWhacker","finalHitAchievement":"PinataBreaker"}"""
            ),
            (
                "hblooza_pinata1",
                9,
                """{"rewardSet":"pinata1","target":100,"hitBy":"Anyone","incrementalHitAchievement":"pinatawhacker","finalHitAchievement":"pinatabreaker"}"""
            ),
            (
                "hblooza_pinata2",
                9,
                """{"rewardSet":"pinata2","target":100,"hitBy":"Anyone","incrementalHitAchievement":"pinatawhacker","finalHitAchievement":"pinatabreaker"}"""
            ),
            (
                "hc_gift_14days",
                3,
                """{"rewardSet":"hcgift_1","target":1,"hitBy":"Owner","subscription":"HabboClub","subscriptionDays":14}"""
            ),
            (
                "hc_gift_31days",
                3,
                """{"rewardSet":"hcgift_2","target":1,"hitBy":"Owner","subscription":"HabboClub","subscriptionDays":31}"""
            ),
            (
                "hhistory_r16_crackable",
                3,
                """{"rewardSet":"hhistory_16","target":1,"hitBy":"Owner"}"""
            ),
            (
                "hhistory_r17_crackable",
                3,
                """{"rewardSet":"hhistory_17","target":1,"hitBy":"Owner"}"""
            ),
            (
                "hhistory_r18_crackable",
                3,
                """{"rewardSet":"hhistory_18","target":1,"hitBy":"Owner"}"""
            ),
            (
                "hween_c15_pumpkin1",
                3,
                """{"rewardSet":"habbocalypse_1","target":1,"hitBy":"Owner"}"""
            ),
            (
                "hween_c15_pumpkin2",
                3,
                """{"rewardSet":"habbocalypse_2","target":1,"hitBy":"Owner"}"""
            ),
            (
                "hween_c16_crackable1",
                21,
                """{"rewardSet":"habboanv_1","target":10,"hitBy":"Anyone","rewardTo":"Cracker"}"""
            ),
            (
                "hween_c17_flamingknight",
                21,
                """{"rewardSet":"hween17","target":10,"hitBy":"Anyone","finalHitAchievement":"flamingknight"}"""
            ),
            (
                "hween_c19_witchsatchel",
                3,
                """{"rewardSet":"hween19_witchsatchel","target":1,"hitBy":"Owner"}"""
            ),
            (
                "hween_c20_duckgoddess",
                23,
                """{"rewardSet":"hween20_6","target":11,"hitBy":"Anyone","requiredEffectId":186}"""
            ),
            (
                "hween_c20_evilscarecrow",
                23,
                """{"rewardSet":"hween20_5","target":11,"hitBy":"Anyone","requiredEffectId":5}"""
            ),
            (
                "hween_c20_eyedemon",
                23,
                """{"rewardSet":"hween20_4","target":11,"hitBy":"Anyone","requiredEffectId":162}"""
            ),
            (
                "hween_c20_octodemon",
                23,
                """{"rewardSet":"hween20_3","target":11,"hitBy":"Anyone","requiredEffectId":117}"""
            ),
            ("hween_c20_pandorabox", 3, """{"rewardSet":"hween20_1","target":1,"hitBy":"Owner"}"""),
            (
                "hween_r16_crackable2",
                21,
                """{"rewardSet":"habboanv_1","target":10,"hitBy":"Anyone"}"""
            ),
            (
                "hween_r20_evilpandorabox",
                3,
                """{"rewardSet":"hween20_2","target":1,"hitBy":"Owner"}"""
            ),
            ("india_c20_blueprint", 3, """{"rewardSet":"india20_1","target":1,"hitBy":"Owner"}"""),
            (
                "jungle_c16_flowera1",
                13,
                """{"rewardSet":"jung16_1","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"Horticulturist"}"""
            ),
            (
                "jungle_c16_flowera2",
                13,
                """{"rewardSet":"jung16_1","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"Horticulturist"}"""
            ),
            (
                "jungle_c16_flowera3",
                13,
                """{"rewardSet":"jung16_1","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"Horticulturist"}"""
            ),
            (
                "jungle_c16_flowerb1",
                13,
                """{"rewardSet":"jung16_2","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"Horticulturist"}"""
            ),
            (
                "jungle_c16_flowerb2",
                13,
                """{"rewardSet":"jung16_2","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"Horticulturist"}"""
            ),
            (
                "jungle_c16_flowerb3",
                13,
                """{"rewardSet":"jung16_2","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"Horticulturist"}"""
            ),
            (
                "jungle_c16_flowerc1",
                13,
                """{"rewardSet":"jung16_3","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"Horticulturist"}"""
            ),
            (
                "jungle_c16_flowerc2",
                13,
                """{"rewardSet":"jung16_3","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"Horticulturist"}"""
            ),
            (
                "jungle_c16_flowerc3",
                13,
                """{"rewardSet":"jung16_3","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"Horticulturist"}"""
            ),
            (
                "jungle_c16_flowerd1",
                13,
                """{"rewardSet":"jung16_4","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"Horticulturist"}"""
            ),
            (
                "jungle_c16_flowerd2",
                13,
                """{"rewardSet":"jung16_4","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"Horticulturist"}"""
            ),
            (
                "jungle_c16_flowerd3",
                13,
                """{"rewardSet":"jung16_4","target":12,"hitBy":"Anyone","requiredEffectId":192,"finalHitAchievement":"Horticulturist"}"""
            ),
            (
                "mystics_crystal_l",
                15,
                """{"rewardSet":"crystal_p1","target":1500000,"hitBy":"Anyone","incrementalHitAchievement":"CrystalCracker","finalHitAchievement":"CrystalLegend"}"""
            ),
            (
                "mystics_crystal_m",
                15,
                """{"rewardSet":"crystal_p1","target":1000000,"hitBy":"Anyone","incrementalHitAchievement":"CrystalCracker","finalHitAchievement":"CrystalLegend"}"""
            ),
            (
                "mystics_crystal_s",
                15,
                """{"rewardSet":"crystal_p1","target":500000,"hitBy":"Anyone","incrementalHitAchievement":"CrystalCracker","finalHitAchievement":"CrystalLegend"}"""
            ),
            ("ny16_crackable", 3, """{"rewardSet":"ny16_1","target":1,"hitBy":"Owner"}"""),
            ("ny17_crackable", 3, """{"rewardSet":"ny17_1","target":1,"hitBy":"Owner"}"""),
            ("ny18_crackable", 3, """{"rewardSet":"ny18_1","target":1,"hitBy":"Owner"}"""),
            ("ny_r18_crackable", 3, """{"rewardSet":"ny_r18","target":1,"hitBy":"Owner"}"""),
            ("ny_r19_crackable", 3, """{"rewardSet":"ny_r19","target":1,"hitBy":"Owner"}"""),
            ("ny_r20_crackable", 3, """{"rewardSet":"ny_r20","target":1,"hitBy":"Owner"}"""),
            (
                "plushie_c20_crackable",
                3,
                """{"rewardSet":"plushie20_1","target":1,"hitBy":"Owner","rewardPlacement":"Inventory"}"""
            ),
            (
                "santorini_c17_artefact1",
                13,
                """{"rewardSet":"santorini_1","target":12,"hitBy":"Anyone","requiredEffectId":186,"finalHitAchievement":"Restorer"}"""
            ),
            (
                "santorini_c17_artefact2",
                13,
                """{"rewardSet":"santorini_2","target":12,"hitBy":"Anyone","requiredEffectId":186,"finalHitAchievement":"Restorer"}"""
            ),
            (
                "santorini_c17_artefact3",
                13,
                """{"rewardSet":"santorini_3","target":12,"hitBy":"Anyone","requiredEffectId":186,"finalHitAchievement":"Restorer"}"""
            ),
            (
                "santorini_c17_artefact4",
                13,
                """{"rewardSet":"santorini_4","target":12,"hitBy":"Anyone","requiredEffectId":186,"finalHitAchievement":"Restorer"}"""
            ),
            (
                "santorini_c17_artefact5",
                13,
                """{"rewardSet":"santorini_5","target":12,"hitBy":"Anyone","requiredEffectId":186,"finalHitAchievement":"Restorer"}"""
            ),
            (
                "santorini_r17_chest",
                3,
                """{"rewardSet":"santorini_6","target":1,"hitBy":"Owner"}"""
            ),
            ("tokyo_c18_gacha", 3, """{"rewardSet":"tokyo_1","target":1,"hitBy":"Owner"}"""),
            (
                "xmas_c16_creature1",
                25,
                """{"rewardSet":"xmas16_2","target":12,"hitBy":"Anyone","requiredEffectId":186,"finalHitAchievement":"CreatureRearer","finalHitAchievementCount":5}"""
            ),
            (
                "xmas_c16_creature4",
                25,
                """{"rewardSet":"xmas16_3","target":12,"hitBy":"Anyone","requiredEffectId":186,"finalHitAchievement":"CreatureRearer","finalHitAchievementCount":10}"""
            ),
            (
                "xmas_c16_creature7",
                25,
                """{"rewardSet":"xmas16_4","target":12,"hitBy":"Anyone","requiredEffectId":186,"finalHitAchievement":"CreatureRearer","finalHitAchievementCount":15}"""
            ),
            (
                "xmas_c16_egg",
                25,
                """{"rewardSet":"xmas16_1","target":12,"hitBy":"Anyone","requiredEffectId":186,"finalHitAchievement":"CreatureRearer","finalHitAchievementCount":5}"""
            ),
            ("xmas_c16_stocking", 3, """{"rewardSet":"xmas16_5","target":1,"hitBy":"Owner"}"""),
            ("xmas_c17_book", 3, """{"rewardSet":"xmas17_1","target":1,"hitBy":"Owner"}"""),
            ("xmas_c18_doll1", 3, """{"rewardSet":"xmas18_1","target":1,"hitBy":"Owner"}"""),
            ("xmas_c18_doll10", 3, """{"rewardSet":"xmas18_10","target":1,"hitBy":"Owner"}"""),
            ("xmas_c18_doll2", 3, """{"rewardSet":"xmas18_2","target":1,"hitBy":"Owner"}"""),
            ("xmas_c18_doll3", 3, """{"rewardSet":"xmas18_3","target":1,"hitBy":"Owner"}"""),
            ("xmas_c18_doll4", 3, """{"rewardSet":"xmas18_4","target":1,"hitBy":"Owner"}"""),
            ("xmas_c18_doll5", 3, """{"rewardSet":"xmas18_5","target":1,"hitBy":"Owner"}"""),
            ("xmas_c18_doll6", 3, """{"rewardSet":"xmas18_6","target":1,"hitBy":"Owner"}"""),
            ("xmas_c18_doll7", 3, """{"rewardSet":"xmas18_7","target":1,"hitBy":"Owner"}"""),
            ("xmas_c18_doll8", 3, """{"rewardSet":"xmas18_8","target":1,"hitBy":"Owner"}"""),
            ("xmas_c18_doll9", 3, """{"rewardSet":"xmas18_9","target":1,"hitBy":"Owner"}"""),
            ("xmas_c19_box1", 3, """{"rewardSet":"xmas19_box1","target":1,"hitBy":"Owner"}"""),
            ("xmas_c19_box2", 3, """{"rewardSet":"xmas19_box2","target":1,"hitBy":"Owner"}"""),
            ("xmas_c19_box3", 3, """{"rewardSet":"xmas19_box3","target":1,"hitBy":"Owner"}"""),
            ("xmas_c19_box4", 3, """{"rewardSet":"xmas19_box4","target":1,"hitBy":"Owner"}"""),
            ("xmas_c19_box5", 3, """{"rewardSet":"xmas19_box5","target":1,"hitBy":"Owner"}"""),
            ("xmas_c19_box6", 3, """{"rewardSet":"xmas19_box6","target":1,"hitBy":"Owner"}"""),
            ("xmas_c20_runerock", 3, """{"rewardSet":"xmas20_1","target":1,"hitBy":"Owner"}"""),
            ("xmas_c20_runerockblue", 3, """{"rewardSet":"xmas20_2","target":1,"hitBy":"Owner"}"""),
            (
                "xmas_c20_runerockgreen",
                3,
                """{"rewardSet":"xmas20_3","target":1,"hitBy":"Owner"}"""
            ),
            (
                "xmas_c20_runerockpurple",
                3,
                """{"rewardSet":"xmas20_4","target":1,"hitBy":"Owner"}"""
            ),
            ("xmas_c20_runerockred", 3, """{"rewardSet":"xmas20_5","target":1,"hitBy":"Owner"}"""),
            (
                "xmas_c20_runerockyellow",
                3,
                """{"rewardSet":"xmas20_6","target":1,"hitBy":"Owner"}"""
            ),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, states, crackable) in CRACKABLES)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `logic` = '{LOGIC}', `total_states` = {states}, "
                        + $"`extra_data` = IF(JSON_VALID(`extra_data`), JSON_SET(`extra_data`, '$.crackable', JSON_EXTRACT('{crackable}', '$')), "
                        + $"JSON_OBJECT('crackable', JSON_EXTRACT('{crackable}', '$'))) WHERE `name` = '{name}';"
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                $"UPDATE `furniture_definitions` SET `logic` = 'default_floor', `extra_data` = "
                    + "IF(JSON_VALID(`extra_data`) AND JSON_LENGTH(JSON_REMOVE(`extra_data`, '$.crackable')) > 0, JSON_REMOVE(`extra_data`, '$.crackable'), "
                    + $"IF(JSON_VALID(`extra_data`), NULL, `extra_data`)) WHERE `logic` = '{LOGIC}';"
            );
    }
}
