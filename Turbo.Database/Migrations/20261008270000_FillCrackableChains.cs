using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Fills what <c>RefineCrackableFurni</c> left empty for 81 crackables, from the Habbox Wiki's
    /// campaign pages and the hotel's own texts, keeping the rest of each one's section:
    /// <list type="bullet">
    /// <item>growth and evolution chains, where cracking gives the next stage, placed where it
    /// stood: Farm's Bag of Seeds to the berry bushes and carrots (the furnidata's "Grown from"
    /// descriptions; Farmer is "for fully cultivating Bag of Seeds"), Easter Garden's seed packs
    /// and patches and Stranded Jungle's flowers ("each time you grow them, there is a chance they
    /// will bloom in one of three colours ... or grow into a Rafflesia"; the Grubby Weed is Easter
    /// Garden's), Christmas Citadel's creatures (33.33% / 50%), Fairytale Easter's storybooks
    /// (45 / 45 / 10) and Enchanted Egg (75 / 25), the Matroyoshka dolls ("each time they were
    /// cracked they would change into a different type of doll"), the Rune Rocks, and Winter
    /// Palace's boxes (50% the next box, 50% an ice carving; the North Star Box always a Crown of
    /// Frost);</item>
    /// <item>boxes of one item: the Cursed Flame Knight's six armour pieces, the Ingredients
    /// Pouch's seven ingredients, the Book of Patterns (25% each), the Indian Blueprint Book (20%
    /// each), the Tailor's Handbook's six designs;</item>
    /// <item>several draws at once (<c>draws</c>): a Coral Kingdom Chest's three commons and one
    /// more at 60 / 30 / 10% common, uncommon, rare (the Magic chest's 70 / 30% uncommon, rare),
    /// the Plushie Crafting Box's stuffing, dye, two fabrics and a dyed fabric, by the campaign's
    /// own illustrations.</item>
    /// </list>
    /// Where a source names the outcomes but not their odds they are equally likely (which carving
    /// a Winter Palace box holds is not given either, so each is one of the five). Only a
    /// definition that is crackable and has its section is changed. Data only: the model is
    /// unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261008270000_FillCrackableChains")]
    public partial class FillCrackableChains : Migration
    {
        /// <summary>Name, and what to merge into its <c>crackable</c> section.</summary>
        private static readonly (string Name, string Contents)[] CONTENTS =
        [
            (
                "coralking_c18_treasurechest",
                """{"draws":[{"count":3,"rewards":[{"furni":"coralking_c18_spinycoral1"},{"furni":"coralking_c18_spinycoral2"},{"furni":"coralking_c18_bushycoral1"},{"furni":"coralking_c18_bushycoral2"},{"furni":"coralking_c18_closedspiral1"},{"furni":"coralking_c18_closedspiral2"},{"furni":"coralking_c18_starfish1"},{"furni":"coralking_c18_starfish2"},{"furni":"coralking_c18_seaweed"},{"furni":"coralking_c18_openspiral1"},{"furni":"coralking_c18_openspiral2"},{"furni":"coralking_c18_cone"},{"furni":"coralking_c18_clamshell1"},{"furni":"coralking_c18_clamshell2"},{"furni":"coralking_c18_clamshell3"}]},{"rewards":[{"furni":"coralking_c18_spinycoral1","weight":8},{"furni":"coralking_c18_spinycoral2","weight":8},{"furni":"coralking_c18_bushycoral1","weight":8},{"furni":"coralking_c18_bushycoral2","weight":8},{"furni":"coralking_c18_closedspiral1","weight":8},{"furni":"coralking_c18_closedspiral2","weight":8},{"furni":"coralking_c18_starfish1","weight":8},{"furni":"coralking_c18_starfish2","weight":8},{"furni":"coralking_c18_seaweed","weight":8},{"furni":"coralking_c18_openspiral1","weight":8},{"furni":"coralking_c18_openspiral2","weight":8},{"furni":"coralking_c18_cone","weight":8},{"furni":"coralking_c18_clamshell1","weight":8},{"furni":"coralking_c18_clamshell2","weight":8},{"furni":"coralking_c18_clamshell3","weight":8},{"furni":"coralking_c18_pearloyster","weight":15},{"furni":"coralking_c18_goldenfish","weight":15},{"furni":"coralking_c18_chalice","weight":15},{"furni":"coralking_c18_trident","weight":15},{"furni":"clothing_r18_seawreath","weight":10},{"furni":"clothing_r18_goldfish","weight":10}]}]}"""
            ),
            (
                "coralking_r18_goldenchest",
                """{"draws":[{"count":3,"rewards":[{"furni":"coralking_c18_spinycoral1"},{"furni":"coralking_c18_spinycoral2"},{"furni":"coralking_c18_bushycoral1"},{"furni":"coralking_c18_bushycoral2"},{"furni":"coralking_c18_closedspiral1"},{"furni":"coralking_c18_closedspiral2"},{"furni":"coralking_c18_starfish1"},{"furni":"coralking_c18_starfish2"},{"furni":"coralking_c18_seaweed"},{"furni":"coralking_c18_openspiral1"},{"furni":"coralking_c18_openspiral2"},{"furni":"coralking_c18_cone"},{"furni":"coralking_c18_clamshell1"},{"furni":"coralking_c18_clamshell2"},{"furni":"coralking_c18_clamshell3"}]},{"rewards":[{"furni":"coralking_c18_pearloyster","weight":35},{"furni":"coralking_c18_goldenfish","weight":35},{"furni":"coralking_c18_chalice","weight":35},{"furni":"coralking_c18_trident","weight":35},{"furni":"clothing_r18_seawreath","weight":30},{"furni":"clothing_r18_goldfish","weight":30}]}]}"""
            ),
            (
                "easter_c17_egg",
                """{"rewards":[{"furni":"easter_c17_choc"},{"furni":"easter_c17_flour"}]}"""
            ),
            (
                "easter_c17_floweringbush",
                """{"rewards":[{"furni":"easter_c17_strawbsbush"},{"furni":"easter_c17_raspbush"},{"furni":"easter_c17_blkberrybush"}]}"""
            ),
            ("easter_c17_leafsprout", """{"rewards":[{"furni":"easter_c17_carrot"}]}"""),
            ("easter_c17_sapling", """{"rewards":[{"furni":"easter_c17_floweringbush"}]}"""),
            ("easter_c17_seedbag", """{"rewards":[{"furni":"easter_c17_seeds"}]}"""),
            (
                "easter_c17_seeds",
                """{"rewards":[{"furni":"easter_c17_sapling"},{"furni":"easter_c17_leafsprout"}]}"""
            ),
            (
                "easter_c18_lupin1",
                """{"rewards":[{"furni":"easter_c18_lupin1"},{"furni":"easter_c18_lupin2"},{"furni":"easter_c18_lupin3"},{"furni":"easter_c18_lupin4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_lupin2",
                """{"rewards":[{"furni":"easter_c18_lupin1"},{"furni":"easter_c18_lupin2"},{"furni":"easter_c18_lupin3"},{"furni":"easter_c18_lupin4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_lupin3",
                """{"rewards":[{"furni":"easter_c18_lupin1"},{"furni":"easter_c18_lupin2"},{"furni":"easter_c18_lupin3"},{"furni":"easter_c18_lupin4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_lupin4",
                """{"rewards":[{"furni":"easter_c18_lupin1"},{"furni":"easter_c18_lupin2"},{"furni":"easter_c18_lupin3"},{"furni":"easter_c18_lupin4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_rose1",
                """{"rewards":[{"furni":"easter_c18_rose1"},{"furni":"easter_c18_rose2"},{"furni":"easter_c18_rose3"},{"furni":"easter_c18_rose4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_rose2",
                """{"rewards":[{"furni":"easter_c18_rose1"},{"furni":"easter_c18_rose2"},{"furni":"easter_c18_rose3"},{"furni":"easter_c18_rose4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_rose3",
                """{"rewards":[{"furni":"easter_c18_rose1"},{"furni":"easter_c18_rose2"},{"furni":"easter_c18_rose3"},{"furni":"easter_c18_rose4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_rose4",
                """{"rewards":[{"furni":"easter_c18_rose1"},{"furni":"easter_c18_rose2"},{"furni":"easter_c18_rose3"},{"furni":"easter_c18_rose4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_seedpacklupin",
                """{"rewards":[{"furni":"easter_c18_lupin1"},{"furni":"easter_c18_lupin2"},{"furni":"easter_c18_lupin3"},{"furni":"easter_c18_lupin4"}]}"""
            ),
            (
                "easter_c18_seedpackrose",
                """{"rewards":[{"furni":"easter_c18_rose1"},{"furni":"easter_c18_rose2"},{"furni":"easter_c18_rose3"},{"furni":"easter_c18_rose4"}]}"""
            ),
            (
                "easter_c18_seedpacksnowdrop",
                """{"rewards":[{"furni":"easter_c18_snowdrop1"},{"furni":"easter_c18_snowdrop2"},{"furni":"easter_c18_snowdrop3"},{"furni":"easter_c18_snowdrop4"}]}"""
            ),
            (
                "easter_c18_seedpacktulip",
                """{"rewards":[{"furni":"easter_c18_tulip1"},{"furni":"easter_c18_tulip2"},{"furni":"easter_c18_tulip3"},{"furni":"easter_c18_tulip4"}]}"""
            ),
            (
                "easter_c18_snowdrop1",
                """{"rewards":[{"furni":"easter_c18_snowdrop1"},{"furni":"easter_c18_snowdrop2"},{"furni":"easter_c18_snowdrop3"},{"furni":"easter_c18_snowdrop4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_snowdrop2",
                """{"rewards":[{"furni":"easter_c18_snowdrop1"},{"furni":"easter_c18_snowdrop2"},{"furni":"easter_c18_snowdrop3"},{"furni":"easter_c18_snowdrop4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_snowdrop3",
                """{"rewards":[{"furni":"easter_c18_snowdrop1"},{"furni":"easter_c18_snowdrop2"},{"furni":"easter_c18_snowdrop3"},{"furni":"easter_c18_snowdrop4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_snowdrop4",
                """{"rewards":[{"furni":"easter_c18_snowdrop1"},{"furni":"easter_c18_snowdrop2"},{"furni":"easter_c18_snowdrop3"},{"furni":"easter_c18_snowdrop4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_tulip1",
                """{"rewards":[{"furni":"easter_c18_tulip1"},{"furni":"easter_c18_tulip2"},{"furni":"easter_c18_tulip3"},{"furni":"easter_c18_tulip4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_tulip2",
                """{"rewards":[{"furni":"easter_c18_tulip1"},{"furni":"easter_c18_tulip2"},{"furni":"easter_c18_tulip3"},{"furni":"easter_c18_tulip4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_tulip3",
                """{"rewards":[{"furni":"easter_c18_tulip1"},{"furni":"easter_c18_tulip2"},{"furni":"easter_c18_tulip3"},{"furni":"easter_c18_tulip4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_tulip4",
                """{"rewards":[{"furni":"easter_c18_tulip1"},{"furni":"easter_c18_tulip2"},{"furni":"easter_c18_tulip3"},{"furni":"easter_c18_tulip4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c19_ancientbook",
                """{"rewards":[{"furni":"easter_c19_book1"},{"furni":"easter_c19_book2"},{"furni":"easter_c19_book3"},{"furni":"easter_c19_book4"}]}"""
            ),
            (
                "easter_c19_babyent",
                """{"rewards":[{"furni":"easter_c19_ent","weight":75},{"furni":"easter_c19_earthdrago","weight":25}]}"""
            ),
            (
                "easter_c19_babyhippogriff",
                """{"rewards":[{"furni":"easter_c19_bearowl","weight":75},{"furni":"easter_c19_hippogriff","weight":25}]}"""
            ),
            (
                "easter_c19_babykelpie",
                """{"rewards":[{"furni":"easter_c19_kelpie","weight":75},{"furni":"easter_c19_waterdrago","weight":25}]}"""
            ),
            (
                "easter_c19_book1",
                """{"rewards":[{"furni":"easter_c19_wolf","weight":45},{"furni":"easter_c19_lilredbonnie","weight":45},{"furni":"clothing_wolfmask","weight":10}]}"""
            ),
            (
                "easter_c19_book2",
                """{"rewards":[{"furni":"easter_c19_habshirecat","weight":45},{"furni":"easter_c19_busybunny","weight":45},{"furni":"clothing_madhat","weight":10}]}"""
            ),
            (
                "easter_c19_book3",
                """{"rewards":[{"furni":"easter_c19_woodlandcritters","weight":45},{"furni":"easter_c19_chillgnome","weight":45},{"furni":"clothing_ribboncurls","weight":10}]}"""
            ),
            (
                "easter_c19_book4",
                """{"rewards":[{"furni":"easter_c19_habelina","weight":45},{"furni":"easter_c19_fairyprince","weight":45},{"furni":"clothing_flowerponytail","weight":10}]}"""
            ),
            (
                "easter_c19_forrestegg",
                """{"rewards":[{"furni":"easter_c19_babyent"},{"furni":"easter_c19_babyhippogriff"},{"furni":"easter_c19_babykelpie"}]}"""
            ),
            (
                "fest_c19_bprintcrackable",
                """{"rewards":[{"furni":"fest_c19_bprint1"},{"furni":"fest_c19_bprint2"},{"furni":"fest_c19_bprint3"},{"furni":"fest_c19_bprint4"}]}"""
            ),
            (
                "hween_c17_flamingknight",
                """{"rewards":[{"furni":"clothing_rebelchest"},{"furni":"clothing_herochest"},{"furni":"clothing_shoearmour"},{"furni":"clothing_badasshelmet"},{"furni":"clothing_herohelmet"},{"furni":"clothing_legarmour"}]}"""
            ),
            (
                "hween_c19_witchsatchel",
                """{"rewards":[{"furni":"hween_c19_bewitchedcandles"},{"furni":"hween_c19_herbs"},{"furni":"hween_c19_bewitchedskull"},{"furni":"hween_c19_crystalball"},{"furni":"hween_c19_feathers"},{"furni":"hween_c19_crystal"},{"furni":"hween_c19_tarot"}]}"""
            ),
            (
                "india_c20_blueprint",
                """{"rewards":[{"furni":"india_c20_capebp"},{"furni":"india_c20_headjewelbp"},{"furni":"india_c20_saribp"},{"furni":"india_c20_sherwanibp"},{"furni":"india_c20_snakebp"}]}"""
            ),
            (
                "jungle_c16_flowera1",
                """{"rewards":[{"furni":"jungle_c16_flowera1"},{"furni":"jungle_c16_flowera2"},{"furni":"jungle_c16_flowera3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowera2",
                """{"rewards":[{"furni":"jungle_c16_flowera1"},{"furni":"jungle_c16_flowera2"},{"furni":"jungle_c16_flowera3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowera3",
                """{"rewards":[{"furni":"jungle_c16_flowera1"},{"furni":"jungle_c16_flowera2"},{"furni":"jungle_c16_flowera3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerb1",
                """{"rewards":[{"furni":"jungle_c16_flowerb1"},{"furni":"jungle_c16_flowerb2"},{"furni":"jungle_c16_flowerb3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerb2",
                """{"rewards":[{"furni":"jungle_c16_flowerb1"},{"furni":"jungle_c16_flowerb2"},{"furni":"jungle_c16_flowerb3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerb3",
                """{"rewards":[{"furni":"jungle_c16_flowerb1"},{"furni":"jungle_c16_flowerb2"},{"furni":"jungle_c16_flowerb3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerc1",
                """{"rewards":[{"furni":"jungle_c16_flowerc1"},{"furni":"jungle_c16_flowerc2"},{"furni":"jungle_c16_flowerc3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerc2",
                """{"rewards":[{"furni":"jungle_c16_flowerc1"},{"furni":"jungle_c16_flowerc2"},{"furni":"jungle_c16_flowerc3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerc3",
                """{"rewards":[{"furni":"jungle_c16_flowerc1"},{"furni":"jungle_c16_flowerc2"},{"furni":"jungle_c16_flowerc3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerd1",
                """{"rewards":[{"furni":"jungle_c16_flowerd1"},{"furni":"jungle_c16_flowerd2"},{"furni":"jungle_c16_flowerd3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerd2",
                """{"rewards":[{"furni":"jungle_c16_flowerd1"},{"furni":"jungle_c16_flowerd2"},{"furni":"jungle_c16_flowerd3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerd3",
                """{"rewards":[{"furni":"jungle_c16_flowerd1"},{"furni":"jungle_c16_flowerd2"},{"furni":"jungle_c16_flowerd3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "plushie_c20_crackable",
                """{"draws":[{"rewards":[{"furni":"plushie_c20_stuffing"}]},{"rewards":[{"furni":"plushie_c20_dyeneutral","weight":9},{"furni":"plushie_c20_dyepink","weight":9},{"furni":"plushie_c20_dyeblue","weight":9},{"furni":"plushie_c20_dyerainbow"}]},{"rewards":[{"furni":"plushie_c20_fabric3","also":["plushie_c20_fabric3"],"weight":9},{"furni":"plushie_c20_fabric1","also":["plushie_c20_fabric1"],"weight":10},{"furni":"plushie_c20_fabric2","also":["plushie_c20_fabric2"],"weight":9}]},{"rewards":[{"furni":"plushie_c20_fabric3","also":["plushie_c20_dyeneutral"],"weight":9},{"furni":"plushie_c20_fabric1","also":["plushie_c20_dyepink"],"weight":10},{"furni":"plushie_c20_fabric2","also":["plushie_c20_dyeblue"],"weight":9}]}]}"""
            ),
            (
                "xmas_c16_creature1",
                """{"rewards":[{"furni":"xmas_c16_creature2"},{"furni":"xmas_c16_creature3"}]}"""
            ),
            (
                "xmas_c16_creature4",
                """{"rewards":[{"furni":"xmas_c16_creature5"},{"furni":"xmas_c16_creature6"}]}"""
            ),
            (
                "xmas_c16_creature7",
                """{"rewards":[{"furni":"xmas_c16_creature8"},{"furni":"xmas_c16_creature9"}]}"""
            ),
            (
                "xmas_c16_egg",
                """{"rewards":[{"furni":"xmas_c16_creature1"},{"furni":"xmas_c16_creature4"},{"furni":"xmas_c16_creature7"}]}"""
            ),
            (
                "xmas_c17_book",
                """{"rewards":[{"furni":"xmas_c17_blueprint1"},{"furni":"xmas_c17_blueprint2"},{"furni":"xmas_c17_blueprint3"},{"furni":"xmas_c17_blueprint4"},{"furni":"xmas_c17_blueprint5"},{"furni":"xmas_c17_blueprint6"}]}"""
            ),
            (
                "xmas_c18_doll1",
                """{"rewards":[{"furni":"xmas_c18_doll2"},{"furni":"xmas_c18_doll3"},{"furni":"xmas_c18_doll4"},{"furni":"xmas_c18_doll5"}]}"""
            ),
            (
                "xmas_c18_doll10",
                """{"rewards":[{"furni":"xmas_c18_doll6"},{"furni":"xmas_c18_doll7"},{"furni":"xmas_c18_doll8"},{"furni":"xmas_c18_doll9"}]}"""
            ),
            (
                "xmas_c18_doll2",
                """{"rewards":[{"furni":"xmas_c18_doll1"},{"furni":"xmas_c18_doll3"},{"furni":"xmas_c18_doll4"},{"furni":"xmas_c18_doll5"}]}"""
            ),
            (
                "xmas_c18_doll3",
                """{"rewards":[{"furni":"xmas_c18_doll1"},{"furni":"xmas_c18_doll2"},{"furni":"xmas_c18_doll4"},{"furni":"xmas_c18_doll5"}]}"""
            ),
            (
                "xmas_c18_doll4",
                """{"rewards":[{"furni":"xmas_c18_doll1"},{"furni":"xmas_c18_doll2"},{"furni":"xmas_c18_doll3"},{"furni":"xmas_c18_doll5"}]}"""
            ),
            (
                "xmas_c18_doll5",
                """{"rewards":[{"furni":"xmas_c18_doll1"},{"furni":"xmas_c18_doll2"},{"furni":"xmas_c18_doll3"},{"furni":"xmas_c18_doll4"}]}"""
            ),
            (
                "xmas_c18_doll6",
                """{"rewards":[{"furni":"xmas_c18_doll7"},{"furni":"xmas_c18_doll8"},{"furni":"xmas_c18_doll9"},{"furni":"xmas_c18_doll10"}]}"""
            ),
            (
                "xmas_c18_doll7",
                """{"rewards":[{"furni":"xmas_c18_doll6"},{"furni":"xmas_c18_doll8"},{"furni":"xmas_c18_doll9"},{"furni":"xmas_c18_doll10"}]}"""
            ),
            (
                "xmas_c18_doll8",
                """{"rewards":[{"furni":"xmas_c18_doll6"},{"furni":"xmas_c18_doll7"},{"furni":"xmas_c18_doll9"},{"furni":"xmas_c18_doll10"}]}"""
            ),
            (
                "xmas_c18_doll9",
                """{"rewards":[{"furni":"xmas_c18_doll6"},{"furni":"xmas_c18_doll7"},{"furni":"xmas_c18_doll8"},{"furni":"xmas_c18_doll10"}]}"""
            ),
            (
                "xmas_c19_box1",
                """{"rewards":[{"furni":"xmas_c19_box2","weight":5},{"furni":"xmas_c19_angelfigure"},{"furni":"xmas_c19_dragonfigure"},{"furni":"xmas_c19_reindeerfigure"},{"furni":"xmas_c19_robinfigure"},{"furni":"xmas_c19_unicornfigure"}]}"""
            ),
            (
                "xmas_c19_box2",
                """{"rewards":[{"furni":"xmas_c19_box3","weight":5},{"furni":"xmas_c19_angelfigure"},{"furni":"xmas_c19_dragonfigure"},{"furni":"xmas_c19_reindeerfigure"},{"furni":"xmas_c19_robinfigure"},{"furni":"xmas_c19_unicornfigure"}]}"""
            ),
            (
                "xmas_c19_box3",
                """{"rewards":[{"furni":"xmas_c19_box4","weight":5},{"furni":"xmas_c19_angelfigure"},{"furni":"xmas_c19_dragonfigure"},{"furni":"xmas_c19_reindeerfigure"},{"furni":"xmas_c19_robinfigure"},{"furni":"xmas_c19_unicornfigure"}]}"""
            ),
            (
                "xmas_c19_box4",
                """{"rewards":[{"furni":"xmas_c19_box5","weight":5},{"furni":"xmas_c19_angelfigure"},{"furni":"xmas_c19_dragonfigure"},{"furni":"xmas_c19_reindeerfigure"},{"furni":"xmas_c19_robinfigure"},{"furni":"xmas_c19_unicornfigure"}]}"""
            ),
            (
                "xmas_c19_box5",
                """{"rewards":[{"furni":"xmas_c19_box6","weight":5},{"furni":"xmas_c19_angelfigure"},{"furni":"xmas_c19_dragonfigure"},{"furni":"xmas_c19_reindeerfigure"},{"furni":"xmas_c19_robinfigure"},{"furni":"xmas_c19_unicornfigure"}]}"""
            ),
            ("xmas_c19_box6", """{"rewards":[{"furni":"clothing_icecrown"}]}"""),
            (
                "xmas_c20_runerock",
                """{"rewards":[{"furni":"xmas_c20_runerockpurple"},{"furni":"xmas_c20_runerockblue"},{"furni":"xmas_c20_runerockyellow"},{"furni":"xmas_c20_runerockred"},{"furni":"xmas_c20_runerockgreen"}]}"""
            ),
            (
                "xmas_c20_runerockblue",
                """{"rewards":[{"furni":"xmas_c20_runerockpurple"},{"furni":"xmas_c20_runerockyellow"},{"furni":"xmas_c20_runerockred"},{"furni":"xmas_c20_runerockgreen"}]}"""
            ),
            (
                "xmas_c20_runerockgreen",
                """{"rewards":[{"furni":"xmas_c20_runerockpurple"},{"furni":"xmas_c20_runerockblue"},{"furni":"xmas_c20_runerockyellow"},{"furni":"xmas_c20_runerockred"}]}"""
            ),
            (
                "xmas_c20_runerockpurple",
                """{"rewards":[{"furni":"xmas_c20_runerockblue"},{"furni":"xmas_c20_runerockyellow"},{"furni":"xmas_c20_runerockred"},{"furni":"xmas_c20_runerockgreen"}]}"""
            ),
            (
                "xmas_c20_runerockred",
                """{"rewards":[{"furni":"xmas_c20_runerockpurple"},{"furni":"xmas_c20_runerockblue"},{"furni":"xmas_c20_runerockyellow"},{"furni":"xmas_c20_runerockgreen"}]}"""
            ),
            (
                "xmas_c20_runerockyellow",
                """{"rewards":[{"furni":"xmas_c20_runerockpurple"},{"furni":"xmas_c20_runerockblue"},{"furni":"xmas_c20_runerockred"},{"furni":"xmas_c20_runerockgreen"}]}"""
            ),
        ];

        private const string HAS_SECTION =
            "`logic` = 'crackable' AND JSON_VALID(`extra_data`) AND JSON_CONTAINS_PATH(`extra_data`, 'one', '$.crackable')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, contents) in CONTENTS)
                migrationBuilder.Sql(
                    "UPDATE `furniture_definitions` SET `extra_data` = JSON_SET(`extra_data`, '$.crackable', "
                        + $"JSON_MERGE_PATCH(JSON_EXTRACT(`extra_data`, '$.crackable'), JSON_EXTRACT('{contents}', '$'))) "
                        + $"WHERE `name` = '{name}' AND {HAS_SECTION};"
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, _) in CONTENTS)
                migrationBuilder.Sql(
                    "UPDATE `furniture_definitions` SET `extra_data` = JSON_REMOVE(JSON_REMOVE(`extra_data`, '$.crackable.rewards'), '$.crackable.draws') "
                        + $"WHERE `name` = '{name}' AND {HAS_SECTION};"
                );
        }
    }
}
