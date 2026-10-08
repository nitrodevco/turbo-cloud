using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

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
    /// Data only: the model is unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261008170000_MapFurnitureBehaviours")]
    public partial class MapFurnitureBehaviours : Migration
    {
        private const string CRACKABLE = "crackable";
        private const string VENDING_MACHINE = "vending_machine";
        private const string EFFECT_PROVIDER = "effect_provider";

        /// <summary>
        /// Name, the asset's state count, the usage policy (2 everyone, 1 room rights), whether it
        /// is walked on, the <c>crackable</c> section.
        /// </summary>
        private static readonly (
            string Name,
            int States,
            int Usage,
            bool Walkable,
            string Crackable
        )[] CRACKABLES =
        [
            (
                "bc_gift_14days",
                3,
                1,
                false,
                """{"rewardSet":"bcgift_1","target":1,"rewards":[{"subscription":"BuildersClub","subscriptionDays":14}]}"""
            ),
            (
                "bc_gift_31days",
                3,
                1,
                false,
                """{"rewardSet":"bcgift_2","target":1,"rewards":[{"subscription":"BuildersClub","subscriptionDays":31}]}"""
            ),
            (
                "bonusbag16_1",
                3,
                1,
                false,
                """{"rewardSet":"bonusrares16_1","target":1,"rewards":[{"furni":"bonusrare16_5*1"},{"furni":"bonusrare16_5*4"},{"furni":"bonusrare16_5*2"},{"furni":"bonusrare16_5*6"},{"furni":"bonusrare16_5*3"},{"furni":"bonusrare16_5*5"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag16_2",
                3,
                1,
                false,
                """{"rewardSet":"bonusrares16_2","target":1,"rewards":[{"furni":"bonusrare16_6*1"},{"furni":"bonusrare16_6*2"},{"furni":"bonusrare16_6*3"},{"furni":"bonusrare16_6*4"},{"furni":"bonusrare16_6*5"},{"furni":"bonusrare16_6*6"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag16_3",
                3,
                1,
                false,
                """{"rewardSet":"bonusrares16_3","target":1,"rewards":[{"furni":"bonusrare16_7*5"},{"furni":"bonusrare16_7*2"},{"furni":"bonusrare16_7*6"},{"furni":"bonusrare16_7*4"},{"furni":"bonusrare16_7*3"},{"furni":"bonusrare16_7*1"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag17_1",
                3,
                1,
                false,
                """{"rewardSet":"bonusrares17_1","target":1,"rewards":[{"furni":"bonusrare17_1*0"},{"furni":"bonusrare17_1*1"},{"furni":"bonusrare17_1*3"},{"furni":"bonusrare17_1*4"},{"furni":"bonusrare17_1*5"},{"furni":"bonusrare17_1*2"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag17_2",
                3,
                1,
                false,
                """{"rewardSet":"bonusrares17_2","target":1,"rewards":[{"furni":"bonusrare17_2*4"},{"furni":"bonusrare17_2*5"},{"furni":"bonusrare17_2*0"},{"furni":"bonusrare17_2*3"},{"furni":"bonusrare17_2*2"},{"furni":"bonusrare17_2*1"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag17_3",
                3,
                1,
                false,
                """{"rewardSet":"bonusrares17_3","target":1,"rewards":[{"furni":"bonusrare17_3*4"},{"furni":"bonusrare17_3*3"},{"furni":"bonusrare17_3*6"},{"furni":"bonusrare17_3*2"},{"furni":"bonusrare17_3*1"},{"furni":"bonusrare17_3*5"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag17_4",
                3,
                1,
                false,
                """{"rewardSet":"bonusrares17_4","target":1,"rewards":[{"furni":"bonusrare17_4*3"},{"furni":"bonusrare17_4*6"},{"furni":"bonusrare17_4*5"},{"furni":"bonusrare17_4*4"},{"furni":"bonusrare17_4*1"},{"furni":"bonusrare17_4*2"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag18_1",
                3,
                1,
                false,
                """{"rewardSet":"bonusrare18_1","target":1,"rewards":[{"furni":"bonusrare18_1*4"},{"furni":"bonusrare18_1*0"},{"furni":"bonusrare18_1*5"},{"furni":"bonusrare18_1*1"},{"furni":"bonusrare18_1*3"},{"furni":"bonusrare18_1*2"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag18_2",
                3,
                1,
                false,
                """{"rewardSet":"bonusrare18_2","target":1,"rewards":[{"furni":"bonusrare18_2*3"},{"furni":"bonusrare18_2*2"},{"furni":"bonusrare18_2*5"},{"furni":"bonusrare18_2*4"},{"furni":"bonusrare18_2*1"},{"furni":"bonusrare18_2*0"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag18_3",
                3,
                1,
                false,
                """{"rewardSet":"bonusrare18_3","target":1,"rewards":[{"furni":"bonusrare18_3*4"},{"furni":"bonusrare18_3*3"},{"furni":"bonusrare18_3*2"},{"furni":"bonusrare18_3*1"},{"furni":"bonusrare18_3*0"},{"furni":"bonusrare18_3*5"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag18_4",
                3,
                1,
                false,
                """{"rewardSet":"bonusrare18_4","target":1,"rewards":[{"furni":"bonusrare18_4*1"},{"furni":"bonusrare18_4*4"},{"furni":"bonusrare18_4*0"},{"furni":"bonusrare18_4*3"},{"furni":"bonusrare18_4*5"},{"furni":"bonusrare18_4*2"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag19_1",
                3,
                1,
                false,
                """{"rewardSet":"bonusrare19_1","target":1,"rewards":[{"furni":"bonusrare19_1*0"},{"furni":"bonusrare19_1*1"},{"furni":"bonusrare19_1*2"},{"furni":"bonusrare19_1*3"},{"furni":"bonusrare19_1*4"},{"furni":"bonusrare19_1*5"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag19_2",
                3,
                1,
                false,
                """{"rewardSet":"bonusrare19_2","target":1,"rewards":[{"furni":"bonusrare19_2*0"},{"furni":"bonusrare19_2*1"},{"furni":"bonusrare19_2*2"},{"furni":"bonusrare19_2*3"},{"furni":"bonusrare19_2*4"},{"furni":"bonusrare19_2*5"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag19_3",
                3,
                1,
                false,
                """{"rewardSet":"bonusrare19_3","target":1,"rewards":[{"furni":"bonusrare19_3*0"},{"furni":"bonusrare19_3*1"},{"furni":"bonusrare19_3*2"},{"furni":"bonusrare19_3*3"},{"furni":"bonusrare19_3*4"},{"furni":"bonusrare19_3*5"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag19_4",
                3,
                1,
                false,
                """{"rewardSet":"bonusrare19_4","target":1,"rewards":[{"furni":"bonusrare19_4*0"},{"furni":"bonusrare19_4*1"},{"furni":"bonusrare19_4*2"},{"furni":"bonusrare19_4*3"},{"furni":"bonusrare19_4*4"},{"furni":"bonusrare19_4*5"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag20_1",
                3,
                1,
                false,
                """{"rewardSet":"bonusrares20_1","target":1,"rewards":[{"furni":"bonusrare20_1a*1"},{"furni":"bonusrare20_1a*2"},{"furni":"bonusrare20_1a*3"},{"furni":"bonusrare20_1a*4"},{"furni":"bonusrare20_1a*5"},{"furni":"bonusrare20_1a*6"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag20_2",
                3,
                1,
                false,
                """{"rewardSet":"bonusrares20_2","target":1,"rewards":[{"furni":"bonusrare20_2*1"},{"furni":"bonusrare20_2*2"},{"furni":"bonusrare20_2*3"},{"furni":"bonusrare20_2*4"},{"furni":"bonusrare20_2*5"},{"furni":"bonusrare20_2*6"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag20_3",
                3,
                1,
                false,
                """{"rewardSet":"bonusrares20_3","target":1,"rewards":[{"furni":"bonusrare20_3*1"},{"furni":"bonusrare20_3*2"},{"furni":"bonusrare20_3*3"},{"furni":"bonusrare20_3*4"},{"furni":"bonusrare20_3*5"},{"furni":"bonusrare20_3*6"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag20_4",
                3,
                1,
                false,
                """{"rewardSet":"bonusrares20_4","target":1,"rewards":[{"furni":"bonusrare20_4*0"},{"furni":"bonusrare20_4*1"},{"furni":"bonusrare20_4*2"},{"furni":"bonusrare20_4*3"},{"furni":"bonusrare20_4*4"},{"furni":"bonusrare20_4*5"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag21_1",
                3,
                1,
                false,
                """{"rewardSet":"bonusrares21_1","target":1,"rewards":[{"furni":"bonusrare21_1a*0"},{"furni":"bonusrare21_1a*1"},{"furni":"bonusrare21_1a*2"},{"furni":"bonusrare21_1a*3"},{"furni":"bonusrare21_1a*4"},{"furni":"bonusrare21_1a*5"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag21_2",
                3,
                1,
                false,
                """{"rewardSet":"bonusrares21_2","target":1,"rewards":[{"furni":"bonusrare21_2*0"},{"furni":"bonusrare21_2*1"},{"furni":"bonusrare21_2*2"},{"furni":"bonusrare21_2*3"},{"furni":"bonusrare21_2*4"},{"furni":"bonusrare21_2*5"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag21_3",
                3,
                1,
                false,
                """{"rewardSet":"bonusrares21_3","target":1,"rewards":[{"furni":"bonusrare21_3*1"},{"furni":"bonusrare21_3*2"},{"furni":"bonusrare21_3*3"},{"furni":"bonusrare21_3*4"},{"furni":"bonusrare21_3*5"},{"furni":"bonusrare21_3*6"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "bonusbag21_4",
                3,
                1,
                false,
                """{"rewardSet":"bonusrares21_4","target":1,"rewards":[{"furni":"bonusrare21_4*0"},{"furni":"bonusrare21_4*1"},{"furni":"bonusrare21_4*2"},{"furni":"bonusrare21_4*3"},{"furni":"bonusrare21_4*4"},{"furni":"bonusrare21_4*5"},{"credits":5},{"subscription":"HabboClub","subscriptionDays":3}]}"""
            ),
            (
                "booster_c19_box1",
                3,
                1,
                false,
                """{"rewardSet":"booster19_1_blue","target":1,"rewardPlacement":"Inventory"}"""
            ),
            (
                "booster_c19_box2",
                3,
                1,
                false,
                """{"rewardSet":"booster19_1_red","target":1,"rewardPlacement":"Inventory"}"""
            ),
            (
                "booster_c20_box",
                3,
                1,
                false,
                """{"rewardSet":"booster20_1","target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"art_c20_clock","weight":1393},{"furni":"art_c20_pillow","weight":1393},{"furni":"art_c20_chair1","weight":1393},{"furni":"art_c20_chair2","weight":1393},{"furni":"art_c20_chair3","weight":1393},{"furni":"art_c20_chair4","weight":1393},{"furni":"clothing_c20_heartbackpack","weight":1393},{"furni":"art_c20_gold5","weight":42},{"furni":"art_c20_gold4","weight":42},{"furni":"art_c20_gold1","weight":42},{"furni":"art_c20_gold2","weight":42},{"furni":"art_c20_gold3","weight":42},{"furni":"clothing_c20_goldheartbp","weight":42}]}"""
            ),
            (
                "coralking_c18_treasurechest",
                3,
                1,
                false,
                """{"rewardSet":"coralking_1","target":1,"rewardPlacement":"Inventory","draws":[{"count":3,"rewards":[{"furni":"coralking_c18_spinycoral1"},{"furni":"coralking_c18_spinycoral2"},{"furni":"coralking_c18_bushycoral1"},{"furni":"coralking_c18_bushycoral2"},{"furni":"coralking_c18_closedspiral1"},{"furni":"coralking_c18_closedspiral2"},{"furni":"coralking_c18_starfish1"},{"furni":"coralking_c18_starfish2"},{"furni":"coralking_c18_seaweed"},{"furni":"coralking_c18_openspiral1"},{"furni":"coralking_c18_openspiral2"},{"furni":"coralking_c18_cone"},{"furni":"coralking_c18_clamshell1"},{"furni":"coralking_c18_clamshell2"},{"furni":"coralking_c18_clamshell3"}]},{"rewards":[{"furni":"coralking_c18_spinycoral1","weight":8},{"furni":"coralking_c18_spinycoral2","weight":8},{"furni":"coralking_c18_bushycoral1","weight":8},{"furni":"coralking_c18_bushycoral2","weight":8},{"furni":"coralking_c18_closedspiral1","weight":8},{"furni":"coralking_c18_closedspiral2","weight":8},{"furni":"coralking_c18_starfish1","weight":8},{"furni":"coralking_c18_starfish2","weight":8},{"furni":"coralking_c18_seaweed","weight":8},{"furni":"coralking_c18_openspiral1","weight":8},{"furni":"coralking_c18_openspiral2","weight":8},{"furni":"coralking_c18_cone","weight":8},{"furni":"coralking_c18_clamshell1","weight":8},{"furni":"coralking_c18_clamshell2","weight":8},{"furni":"coralking_c18_clamshell3","weight":8},{"furni":"coralking_c18_pearloyster","weight":15},{"furni":"coralking_c18_goldenfish","weight":15},{"furni":"coralking_c18_chalice","weight":15},{"furni":"coralking_c18_trident","weight":15},{"furni":"clothing_r18_seawreath","weight":10},{"furni":"clothing_r18_goldfish","weight":10}]}]}"""
            ),
            (
                "coralking_r18_goldenchest",
                3,
                1,
                false,
                """{"rewardSet":"coralking_2","target":1,"rewardPlacement":"Inventory","draws":[{"count":3,"rewards":[{"furni":"coralking_c18_spinycoral1"},{"furni":"coralking_c18_spinycoral2"},{"furni":"coralking_c18_bushycoral1"},{"furni":"coralking_c18_bushycoral2"},{"furni":"coralking_c18_closedspiral1"},{"furni":"coralking_c18_closedspiral2"},{"furni":"coralking_c18_starfish1"},{"furni":"coralking_c18_starfish2"},{"furni":"coralking_c18_seaweed"},{"furni":"coralking_c18_openspiral1"},{"furni":"coralking_c18_openspiral2"},{"furni":"coralking_c18_cone"},{"furni":"coralking_c18_clamshell1"},{"furni":"coralking_c18_clamshell2"},{"furni":"coralking_c18_clamshell3"}]},{"rewards":[{"furni":"coralking_c18_pearloyster","weight":35},{"furni":"coralking_c18_goldenfish","weight":35},{"furni":"coralking_c18_chalice","weight":35},{"furni":"coralking_c18_trident","weight":35},{"furni":"clothing_r18_seawreath","weight":30},{"furni":"clothing_r18_goldfish","weight":30}]}]}"""
            ),
            (
                "diamond_c18_giftbox",
                3,
                1,
                false,
                """{"rewardSet":"diamond18","target":1,"rewards":[{"furni":"clothing_diafish","weight":10},{"furni":"clothing_dianoblecrown","weight":20},{"furni":"clothing_luscioushair","weight":70},{"furni":"clothing_twotonecardi","weight":75},{"furni":"clothing_cjersey","weight":75},{"furni":"clothing_sliponcanvas","weight":75},{"furni":"clothing_hipsterglasses","weight":75},{"furni":"jungle_c16_roof","weight":90},{"furni":"attic15_paintingfloor","weight":90},{"furni":"bazaar_c17_bubblejuiceblower","weight":90},{"furni":"cpunk_c15_traffic","weight":90},{"furni":"paris_c15_flowerstl","weight":100}]}"""
            ),
            (
                "easter13_egg_0",
                15,
                2,
                false,
                """{"rewardSet":"egg_p1","target":1000,"incrementalHitAchievement":"EggCracker","finalHitAchievement":"EggMaster","rewards":[{"furni":"barchair_silo"},{"furni":"chair_silo"},{"furni":"divider_silo1"},{"furni":"divider_silo3"},{"furni":"safe_silo"},{"furni":"sofa_silo"},{"furni":"sofachair_silo"},{"furni":"table_silo_med"},{"furni":"table_silo_small"}]}"""
            ),
            (
                "easter13_egg_1",
                15,
                2,
                false,
                """{"rewardSet":"egg_p2","target":5000,"incrementalHitAchievement":"EggCracker","finalHitAchievement":"EggMaster","rewards":[{"furni":"penguin_frank"},{"furni":"penguin_skele"},{"furni":"penguin_tribal"},{"furni":"penguin_wip"}]}"""
            ),
            (
                "easter13_egg_2",
                15,
                2,
                false,
                """{"rewardSet":"egg_p3","target":10000,"incrementalHitAchievement":"EggCracker","finalHitAchievement":"EggMaster","rewards":[{"furni":"duck_zombie"},{"furni":"duck_scuba"},{"furni":"duck_frank"},{"furni":"duck_afro"}]}"""
            ),
            (
                "easter13_egg_3",
                15,
                2,
                false,
                """{"rewardSet":"egg_p4","target":20000,"incrementalHitAchievement":"EggCracker","finalHitAchievement":"EggMaster","rewards":[{"furni":"easter13_stonefrank"},{"furni":"easter13_stonehead"},{"furni":"easter13_sub"}]}"""
            ),
            (
                "easter_c17_egg",
                21,
                2,
                false,
                """{"rewardSet":"easter17_6","target":10,"rewardTo":"Cracker","rewards":[{"furni":"easter_c17_choc"},{"furni":"easter_c17_flour"}]}"""
            ),
            (
                "easter_c17_floweringbush",
                3,
                1,
                false,
                """{"rewardSet":"easter17_5","target":1,"requiredEffectId":192,"finalHitAchievement":"Farmer","rewards":[{"furni":"easter_c17_strawbsbush"},{"furni":"easter_c17_raspbush"},{"furni":"easter_c17_blkberrybush"}]}"""
            ),
            (
                "easter_c17_leafsprout",
                3,
                1,
                false,
                """{"rewardSet":"easter17_4","target":1,"requiredEffectId":192,"finalHitAchievement":"Farmer","rewards":[{"furni":"easter_c17_carrot"}]}"""
            ),
            (
                "easter_c17_sapling",
                3,
                1,
                false,
                """{"rewardSet":"easter17_3","target":1,"requiredEffectId":192,"rewards":[{"furni":"easter_c17_floweringbush"}]}"""
            ),
            (
                "easter_c17_seedbag",
                3,
                1,
                false,
                """{"rewardSet":"easter17_1","target":1,"rewards":[{"furni":"easter_c17_seeds"}]}"""
            ),
            (
                "easter_c17_seeds",
                3,
                1,
                false,
                """{"rewardSet":"easter17_2","target":1,"requiredEffectId":192,"rewards":[{"furni":"easter_c17_sapling"},{"furni":"easter_c17_leafsprout"}]}"""
            ),
            (
                "easter_c18_lupin1",
                13,
                1,
                true,
                """{"rewardSet":"easter18_3","target":12,"requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist","rewards":[{"furni":"easter_c18_lupin1"},{"furni":"easter_c18_lupin2"},{"furni":"easter_c18_lupin3"},{"furni":"easter_c18_lupin4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_lupin2",
                13,
                1,
                true,
                """{"rewardSet":"easter18_3","target":12,"requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist","rewards":[{"furni":"easter_c18_lupin1"},{"furni":"easter_c18_lupin2"},{"furni":"easter_c18_lupin3"},{"furni":"easter_c18_lupin4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_lupin3",
                13,
                1,
                true,
                """{"rewardSet":"easter18_3","target":12,"requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist","rewards":[{"furni":"easter_c18_lupin1"},{"furni":"easter_c18_lupin2"},{"furni":"easter_c18_lupin3"},{"furni":"easter_c18_lupin4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_lupin4",
                13,
                1,
                true,
                """{"rewardSet":"easter18_3","target":12,"requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist","rewards":[{"furni":"easter_c18_lupin1"},{"furni":"easter_c18_lupin2"},{"furni":"easter_c18_lupin3"},{"furni":"easter_c18_lupin4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_rose1",
                13,
                1,
                true,
                """{"rewardSet":"easter18_1","target":12,"requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist","rewards":[{"furni":"easter_c18_rose1"},{"furni":"easter_c18_rose2"},{"furni":"easter_c18_rose3"},{"furni":"easter_c18_rose4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_rose2",
                13,
                1,
                true,
                """{"rewardSet":"easter18_1","target":12,"requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist","rewards":[{"furni":"easter_c18_rose1"},{"furni":"easter_c18_rose2"},{"furni":"easter_c18_rose3"},{"furni":"easter_c18_rose4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_rose3",
                13,
                1,
                true,
                """{"rewardSet":"easter18_1","target":12,"requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist","rewards":[{"furni":"easter_c18_rose1"},{"furni":"easter_c18_rose2"},{"furni":"easter_c18_rose3"},{"furni":"easter_c18_rose4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_rose4",
                13,
                1,
                true,
                """{"rewardSet":"easter18_1","target":12,"requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist","rewards":[{"furni":"easter_c18_rose1"},{"furni":"easter_c18_rose2"},{"furni":"easter_c18_rose3"},{"furni":"easter_c18_rose4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_seedpacklupin",
                3,
                1,
                false,
                """{"rewardSet":"easter18_seed3","target":1,"rewards":[{"furni":"easter_c18_lupin1"},{"furni":"easter_c18_lupin2"},{"furni":"easter_c18_lupin3"},{"furni":"easter_c18_lupin4"}]}"""
            ),
            (
                "easter_c18_seedpackrose",
                3,
                1,
                false,
                """{"rewardSet":"easter18_seed1","target":1,"rewards":[{"furni":"easter_c18_rose1"},{"furni":"easter_c18_rose2"},{"furni":"easter_c18_rose3"},{"furni":"easter_c18_rose4"}]}"""
            ),
            (
                "easter_c18_seedpacksnowdrop",
                3,
                1,
                false,
                """{"rewardSet":"easter18_seed4","target":1,"rewards":[{"furni":"easter_c18_snowdrop1"},{"furni":"easter_c18_snowdrop2"},{"furni":"easter_c18_snowdrop3"},{"furni":"easter_c18_snowdrop4"}]}"""
            ),
            (
                "easter_c18_seedpacktulip",
                3,
                1,
                false,
                """{"rewardSet":"easter18_seed2","target":1,"rewards":[{"furni":"easter_c18_tulip1"},{"furni":"easter_c18_tulip2"},{"furni":"easter_c18_tulip3"},{"furni":"easter_c18_tulip4"}]}"""
            ),
            (
                "easter_c18_snowdrop1",
                13,
                1,
                true,
                """{"rewardSet":"easter18_4","target":12,"requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist","rewards":[{"furni":"easter_c18_snowdrop1"},{"furni":"easter_c18_snowdrop2"},{"furni":"easter_c18_snowdrop3"},{"furni":"easter_c18_snowdrop4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_snowdrop2",
                13,
                1,
                true,
                """{"rewardSet":"easter18_4","target":12,"requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist","rewards":[{"furni":"easter_c18_snowdrop1"},{"furni":"easter_c18_snowdrop2"},{"furni":"easter_c18_snowdrop3"},{"furni":"easter_c18_snowdrop4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_snowdrop3",
                13,
                1,
                true,
                """{"rewardSet":"easter18_4","target":12,"requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist","rewards":[{"furni":"easter_c18_snowdrop1"},{"furni":"easter_c18_snowdrop2"},{"furni":"easter_c18_snowdrop3"},{"furni":"easter_c18_snowdrop4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_snowdrop4",
                13,
                1,
                true,
                """{"rewardSet":"easter18_4","target":12,"requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist","rewards":[{"furni":"easter_c18_snowdrop1"},{"furni":"easter_c18_snowdrop2"},{"furni":"easter_c18_snowdrop3"},{"furni":"easter_c18_snowdrop4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_tulip1",
                13,
                1,
                true,
                """{"rewardSet":"easter18_2","target":12,"requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist","rewards":[{"furni":"easter_c18_tulip1"},{"furni":"easter_c18_tulip2"},{"furni":"easter_c18_tulip3"},{"furni":"easter_c18_tulip4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_tulip2",
                13,
                1,
                true,
                """{"rewardSet":"easter18_2","target":12,"requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist","rewards":[{"furni":"easter_c18_tulip1"},{"furni":"easter_c18_tulip2"},{"furni":"easter_c18_tulip3"},{"furni":"easter_c18_tulip4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_tulip3",
                13,
                1,
                true,
                """{"rewardSet":"easter18_2","target":12,"requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist","rewards":[{"furni":"easter_c18_tulip1"},{"furni":"easter_c18_tulip2"},{"furni":"easter_c18_tulip3"},{"furni":"easter_c18_tulip4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c18_tulip4",
                13,
                1,
                true,
                """{"rewardSet":"easter18_2","target":12,"requiredEffectId":192,"finalHitAchievement":"AdvancedHorticulturist","rewards":[{"furni":"easter_c18_tulip1"},{"furni":"easter_c18_tulip2"},{"furni":"easter_c18_tulip3"},{"furni":"easter_c18_tulip4"},{"furni":"easter_c18_badflower"}]}"""
            ),
            (
                "easter_c19_ancientbook",
                3,
                1,
                false,
                """{"rewardSet":"easter19_5","target":1,"requiredEffectId":186,"rewards":[{"furni":"easter_c19_book1"},{"furni":"easter_c19_book2"},{"furni":"easter_c19_book3"},{"furni":"easter_c19_book4"}]}"""
            ),
            (
                "easter_c19_babyent",
                25,
                1,
                false,
                """{"rewardSet":"easter19_3","target":12,"requiredEffectId":186,"finalHitAchievement":"EasterCreatures","rewards":[{"furni":"easter_c19_ent","weight":75},{"furni":"easter_c19_earthdrago","weight":25}]}"""
            ),
            (
                "easter_c19_babyhippogriff",
                25,
                1,
                false,
                """{"rewardSet":"easter19_4","target":12,"requiredEffectId":186,"finalHitAchievement":"EasterCreatures","rewards":[{"furni":"easter_c19_bearowl","weight":75},{"furni":"easter_c19_hippogriff","weight":25}]}"""
            ),
            (
                "easter_c19_babykelpie",
                25,
                1,
                false,
                """{"rewardSet":"easter19_2","target":12,"requiredEffectId":186,"finalHitAchievement":"EasterCreatures","rewards":[{"furni":"easter_c19_kelpie","weight":75},{"furni":"easter_c19_waterdrago","weight":25}]}"""
            ),
            (
                "easter_c19_book1",
                3,
                1,
                false,
                """{"rewardSet":"easter19_6","target":1,"rewards":[{"furni":"easter_c19_wolf","weight":45},{"furni":"easter_c19_lilredbonnie","weight":45},{"furni":"clothing_wolfmask","weight":10}]}"""
            ),
            (
                "easter_c19_book2",
                3,
                1,
                false,
                """{"rewardSet":"easter19_7","target":1,"rewards":[{"furni":"easter_c19_habshirecat","weight":45},{"furni":"easter_c19_busybunny","weight":45},{"furni":"clothing_madhat","weight":10}]}"""
            ),
            (
                "easter_c19_book3",
                3,
                1,
                false,
                """{"rewardSet":"easter19_8","target":1,"rewards":[{"furni":"easter_c19_woodlandcritters","weight":45},{"furni":"easter_c19_chillgnome","weight":45},{"furni":"clothing_ribboncurls","weight":10}]}"""
            ),
            (
                "easter_c19_book4",
                3,
                1,
                false,
                """{"rewardSet":"easter19_9","target":1,"rewards":[{"furni":"easter_c19_habelina","weight":45},{"furni":"easter_c19_fairyprince","weight":45},{"furni":"clothing_flowerponytail","weight":10}]}"""
            ),
            (
                "easter_c19_forrestegg",
                25,
                1,
                false,
                """{"rewardSet":"easter19_1","target":12,"requiredEffectId":186,"finalHitAchievement":"EasterCreatures","rewards":[{"furni":"easter_c19_babyent"},{"furni":"easter_c19_babyhippogriff"},{"furni":"easter_c19_babykelpie"}]}"""
            ),
            (
                "easter_c20_darkprize1",
                3,
                1,
                false,
                """{"rewardSet":"easter20_dark1","target":1,"requiredEffectId":186,"rewards":[{"furni":"easter_c20_clayrelic"}]}"""
            ),
            (
                "easter_c20_darkprize2",
                3,
                1,
                false,
                """{"rewardSet":"easter20_dark2","target":1,"requiredEffectId":186,"rewards":[{"furni":"easter_c20_ancienthorse"}]}"""
            ),
            (
                "easter_c20_darkprize3",
                3,
                1,
                false,
                """{"rewardSet":"easter20_dark3","target":1,"requiredEffectId":186,"rewards":[{"furni":"easter_c20_zenmaster"}]}"""
            ),
            (
                "easter_c20_darkprize4",
                3,
                1,
                false,
                """{"rewardSet":"easter20_dark4","target":1,"requiredEffectId":186,"rewards":[{"furni":"clothing_mysticcrown"}]}"""
            ),
            (
                "easter_c20_darkrock",
                3,
                1,
                false,
                """{"rewardSet":"easter20_dark0","target":1,"requiredEffectId":183,"rewards":[{"furni":"easter_c20_darkprize1","weight":3125},{"furni":"easter_c20_darkprize2","weight":3125},{"furni":"easter_c20_darkprize3","weight":3125},{"furni":"easter_c20_darkprize4","weight":625}]}"""
            ),
            (
                "easter_c20_lightprize1",
                3,
                1,
                false,
                """{"rewardSet":"easter20_light1","target":1,"requiredEffectId":186,"rewards":[{"furni":"easter_c20_ancientbird"}]}"""
            ),
            (
                "easter_c20_lightprize2",
                3,
                1,
                false,
                """{"rewardSet":"easter20_light2","target":1,"requiredEffectId":186,"rewards":[{"furni":"easter_c20_jadeguardian"}]}"""
            ),
            (
                "easter_c20_lightprize3",
                3,
                1,
                false,
                """{"rewardSet":"easter20_light3","target":1,"requiredEffectId":186,"rewards":[{"furni":"easter_c20_ancientstatue"}]}"""
            ),
            (
                "easter_c20_lightprize4",
                3,
                1,
                false,
                """{"rewardSet":"easter20_light4","target":1,"requiredEffectId":186,"rewards":[{"furni":"clothing_mysticrobes"}]}"""
            ),
            (
                "easter_c20_lightrock",
                3,
                1,
                false,
                """{"rewardSet":"easter20_light0","target":1,"requiredEffectId":183,"rewards":[{"furni":"easter_c20_lightprize3","weight":3125},{"furni":"easter_c20_lightprize2","weight":3125},{"furni":"easter_c20_lightprize1","weight":3125},{"furni":"easter_c20_lightprize4","weight":625}]}"""
            ),
            (
                "easter_r16_crackable",
                3,
                1,
                false,
                """{"rewardSet":"easter16","target":1,"rewards":[{"furni":"gothic_r16_fountain"},{"furni":"easter_r16_gold"},{"furni":"easter_r16_squid"},{"furni":"easter_r16_pot"},{"furni":"easter_r16_throne"}]}"""
            ),
            (
                "fest_c19_bprintcrackable",
                3,
                1,
                false,
                """{"rewardSet":"fest19_1","target":1,"rewards":[{"furni":"fest_c19_bprint1"},{"furni":"fest_c19_bprint2"},{"furni":"fest_c19_bprint3"},{"furni":"fest_c19_bprint4"}]}"""
            ),
            (
                "gold_rare_crackable",
                3,
                1,
                false,
                """{"rewardSet":"gold_1","target":1,"rewards":[{"furni":"gold_c15_arc_geysir"},{"furni":"gold_c15_arc_camp"},{"furni":"gold_c15_arc_chair"},{"furni":"gold_c15_arc_tree2"},{"furni":"gold_c15_arc_tree1"},{"furni":"gold_c15_arc_tub"},{"furni":"gold_c15_arc_lamp"},{"furni":"gold_c15_arc_statue"},{"furni":"gold_c15_arc_seat"},{"furni":"gold_c15_arc_table"}]}"""
            ),
            (
                "habbo15_crackable",
                1,
                1,
                false,
                """{"rewardSet":"habboanv_1","target":1,"incrementalHitAchievement":"PinataWhacker","finalHitAchievement":"PinataWhacker"}"""
            ),
            ("habbo15_pumpkin1", 3, 1, false, """{"rewardSet":"habbocalypse_1","target":1}"""),
            (
                "habbo15_pumpkin2",
                3,
                1,
                false,
                """{"rewardSet":"habbocalypse_2","target":1,"rewards":[{"furni":"duck_afro"},{"furni":"hween14_goat"},{"furni":"hween12_guillotine"},{"furni":"hween15_evilraider"},{"furni":"hween15_evilfrank"},{"furni":"hween15_saintta"},{"furni":"hween15_saintini"}]}"""
            ),
            (
                "habbo15_rare_crackable",
                1,
                1,
                false,
                """{"rewardSet":"habboanv_2","target":1,"incrementalHitAchievement":"PinataWhacker","finalHitAchievement":"PinataWhacker","rewards":[{"furni":"rare_beehive_bulb*3"},{"furni":"pillar*10"},{"furni":"rare_dragonlamp*10"},{"furni":"rare_elephant_statue*3"},{"furni":"rare_fountain*4"},{"furni":"rare_icecream*11"},{"furni":"scifiport*10"},{"furni":"marquee*11"},{"furni":"wooden_screen*10"},{"furni":"rare_parasol*4"},{"furni":"pillow*10"},{"furni":"rare_fan*10"},{"furni":"sleepingbag*11"},{"furni":"scifirocket*10"},{"furni":"scifidoor*11"}]}"""
            ),
            (
                "habbo20_c20_crackable",
                3,
                1,
                false,
                """{"rewardSet":"habbo20_common","target":1,"rewards":[{"furni":"habbo20_c20_crackable","weight":36},{"furni":"habbo20_r20_crackable","weight":4},{"furni":"clothing_meowtfit","weight":60},{"furni":"clothing_casualoutfit1","weight":100},{"furni":"clothing_demonoutfit","weight":100},{"furni":"clothing_mermaidoutfit","weight":100},{"furni":"clothing_messycurls","weight":120},{"furni":"clothing_knithat","weight":120},{"furni":"clothing_cyphones","weight":120},{"furni":"clothing_tux","weight":120},{"furni":"clothing_witchrobes","weight":120}]}"""
            ),
            (
                "habbo20_r20_crackable",
                3,
                1,
                false,
                """{"rewardSet":"habbo20_rare","target":1,"rewards":[{"furni":"rare_blackrosegold_dragonlamp"},{"furni":"rare_blackrosegold_icecream","weight":2},{"furni":"rare_blackrosegold_fountain","weight":4},{"furni":"rare_blackrosegold_elephant_statue","weight":4},{"furni":"rare_blackrosegold_parasol","weight":4},{"furni":"rare_blackrosegold_scifiport","weight":6},{"furni":"rare_blackrosegold_fan","weight":6},{"furni":"rare_blackrosegold_pillow","weight":6},{"furni":"rare_blackrosegold_scifidoor","weight":7},{"furni":"rare_blackrosegold_scifirocket","weight":7},{"furni":"rare_blackrosegold_beehive_bulb","weight":7},{"furni":"rare_blackrosegold_sleepingbag","weight":9},{"furni":"rare_blackrosegold_wooden_screen","weight":9},{"furni":"rare_blackrosegold_pillar","weight":9},{"furni":"rare_blackrosegold_marquee","weight":9},{"furni":"rare_blackrosegold_barrier","weight":10}]}"""
            ),
            (
                "hblooza14_pinata1",
                9,
                2,
                true,
                """{"rewardSet":"pinata1","target":100,"hitOn":"Walk","requiredEffectId":158,"incrementalHitAchievement":"PinataWhacker","finalHitAchievement":"PinataBreaker","rewards":[{"furni":"hblooza14_duck_balloon_y"},{"furni":"hblooza14_duck_balloon_b"},{"furni":"hblooza14_duck_balloon_p"}]}"""
            ),
            (
                "hblooza14_pinata2",
                9,
                2,
                true,
                """{"rewardSet":"pinata1","target":100,"hitOn":"Walk","requiredEffectId":158,"incrementalHitAchievement":"PinataWhacker","finalHitAchievement":"PinataBreaker","rewards":[{"furni":"hblooza14_cafe_y"},{"furni":"hblooza14_cafe_b"},{"furni":"hblooza14_cafe_p"}]}"""
            ),
            (
                "hblooza14_pinata3",
                9,
                2,
                true,
                """{"rewardSet":"pinata1","target":100,"hitOn":"Walk","requiredEffectId":158,"incrementalHitAchievement":"PinataWhacker","finalHitAchievement":"PinataBreaker","rewards":[{"furni":"hblooza14_horsecaroy"},{"furni":"hblooza14_horsecarob"},{"furni":"hblooza14_horsecarop"}]}"""
            ),
            (
                "hblooza14_pinata4",
                9,
                2,
                true,
                """{"rewardSet":"green_pinata","target":100,"hitOn":"Walk","requiredEffectId":158,"incrementalHitAchievement":"PinataWhacker","finalHitAchievement":"PinataBreaker","rewards":[{"furni":"hblooza14_hotair_y"},{"furni":"hblooza14_hotair_b"},{"furni":"hblooza14_hotair_p"}]}"""
            ),
            (
                "hblooza_pinata1",
                9,
                2,
                true,
                """{"rewardSet":"pinata1","target":100,"hitOn":"Walk","requiredEffectId":158,"incrementalHitAchievement":"pinatawhacker","finalHitAchievement":"pinatabreaker"}"""
            ),
            (
                "hblooza_pinata2",
                9,
                2,
                true,
                """{"rewardSet":"pinata2","target":100,"hitOn":"Walk","requiredEffectId":158,"incrementalHitAchievement":"pinatawhacker","finalHitAchievement":"pinatabreaker"}"""
            ),
            (
                "hc_gift_14days",
                3,
                1,
                false,
                """{"rewardSet":"hcgift_1","target":1,"rewards":[{"subscription":"HabboClub","subscriptionDays":14}]}"""
            ),
            (
                "hc_gift_31days",
                3,
                1,
                false,
                """{"rewardSet":"hcgift_2","target":1,"rewards":[{"subscription":"HabboClub","subscriptionDays":31}]}"""
            ),
            (
                "hhistory_r16_crackable",
                3,
                1,
                false,
                """{"rewardSet":"hhistory_16","target":1,"rewards":[{"furni":"rare_colourable_dragonlamp*1"},{"furni":"rare_colourable_pillow*1"},{"furni":"rare_colourable_icecream*1"},{"furni":"rare_colourable_elephant_statue*1"},{"furni":"rare_colourable_fountain*1"},{"furni":"rare_colourable_fan*1"},{"furni":"rare_colourable_beehive_bulb*1"},{"furni":"rare_colourable_scifiport*1"},{"furni":"rare_colourable_scifirocket*1"},{"furni":"rare_colourable_parasol*1"},{"furni":"rare_colourable_marquee*1"},{"furni":"rare_colourable_pillar*1"},{"furni":"rare_colourable_scifidoor*1"},{"furni":"rare_colourable_wooden_screen*1"},{"furni":"rare_colourable_barrier*1"}]}"""
            ),
            (
                "hhistory_r17_crackable",
                3,
                1,
                false,
                """{"rewardSet":"hhistory_17","target":1,"rewards":[{"furni":"rare_colourable_dragonlamp*2"},{"furni":"rare_colourable_pillow*2"},{"furni":"rare_colourable_icecream*2"},{"furni":"rare_colourable_elephant_statue*2"},{"furni":"rare_colourable_fountain*2"},{"furni":"rare_colourable_fan*2"},{"furni":"rare_colourable_beehive_bulb*2"},{"furni":"rare_colourable_scifiport*2"},{"furni":"rare_colourable_scifirocket*2"},{"furni":"rare_colourable_parasol*2"},{"furni":"rare_colourable_marquee*2"},{"furni":"rare_colourable_pillar*2"},{"furni":"rare_colourable_scifidoor*2"},{"furni":"rare_colourable_wooden_screen*2"},{"furni":"rare_colourable_sleepingbag*2"}]}"""
            ),
            (
                "hhistory_r18_crackable",
                3,
                1,
                false,
                """{"rewardSet":"hhistory_18","target":1,"rewards":[{"furni":"rare_colourable_dragonlamp*3"},{"furni":"rare_colourable_pillow*3"},{"furni":"rare_colourable_icecream*3"},{"furni":"rare_colourable_fountain*3"},{"furni":"rare_colourable_parasol*3"},{"furni":"rare_colourable_fan*3"},{"furni":"rare_colourable_elephant_statue*3"},{"furni":"rare_colourable_scifiport*3"},{"furni":"rare_colourable_pillar*3"},{"furni":"rare_colourable_scifidoor*3"},{"furni":"rare_colourable_scifirocket*3"},{"furni":"rare_colourable_wooden_screen*3"},{"furni":"rare_colourable_marquee*3"},{"furni":"rare_colourable_sleepingbag*3"},{"furni":"rare_colourable_beehive_bulb*3"},{"furni":"rare_colourable_barrier*3"}]}"""
            ),
            ("hween_c15_pumpkin1", 3, 1, false, """{"rewardSet":"habbocalypse_1","target":1}"""),
            (
                "hween_c15_pumpkin2",
                3,
                1,
                false,
                """{"rewardSet":"habbocalypse_2","target":1,"rewards":[{"furni":"duck_afro"},{"furni":"hween14_goat"},{"furni":"hween12_guillotine"},{"furni":"hween15_evilraider"},{"furni":"hween15_evilfrank"},{"furni":"hween15_saintta"},{"furni":"hween15_saintini"}]}"""
            ),
            (
                "hween_c16_crackable1",
                21,
                2,
                false,
                """{"rewardSet":"habboanv_1","target":10,"rewardTo":"Cracker"}"""
            ),
            (
                "hween_c17_flamingknight",
                21,
                1,
                false,
                """{"rewardSet":"hween17","target":10,"finalHitAchievement":"flamingknight","rewards":[{"furni":"clothing_rebelchest"},{"furni":"clothing_herochest"},{"furni":"clothing_shoearmour"},{"furni":"clothing_badasshelmet"},{"furni":"clothing_herohelmet"},{"furni":"clothing_legarmour"}]}"""
            ),
            (
                "hween_c19_witchsatchel",
                3,
                1,
                false,
                """{"rewardSet":"hween19_witchsatchel","target":1,"rewards":[{"furni":"hween_c19_bewitchedcandles"},{"furni":"hween_c19_herbs"},{"furni":"hween_c19_bewitchedskull"},{"furni":"hween_c19_crystalball"},{"furni":"hween_c19_feathers"},{"furni":"hween_c19_crystal"},{"furni":"hween_c19_tarot"}]}"""
            ),
            (
                "hween_c20_duckgoddess",
                23,
                1,
                false,
                """{"rewardSet":"hween20_6","target":11,"requiredEffectId":186,"rewards":[{"furni":"hween_c20_goddessthrone","weight":45},{"furni":"hween_c20_goddesscrystal","weight":45},{"furni":"clothing_wingstiara","weight":10}]}"""
            ),
            (
                "hween_c20_evilscarecrow",
                23,
                1,
                false,
                """{"rewardSet":"hween20_5","target":11,"requiredEffectId":5,"rewards":[{"furni":"hween_c20_crookedtree","weight":45},{"furni":"hween_c20_crookedclock","weight":45},{"furni":"clothing_crookedhat","weight":10}]}"""
            ),
            (
                "hween_c20_eyedemon",
                23,
                1,
                false,
                """{"rewardSet":"hween20_4","target":11,"requiredEffectId":162,"rewards":[{"furni":"hween_c20_eyesofa","weight":45},{"furni":"hween_c20_eyetv","weight":45},{"furni":"clothing_multieyesface","weight":10}]}"""
            ),
            (
                "hween_c20_octodemon",
                23,
                1,
                false,
                """{"rewardSet":"hween20_3","target":11,"requiredEffectId":117,"rewards":[{"furni":"hween_c20_tentaclethrone","weight":25},{"furni":"hween_c20_tentacletable","weight":25},{"furni":"clothing_tentaclehead","weight":10}]}"""
            ),
            (
                "hween_c20_pandorabox",
                3,
                1,
                false,
                """{"rewardSet":"hween20_1","target":1,"rewards":[{"furni":"hween_r20_evilpandorabox","weight":5},{"furni":"clothing_bloodglasses","weight":7},{"furni":"clothing_bloodjacket","weight":7},{"furni":"clothing_bloodshoes","weight":7},{"furni":"clothing_brain","weight":7},{"furni":"clothing_witchhat2","weight":8},{"furni":"clothing_witchrobes","weight":8},{"furni":"clothing_possessedeyes","weight":8},{"furni":"hween_c18_labglovebox","weight":9},{"furni":"clothing_demonoutfit","weight":10},{"furni":"hween_c16_bed2","weight":10},{"furni":"hween_c15_busstop","weight":10},{"furni":"hween_c19_bewitchedcauldron","weight":13},{"furni":"hween_c17_torturebed","weight":13},{"furni":"hween_c19_fireplace","weight":15}]}"""
            ),
            (
                "hween_r16_crackable2",
                21,
                1,
                false,
                """{"rewardSet":"habboanv_1","target":10,"rewards":[{"furni":"hween_r16_grandpiano"},{"furni":"hween_r16_chandelier"},{"furni":"clothing_r16_catface"},{"furni":"clothing_r16_cyclops"}]}"""
            ),
            (
                "hween_r20_evilpandorabox",
                3,
                1,
                false,
                """{"rewardSet":"hween20_2","target":1,"rewards":[{"furni":"hween_c20_duckgoddess"},{"furni":"hween_c20_evilscarecrow"},{"furni":"hween_c20_eyedemon"},{"furni":"hween_c20_octodemon"}]}"""
            ),
            (
                "india_c20_blueprint",
                3,
                1,
                false,
                """{"rewardSet":"india20_1","target":1,"rewards":[{"furni":"india_c20_capebp"},{"furni":"india_c20_headjewelbp"},{"furni":"india_c20_saribp"},{"furni":"india_c20_sherwanibp"},{"furni":"india_c20_snakebp"}]}"""
            ),
            (
                "jungle_c16_flowera1",
                13,
                2,
                true,
                """{"rewardSet":"jung16_1","target":12,"requiredEffectId":192,"finalHitAchievement":"Horticulturist","rewards":[{"furni":"jungle_c16_flowera1"},{"furni":"jungle_c16_flowera2"},{"furni":"jungle_c16_flowera3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowera2",
                13,
                2,
                true,
                """{"rewardSet":"jung16_1","target":12,"requiredEffectId":192,"finalHitAchievement":"Horticulturist","rewards":[{"furni":"jungle_c16_flowera1"},{"furni":"jungle_c16_flowera2"},{"furni":"jungle_c16_flowera3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowera3",
                13,
                2,
                true,
                """{"rewardSet":"jung16_1","target":12,"requiredEffectId":192,"finalHitAchievement":"Horticulturist","rewards":[{"furni":"jungle_c16_flowera1"},{"furni":"jungle_c16_flowera2"},{"furni":"jungle_c16_flowera3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerb1",
                13,
                2,
                true,
                """{"rewardSet":"jung16_2","target":12,"requiredEffectId":192,"finalHitAchievement":"Horticulturist","rewards":[{"furni":"jungle_c16_flowerb1"},{"furni":"jungle_c16_flowerb2"},{"furni":"jungle_c16_flowerb3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerb2",
                13,
                2,
                true,
                """{"rewardSet":"jung16_2","target":12,"requiredEffectId":192,"finalHitAchievement":"Horticulturist","rewards":[{"furni":"jungle_c16_flowerb1"},{"furni":"jungle_c16_flowerb2"},{"furni":"jungle_c16_flowerb3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerb3",
                13,
                2,
                true,
                """{"rewardSet":"jung16_2","target":12,"requiredEffectId":192,"finalHitAchievement":"Horticulturist","rewards":[{"furni":"jungle_c16_flowerb1"},{"furni":"jungle_c16_flowerb2"},{"furni":"jungle_c16_flowerb3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerc1",
                13,
                2,
                true,
                """{"rewardSet":"jung16_3","target":12,"requiredEffectId":192,"finalHitAchievement":"Horticulturist","rewards":[{"furni":"jungle_c16_flowerc1"},{"furni":"jungle_c16_flowerc2"},{"furni":"jungle_c16_flowerc3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerc2",
                13,
                2,
                true,
                """{"rewardSet":"jung16_3","target":12,"requiredEffectId":192,"finalHitAchievement":"Horticulturist","rewards":[{"furni":"jungle_c16_flowerc1"},{"furni":"jungle_c16_flowerc2"},{"furni":"jungle_c16_flowerc3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerc3",
                13,
                2,
                true,
                """{"rewardSet":"jung16_3","target":12,"requiredEffectId":192,"finalHitAchievement":"Horticulturist","rewards":[{"furni":"jungle_c16_flowerc1"},{"furni":"jungle_c16_flowerc2"},{"furni":"jungle_c16_flowerc3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerd1",
                13,
                2,
                true,
                """{"rewardSet":"jung16_4","target":12,"requiredEffectId":192,"finalHitAchievement":"Horticulturist","rewards":[{"furni":"jungle_c16_flowerd1"},{"furni":"jungle_c16_flowerd2"},{"furni":"jungle_c16_flowerd3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerd2",
                13,
                2,
                true,
                """{"rewardSet":"jung16_4","target":12,"requiredEffectId":192,"finalHitAchievement":"Horticulturist","rewards":[{"furni":"jungle_c16_flowerd1"},{"furni":"jungle_c16_flowerd2"},{"furni":"jungle_c16_flowerd3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "jungle_c16_flowerd3",
                13,
                2,
                true,
                """{"rewardSet":"jung16_4","target":12,"requiredEffectId":192,"finalHitAchievement":"Horticulturist","rewards":[{"furni":"jungle_c16_flowerd1"},{"furni":"jungle_c16_flowerd2"},{"furni":"jungle_c16_flowerd3"},{"furni":"jungle_c16_rafflesia"}]}"""
            ),
            (
                "mystics_crystal_l",
                15,
                2,
                false,
                """{"rewardSet":"crystal_p1","target":1500000,"incrementalHitAchievement":"CrystalCracker","finalHitAchievement":"CrystalLegend"}"""
            ),
            (
                "mystics_crystal_m",
                15,
                2,
                false,
                """{"rewardSet":"crystal_p1","target":1000000,"incrementalHitAchievement":"CrystalCracker","finalHitAchievement":"CrystalLegend"}"""
            ),
            (
                "mystics_crystal_s",
                15,
                2,
                false,
                """{"rewardSet":"crystal_p1","target":500000,"incrementalHitAchievement":"CrystalCracker","finalHitAchievement":"CrystalLegend"}"""
            ),
            (
                "ny16_crackable",
                3,
                1,
                false,
                """{"rewardSet":"ny16_1","target":1,"rewards":[{"furni":"clothing_party3"},{"furni":"clothing_party4"},{"furni":"clothing_party5"},{"furni":"clothing_party6"},{"furni":"clothing_party7"},{"furni":"clothing_party8"}]}"""
            ),
            (
                "ny17_crackable",
                3,
                1,
                false,
                """{"rewardSet":"ny17_1","target":1,"rewards":[{"furni":"clothing_r16_party9"},{"furni":"clothing_r16_party10"},{"furni":"clothing_r16_party11"},{"furni":"clothing_r16_party12"},{"furni":"clothing_r16_party13"},{"furni":"clothing_r16_party14"}]}"""
            ),
            (
                "ny18_crackable",
                3,
                1,
                false,
                """{"rewardSet":"ny18_1","target":1,"rewards":[{"furni":"clothing_mini_bowlerhat_blue"},{"furni":"clothing_mini_bowlerhat_cream"},{"furni":"clothing_mini_bowlerhat_dark"},{"furni":"clothing_mini_bowlerhat_green"},{"furni":"clothing_mini_bowlerhat_gold"},{"furni":"clothing_mini_bowlerhat_purple"},{"furni":"clothing_mini_bowlerhat_red"}]}"""
            ),
            (
                "ny_r18_crackable",
                3,
                1,
                false,
                """{"rewardSet":"ny_r18","target":1,"rewards":[{"furni":"clothing_r18_sparkleparty1"},{"furni":"clothing_r18_sparkleparty2"},{"furni":"clothing_r18_sparkleparty3"},{"furni":"clothing_r18_sparkleparty4"},{"furni":"clothing_r18_sparkleparty5"},{"furni":"clothing_r18_sparkleparty6"},{"furni":"clothing_r18_sparkleparty7"},{"furni":"clothing_r18_sparkleparty8"}]}"""
            ),
            (
                "ny_r19_crackable",
                3,
                1,
                false,
                """{"rewardSet":"ny_r19","target":1,"rewards":[{"furni":"clothing_r19_sparklywaist1"},{"furni":"clothing_r19_sparklywaist2"},{"furni":"clothing_r19_sparklywaist3"},{"furni":"clothing_r19_sparklywaist4"},{"furni":"clothing_r19_sparklywaist5"},{"furni":"clothing_r19_sparklywaist6"},{"furni":"clothing_r19_sparklywaist7"},{"furni":"clothing_r19_sparklywaist8"}]}"""
            ),
            (
                "ny_r20_crackable",
                3,
                1,
                false,
                """{"rewardSet":"ny_r20","target":1,"rewards":[{"furni":"clothing_r20_mask1","weight":16},{"furni":"clothing_r20_mask2","weight":12},{"furni":"clothing_r20_mask3","weight":4},{"furni":"clothing_r20_mask4","weight":16},{"furni":"clothing_r20_mask5","weight":12},{"furni":"clothing_r20_mask6","weight":12},{"furni":"clothing_r20_mask7","weight":12},{"furni":"clothing_r20_mask8","weight":16}]}"""
            ),
            (
                "plushie_c20_crackable",
                3,
                1,
                false,
                """{"rewardSet":"plushie20_1","target":1,"rewardPlacement":"Inventory","draws":[{"rewards":[{"furni":"plushie_c20_stuffing"}]},{"rewards":[{"furni":"plushie_c20_dyeneutral","weight":9},{"furni":"plushie_c20_dyepink","weight":9},{"furni":"plushie_c20_dyeblue","weight":9},{"furni":"plushie_c20_dyerainbow"}]},{"rewards":[{"furni":"plushie_c20_fabric3","also":["plushie_c20_fabric3"],"weight":9},{"furni":"plushie_c20_fabric1","also":["plushie_c20_fabric1"],"weight":10},{"furni":"plushie_c20_fabric2","also":["plushie_c20_fabric2"],"weight":9}]},{"rewards":[{"furni":"plushie_c20_fabric3","also":["plushie_c20_dyeneutral"],"weight":9},{"furni":"plushie_c20_fabric1","also":["plushie_c20_dyepink"],"weight":10},{"furni":"plushie_c20_fabric2","also":["plushie_c20_dyeblue"],"weight":9}]}]}"""
            ),
            (
                "santorini_c17_artefact1",
                13,
                1,
                false,
                """{"rewardSet":"santorini_1","target":12,"requiredEffectId":186,"finalHitAchievement":"Restorer","rewards":[{"furni":"clothing_greektoga"}]}"""
            ),
            (
                "santorini_c17_artefact2",
                13,
                1,
                false,
                """{"rewardSet":"santorini_2","target":12,"requiredEffectId":186,"finalHitAchievement":"Restorer","rewards":[{"furni":"clothing_leafcrown"}]}"""
            ),
            (
                "santorini_c17_artefact3",
                13,
                1,
                false,
                """{"rewardSet":"santorini_3","target":12,"requiredEffectId":186,"finalHitAchievement":"Restorer","rewards":[{"furni":"clothing_hermeshat"}]}"""
            ),
            (
                "santorini_c17_artefact4",
                13,
                1,
                false,
                """{"rewardSet":"santorini_4","target":12,"requiredEffectId":186,"finalHitAchievement":"Restorer","rewards":[{"furni":"clothing_hermesshoes"}]}"""
            ),
            (
                "santorini_c17_artefact5",
                13,
                1,
                false,
                """{"rewardSet":"santorini_5","target":12,"requiredEffectId":186,"finalHitAchievement":"Restorer","rewards":[{"furni":"clothing_hoplitehelm"}]}"""
            ),
            (
                "santorini_r17_chest",
                3,
                1,
                false,
                """{"rewardSet":"santorini_6","target":1,"rewards":[{"furni":"santorini_c17_artefact1"},{"furni":"santorini_c17_artefact2"},{"furni":"santorini_c17_artefact3"},{"furni":"santorini_c17_artefact4"},{"furni":"santorini_c17_artefact5"}]}"""
            ),
            (
                "tokyo_c18_gacha",
                3,
                1,
                false,
                """{"rewardSet":"tokyo_1","target":1,"rewards":[{"furni":"tokyo_c18_toy6"},{"furni":"tokyo_c18_toy4"},{"furni":"tokyo_c18_toy2"},{"furni":"tokyo_c18_toy1"},{"furni":"tokyo_c18_toy9"},{"furni":"tokyo_c18_toy10"},{"furni":"tokyo_c18_toy5"},{"furni":"tokyo_c18_toy3"},{"furni":"tokyo_c18_toy7"},{"furni":"tokyo_c18_toy8"}]}"""
            ),
            (
                "xmas_c16_creature1",
                25,
                1,
                false,
                """{"rewardSet":"xmas16_2","target":12,"requiredEffectId":186,"finalHitAchievement":"CreatureRearer","finalHitAchievementCount":5,"rewards":[{"furni":"xmas_c16_creature2"},{"furni":"xmas_c16_creature3"}]}"""
            ),
            (
                "xmas_c16_creature4",
                25,
                1,
                false,
                """{"rewardSet":"xmas16_3","target":12,"requiredEffectId":186,"finalHitAchievement":"CreatureRearer","finalHitAchievementCount":10,"rewards":[{"furni":"xmas_c16_creature5"},{"furni":"xmas_c16_creature6"}]}"""
            ),
            (
                "xmas_c16_creature7",
                25,
                1,
                false,
                """{"rewardSet":"xmas16_4","target":12,"requiredEffectId":186,"finalHitAchievement":"CreatureRearer","finalHitAchievementCount":15,"rewards":[{"furni":"xmas_c16_creature8"},{"furni":"xmas_c16_creature9"}]}"""
            ),
            (
                "xmas_c16_egg",
                25,
                1,
                false,
                """{"rewardSet":"xmas16_1","target":12,"requiredEffectId":186,"finalHitAchievement":"CreatureRearer","finalHitAchievementCount":5,"rewards":[{"furni":"xmas_c16_creature1"},{"furni":"xmas_c16_creature4"},{"furni":"xmas_c16_creature7"}]}"""
            ),
            ("xmas_c16_stocking", 3, 1, false, """{"rewardSet":"xmas16_5","target":1}"""),
            (
                "xmas_c17_book",
                3,
                1,
                false,
                """{"rewardSet":"xmas17_1","target":1,"rewards":[{"furni":"xmas_c17_blueprint1"},{"furni":"xmas_c17_blueprint2"},{"furni":"xmas_c17_blueprint3"},{"furni":"xmas_c17_blueprint4"},{"furni":"xmas_c17_blueprint5"},{"furni":"xmas_c17_blueprint6"}]}"""
            ),
            (
                "xmas_c18_doll1",
                3,
                1,
                false,
                """{"rewardSet":"xmas18_1","target":1,"rewards":[{"furni":"xmas_c18_doll2"},{"furni":"xmas_c18_doll3"},{"furni":"xmas_c18_doll4"},{"furni":"xmas_c18_doll5"}]}"""
            ),
            (
                "xmas_c18_doll10",
                3,
                1,
                false,
                """{"rewardSet":"xmas18_10","target":1,"rewards":[{"furni":"xmas_c18_doll6"},{"furni":"xmas_c18_doll7"},{"furni":"xmas_c18_doll8"},{"furni":"xmas_c18_doll9"}]}"""
            ),
            (
                "xmas_c18_doll2",
                3,
                1,
                false,
                """{"rewardSet":"xmas18_2","target":1,"rewards":[{"furni":"xmas_c18_doll1"},{"furni":"xmas_c18_doll3"},{"furni":"xmas_c18_doll4"},{"furni":"xmas_c18_doll5"}]}"""
            ),
            (
                "xmas_c18_doll3",
                3,
                1,
                false,
                """{"rewardSet":"xmas18_3","target":1,"rewards":[{"furni":"xmas_c18_doll1"},{"furni":"xmas_c18_doll2"},{"furni":"xmas_c18_doll4"},{"furni":"xmas_c18_doll5"}]}"""
            ),
            (
                "xmas_c18_doll4",
                3,
                1,
                false,
                """{"rewardSet":"xmas18_4","target":1,"rewards":[{"furni":"xmas_c18_doll1"},{"furni":"xmas_c18_doll2"},{"furni":"xmas_c18_doll3"},{"furni":"xmas_c18_doll5"}]}"""
            ),
            (
                "xmas_c18_doll5",
                3,
                1,
                false,
                """{"rewardSet":"xmas18_5","target":1,"rewards":[{"furni":"xmas_c18_doll1"},{"furni":"xmas_c18_doll2"},{"furni":"xmas_c18_doll3"},{"furni":"xmas_c18_doll4"}]}"""
            ),
            (
                "xmas_c18_doll6",
                3,
                1,
                false,
                """{"rewardSet":"xmas18_6","target":1,"rewards":[{"furni":"xmas_c18_doll7"},{"furni":"xmas_c18_doll8"},{"furni":"xmas_c18_doll9"},{"furni":"xmas_c18_doll10"}]}"""
            ),
            (
                "xmas_c18_doll7",
                3,
                1,
                false,
                """{"rewardSet":"xmas18_7","target":1,"rewards":[{"furni":"xmas_c18_doll6"},{"furni":"xmas_c18_doll8"},{"furni":"xmas_c18_doll9"},{"furni":"xmas_c18_doll10"}]}"""
            ),
            (
                "xmas_c18_doll8",
                3,
                1,
                false,
                """{"rewardSet":"xmas18_8","target":1,"rewards":[{"furni":"xmas_c18_doll6"},{"furni":"xmas_c18_doll7"},{"furni":"xmas_c18_doll9"},{"furni":"xmas_c18_doll10"}]}"""
            ),
            (
                "xmas_c18_doll9",
                3,
                1,
                false,
                """{"rewardSet":"xmas18_9","target":1,"rewards":[{"furni":"xmas_c18_doll6"},{"furni":"xmas_c18_doll7"},{"furni":"xmas_c18_doll8"},{"furni":"xmas_c18_doll10"}]}"""
            ),
            (
                "xmas_c19_box1",
                3,
                1,
                false,
                """{"rewardSet":"xmas19_box1","target":1,"rewards":[{"furni":"xmas_c19_box2","weight":5},{"furni":"xmas_c19_angelfigure"},{"furni":"xmas_c19_dragonfigure"},{"furni":"xmas_c19_reindeerfigure"},{"furni":"xmas_c19_robinfigure"},{"furni":"xmas_c19_unicornfigure"}]}"""
            ),
            (
                "xmas_c19_box2",
                3,
                1,
                false,
                """{"rewardSet":"xmas19_box2","target":1,"rewards":[{"furni":"xmas_c19_box3","weight":5},{"furni":"xmas_c19_angelfigure"},{"furni":"xmas_c19_dragonfigure"},{"furni":"xmas_c19_reindeerfigure"},{"furni":"xmas_c19_robinfigure"},{"furni":"xmas_c19_unicornfigure"}]}"""
            ),
            (
                "xmas_c19_box3",
                3,
                1,
                false,
                """{"rewardSet":"xmas19_box3","target":1,"rewards":[{"furni":"xmas_c19_box4","weight":5},{"furni":"xmas_c19_angelfigure"},{"furni":"xmas_c19_dragonfigure"},{"furni":"xmas_c19_reindeerfigure"},{"furni":"xmas_c19_robinfigure"},{"furni":"xmas_c19_unicornfigure"}]}"""
            ),
            (
                "xmas_c19_box4",
                3,
                1,
                false,
                """{"rewardSet":"xmas19_box4","target":1,"rewards":[{"furni":"xmas_c19_box5","weight":5},{"furni":"xmas_c19_angelfigure"},{"furni":"xmas_c19_dragonfigure"},{"furni":"xmas_c19_reindeerfigure"},{"furni":"xmas_c19_robinfigure"},{"furni":"xmas_c19_unicornfigure"}]}"""
            ),
            (
                "xmas_c19_box5",
                3,
                1,
                false,
                """{"rewardSet":"xmas19_box5","target":1,"rewards":[{"furni":"xmas_c19_box6","weight":5},{"furni":"xmas_c19_angelfigure"},{"furni":"xmas_c19_dragonfigure"},{"furni":"xmas_c19_reindeerfigure"},{"furni":"xmas_c19_robinfigure"},{"furni":"xmas_c19_unicornfigure"}]}"""
            ),
            (
                "xmas_c19_box6",
                3,
                1,
                false,
                """{"rewardSet":"xmas19_box6","target":1,"rewards":[{"furni":"clothing_icecrown"}]}"""
            ),
            (
                "xmas_c20_runerock",
                3,
                1,
                false,
                """{"rewardSet":"xmas20_1","target":1,"rewards":[{"furni":"xmas_c20_runerockpurple"},{"furni":"xmas_c20_runerockblue"},{"furni":"xmas_c20_runerockyellow"},{"furni":"xmas_c20_runerockred"},{"furni":"xmas_c20_runerockgreen"}]}"""
            ),
            (
                "xmas_c20_runerockblue",
                3,
                1,
                false,
                """{"rewardSet":"xmas20_2","target":1,"rewards":[{"furni":"xmas_c20_runerockpurple"},{"furni":"xmas_c20_runerockyellow"},{"furni":"xmas_c20_runerockred"},{"furni":"xmas_c20_runerockgreen"}]}"""
            ),
            (
                "xmas_c20_runerockgreen",
                3,
                1,
                false,
                """{"rewardSet":"xmas20_3","target":1,"rewards":[{"furni":"xmas_c20_runerockpurple"},{"furni":"xmas_c20_runerockblue"},{"furni":"xmas_c20_runerockyellow"},{"furni":"xmas_c20_runerockred"}]}"""
            ),
            (
                "xmas_c20_runerockpurple",
                3,
                1,
                false,
                """{"rewardSet":"xmas20_4","target":1,"rewards":[{"furni":"xmas_c20_runerockblue"},{"furni":"xmas_c20_runerockyellow"},{"furni":"xmas_c20_runerockred"},{"furni":"xmas_c20_runerockgreen"}]}"""
            ),
            (
                "xmas_c20_runerockred",
                3,
                1,
                false,
                """{"rewardSet":"xmas20_5","target":1,"rewards":[{"furni":"xmas_c20_runerockpurple"},{"furni":"xmas_c20_runerockblue"},{"furni":"xmas_c20_runerockyellow"},{"furni":"xmas_c20_runerockgreen"}]}"""
            ),
            (
                "xmas_c20_runerockyellow",
                3,
                1,
                false,
                """{"rewardSet":"xmas20_6","target":1,"rewards":[{"furni":"xmas_c20_runerockpurple"},{"furni":"xmas_c20_runerockblue"},{"furni":"xmas_c20_runerockred"},{"furni":"xmas_c20_runerockgreen"}]}"""
            ),
            (
                "bonusbag22_1",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare22_1*0"},{"furni":"bonusrare22_1*1"},{"furni":"bonusrare22_1*2"},{"furni":"bonusrare22_1*3"},{"furni":"bonusrare22_1*4"},{"furni":"bonusrare22_1*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag22_2",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare22_2*0"},{"furni":"bonusrare22_2*1"},{"furni":"bonusrare22_2*2"},{"furni":"bonusrare22_2*3"},{"furni":"bonusrare22_2*4"},{"furni":"bonusrare22_2*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag22_3",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare22_3*0"},{"furni":"bonusrare22_3*1"},{"furni":"bonusrare22_3*2"},{"furni":"bonusrare22_3*3"},{"furni":"bonusrare22_3*4"},{"furni":"bonusrare22_3*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag22_4",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare22_4*0"},{"furni":"bonusrare22_4*1"},{"furni":"bonusrare22_4*2"},{"furni":"bonusrare22_4*3"},{"furni":"bonusrare22_4*4"},{"furni":"bonusrare22_4*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag23_1",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare23_1*0"},{"furni":"bonusrare23_1*1"},{"furni":"bonusrare23_1*2"},{"furni":"bonusrare23_1*3"},{"furni":"bonusrare23_1*4"},{"furni":"bonusrare23_1*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag23_2",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare23_2*0"},{"furni":"bonusrare23_2*1"},{"furni":"bonusrare23_2*2"},{"furni":"bonusrare23_2*3"},{"furni":"bonusrare23_2*4"},{"furni":"bonusrare23_2*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag23_3",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare23_3*0"},{"furni":"bonusrare23_3*1"},{"furni":"bonusrare23_3*2"},{"furni":"bonusrare23_3*3"},{"furni":"bonusrare23_3*4"},{"furni":"bonusrare23_3*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag23_4",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare23_4*0"},{"furni":"bonusrare23_4*1"},{"furni":"bonusrare23_4*2"},{"furni":"bonusrare23_4*3"},{"furni":"bonusrare23_4*4"},{"furni":"bonusrare23_4*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag24_1",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare24_1_a*1"},{"furni":"bonusrare24_1_a*2"},{"furni":"bonusrare24_1_a*3"},{"furni":"bonusrare24_1_a*4"},{"furni":"bonusrare24_1_a*5"},{"furni":"bonusrare24_1_a*6"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag24_2",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare24_2*0"},{"furni":"bonusrare24_2*1"},{"furni":"bonusrare24_2*2"},{"furni":"bonusrare24_2*3"},{"furni":"bonusrare24_2*4"},{"furni":"bonusrare24_2*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag24_3",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare24_3_a*1"},{"furni":"bonusrare24_3_a*2"},{"furni":"bonusrare24_3_a*3"},{"furni":"bonusrare24_3_a*4"},{"furni":"bonusrare24_3_a*5"},{"furni":"bonusrare24_3_a*6"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag24_4",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare24_4*0"},{"furni":"bonusrare24_4*1"},{"furni":"bonusrare24_4*2"},{"furni":"bonusrare24_4*3"},{"furni":"bonusrare24_4*4"},{"furni":"bonusrare24_4*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag25_1",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare25_1*0"},{"furni":"bonusrare25_1*1"},{"furni":"bonusrare25_1*2"},{"furni":"bonusrare25_1*3"},{"furni":"bonusrare25_1*4"},{"furni":"bonusrare25_1*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag25_2",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare25_2*0"},{"furni":"bonusrare25_2*1"},{"furni":"bonusrare25_2*2"},{"furni":"bonusrare25_2*3"},{"furni":"bonusrare25_2*4"},{"furni":"bonusrare25_2*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag25_3",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare25_3*0"},{"furni":"bonusrare25_3*1"},{"furni":"bonusrare25_3*2"},{"furni":"bonusrare25_3*3"},{"furni":"bonusrare25_3*4"},{"furni":"bonusrare25_3*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag25_4",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare25_4*0"},{"furni":"bonusrare25_4*1"},{"furni":"bonusrare25_4*2"},{"furni":"bonusrare25_4*3"},{"furni":"bonusrare25_4*4"},{"furni":"bonusrare25_4*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag26_1",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare26_1*0"},{"furni":"bonusrare26_1*1"},{"furni":"bonusrare26_1*2"},{"furni":"bonusrare26_1*3"},{"furni":"bonusrare26_1*4"},{"furni":"bonusrare26_1*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag26_2",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare26_2*0"},{"furni":"bonusrare26_2*1"},{"furni":"bonusrare26_2*2"},{"furni":"bonusrare26_2*3"},{"furni":"bonusrare26_2*4"},{"furni":"bonusrare26_2*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            (
                "bonusbag26_3",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"bonusrare26_3*0"},{"furni":"bonusrare26_3*1"},{"furni":"bonusrare26_3*2"},{"furni":"bonusrare26_3*3"},{"furni":"bonusrare26_3*4"},{"furni":"bonusrare26_3*5"},{"credits":5,"weight":1},{"subscription":"HabboClub","subscriptionDays":3,"weight":1}]}"""
            ),
            ("bonusbag26_4", 3, 1, false, """{"target":1}"""),
            (
                "br_c25_faunacrate",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"br_c25_poisondartfrog","weight":33},{"furni":"br_c25_parrot","weight":33},{"furni":"br_c25_toucan","weight":33},{"furni":"br_c25_jaguar"}]}"""
            ),
            (
                "br_c25_floracrate",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"br_c25_lilies","weight":33},{"furni":"br_c25_orchids","weight":33},{"furni":"br_c25_passionflower","weight":33},{"furni":"br_c25_cocoatree"}]}"""
            ),
            (
                "bubblejuice_c21_crackable1",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"bubblejuice_c21_hops","also":["bubblejuice_c21_barley","bubblejuice_c21_citrus"],"weight":10},{"furni":"bubblejuice_c21_hops","also":["bubblejuice_c21_hops","bubblejuice_c21_hops"],"weight":10},{"furni":"bubblejuice_c21_barley","also":["bubblejuice_c21_barley","bubblejuice_c21_barley"],"weight":10},{"furni":"bubblejuice_c21_citrus","also":["bubblejuice_c21_citrus","bubblejuice_c21_citrus"],"weight":10},{"furni":"bubblejuice_c21_hops","also":["bubblejuice_c21_hops","bubblejuice_c21_barley","bubblejuice_c21_barley"],"weight":10},{"furni":"bubblejuice_c21_hops","also":["bubblejuice_c21_hops","bubblejuice_c21_citrus","bubblejuice_c21_citrus"],"weight":10},{"furni":"bubblejuice_c21_barley","also":["bubblejuice_c21_barley","bubblejuice_c21_citrus","bubblejuice_c21_citrus"],"weight":10},{"furni":"bubblejuice_c21_barley","also":["bubblejuice_c21_barley","bubblejuice_c21_hops","bubblejuice_c21_hops"],"weight":10},{"furni":"bubblejuice_c21_citrus","also":["bubblejuice_c21_citrus","bubblejuice_c21_hops","bubblejuice_c21_hops"],"weight":10},{"furni":"bubblejuice_c21_citrus","also":["bubblejuice_c21_citrus","bubblejuice_c21_barley","bubblejuice_c21_barley"],"weight":10}]}"""
            ),
            (
                "bubblejuice_c21_crackable2",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"bubblejuice_c21_hops","also":["bubblejuice_c21_hops","bubblejuice_c21_barley","bubblejuice_c21_barley","bubblejuice_c21_citrus","bubblejuice_c21_citrus"],"weight":40},{"furni":"bubblejuice_c21_hops","also":["bubblejuice_c21_hops","bubblejuice_c21_barley","bubblejuice_c21_barley","bubblejuice_c21_citrus","bubblejuice_c21_citrus","bubblejuice_c21_secretingredient"],"weight":30},{"furni":"bubblejuice_c21_sofa","weight":15},{"furni":"bubblejuice_c21_dartboard","weight":10},{"furni":"bubblejuice_c21_neonsign","weight":5}]}"""
            ),
            (
                "circus_c24_bluetentcrackable",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"circus_c24_cardshat","also":["circus_c24_bunnyhat","circus_c24_duckhat"],"weight":14},{"furni":"circus_c24_bunnyhat","also":["circus_c24_bunnyhat","circus_c24_cardshat"],"weight":14},{"furni":"circus_c24_cardshat","also":["circus_c24_cardshat","circus_c24_duckhat"],"weight":14},{"furni":"circus_c24_duckhat","also":["circus_c24_duckhat","circus_c24_bunnyhat"],"weight":14},{"furni":"circus_c24_cardshat","also":["circus_c24_bunnyhat","circus_c24_ball"],"weight":14},{"furni":"circus_c24_duckhat","also":["circus_c24_bunnyhat","circus_c24_ball"],"weight":14},{"furni":"circus_c24_duckhat","also":["circus_c24_cardshat","circus_c24_ball"],"weight":14},{"furni":"circus_c24_cardshat","also":["circus_c24_bunnyhat","circus_c24_duckhat","clothing_clownhat"]},{"furni":"circus_c24_cardshat","also":["circus_c24_duckhat","circus_c24_bunnyhat","clothing_clowntop"]}]}"""
            ),
            (
                "circus_c24_redtentcrackable",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"circus_c24_gymnastsgroup","weight":20},{"furni":"circus_c24_unicyclist","weight":20},{"furni":"circus_c24_acrobat","weight":20},{"furni":"circus_c24_puppyact","weight":15},{"furni":"circus_c24_mouseact","weight":15},{"furni":"circus_c24_ponyact","weight":10}]}"""
            ),
            (
                "diamond_c21_giftbox",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"clothing_diahermeshat","weight":10},{"furni":"clothing_diaelegantcrown","weight":20},{"furni":"exe_c15_telephone","weight":100},{"furni":"tokyo_c18_magazinerack","weight":90},{"furni":"hygge_c18_stove","weight":90},{"furni":"bling_shwr","weight":90},{"furni":"band_c19_drums","weight":90},{"furni":"attic15_carpet","weight":90},{"furni":"clothing_nutrainers","weight":75},{"furni":"clothing_denimshorts","weight":75},{"furni":"clothing_leathertrousers","weight":75},{"furni":"clothing_chestbag","weight":75},{"furni":"clothing_grandetail","weight":70}]}"""
            ),
            (
                "diamond_c22_giftbox",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"clothing_diafeathercrown","weight":10},{"furni":"clothing_diaturban","weight":20},{"furni":"clothing_diahalo","weight":50},{"furni":"rainyday_c20_paintset","weight":100},{"furni":"bling_toilet","weight":90},{"furni":"olympics_c16_treadmill","weight":90},{"furni":"js_jetski2","weight":90},{"furni":"antique_c21_armchair","weight":90},{"furni":"clothing_elegantponytail","weight":70},{"furni":"clothing_pompomhat","weight":75},{"furni":"clothing_leatherhoodie","weight":75},{"furni":"clothing_beetshirt","weight":75},{"furni":"clothing_sportsshade","weight":75}]}"""
            ),
            (
                "diamond_c25_giftbox4",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"clothing_diasparkleparty","weight":10},{"furni":"clothing_diaplume","weight":20},{"furni":"clothing_diastar","weight":50},{"furni":"easter_c17_sprinkler","weight":100},{"furni":"easter_c24_bed","weight":90},{"furni":"nyc_c23_wallposters","weight":90},{"furni":"santorini_c17_donkey","weight":90},{"furni":"easter_c22_tree","weight":90},{"furni":"easter_c19_logtable","weight":90},{"furni":"clothing_plaitedbunhair","weight":70},{"furni":"clothing_animalprint","weight":75},{"furni":"clothing_gardenapron","weight":75},{"furni":"clothing_satchel","weight":75},{"furni":"rainyday_c20_retrogames","weight":75}]}"""
            ),
            (
                "dino_c22_fossilrock",
                3,
                1,
                false,
                """{"target":1,"requiredEffectIds":[182,183],"rewards":[{"furni":"dino_c22_bigfoot"},{"furni":"dino_c22_bigteeth"},{"furni":"dino_c22_bonyplate"},{"furni":"dino_c22_clawedfoot"},{"furni":"dino_c22_flipper"},{"furni":"dino_c22_smallteeth"}]}"""
            ),
            (
                "disco_c26_discoball",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"disco_c26_walkman","also":["disco_c26_helmet","disco_c26_wheels"]},{"furni":"disco_c26_walkman","also":["disco_c26_lightstick","disco_c26_tool"]},{"furni":"disco_c26_walkman","also":["disco_c26_heartglitter","disco_c26_candyheart"]},{"furni":"disco_c26_helmet","also":["disco_c26_starglitter","disco_c26_lightstick"]},{"furni":"disco_c26_helmet","also":["disco_c26_wheels","disco_c26_tool"]},{"furni":"disco_c26_candyheart","also":["disco_c26_lightstick","disco_c26_tool"]},{"furni":"disco_c26_candyheart","also":["disco_c26_heartglitter","disco_c26_wheels"]},{"furni":"disco_c26_heartglitter","also":["disco_c26_starglitter","disco_c26_starglitter"]}]}"""
            ),
            (
                "dream_c24_dreammirror",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"dream_c24_moonshard","also":["dream_c24_moonshard","dream_c24_cloudplant"]},{"furni":"dream_c24_moonshard","also":["dream_c24_puddle","dream_c24_puddle"]},{"furni":"dream_c24_tinyrainbow","also":["dream_c24_cloudplant","dream_c24_cloudplant"]},{"furni":"dream_c24_tinyrainbow","also":["dream_c24_cloudplant","dream_c24_puddle"]},{"furni":"dream_c24_tinyrainbow","also":["dream_c24_cloudplant","dream_c24_moonshard"]},{"furni":"dream_c24_tinyrainbow","also":["dream_c24_puddle","dream_c24_moonshard"]},{"furni":"dream_c24_tinyrainbow","also":["dream_c24_tinyrainbow","dream_c24_puddle"]},{"furni":"dream_c24_tinyrainbow","also":["dream_c24_tinyrainbow","dream_c24_tinyrainbow"]}]}"""
            ),
            (
                "dream_c24_storybook",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"dream_c24_storyboat","also":["dream_c24_storyboat","dream_c24_storyboat"]},{"furni":"dream_c24_storyplane","also":["dream_c24_storyplane","dream_c24_storyplane"]},{"furni":"dream_c24_storyswan","also":["dream_c24_storyswan","dream_c24_storyswan"]},{"furni":"dream_c24_storybookpages","also":["dream_c24_storybookpages","dream_c24_storybookpages"]},{"furni":"dream_c24_storyboat","also":["dream_c24_storyboat","dream_c24_storyplane"]},{"furni":"dream_c24_storyswan","also":["dream_c24_storyswan","dream_c24_storybookpages"]},{"furni":"dream_c24_storybookpages","also":["dream_c24_storybookpages","dream_c24_storyplane"]},{"furni":"dream_c24_storyboat","also":["dream_c24_storyplane","dream_c24_storyswan"]}]}"""
            ),
            (
                "easter_c22_aerialbugs1",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"easter_c22_morpho1","also":["easter_c22_agrias1","easter_c22_dragonfly1"]}]}"""
            ),
            (
                "easter_c22_aerialbugs2",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"easter_c22_morpho2","also":["easter_c22_agrias2","easter_c22_dragonfly2"]}]}"""
            ),
            (
                "easter_c22_apparelbag",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"clothing_ladybirdbag","weight":45},{"furni":"clothing_butterflyhair","weight":45},{"furni":"clothing_snailfriend","weight":10}]}"""
            ),
            (
                "easter_c22_bugkit",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"easter_c22_apparelbag"},{"furni":"easter_c22_groundbugs1","weight":33},{"furni":"easter_c22_groundbugs3","weight":33},{"furni":"easter_c22_aerialbugs1","weight":33}]}"""
            ),
            (
                "easter_c22_deluxebugkit",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"easter_c22_groundbugs2","also":["easter_c22_apparelbag"]},{"furni":"easter_c22_groundbugs4","also":["easter_c22_apparelbag"]},{"furni":"easter_c22_aerialbugs2","also":["easter_c22_apparelbag"]}]}"""
            ),
            (
                "easter_c22_groundbugs1",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"easter_c22_scarab1","also":["easter_c22_ladybird1","easter_c22_sawstagbeetle1"]}]}"""
            ),
            (
                "easter_c22_groundbugs2",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"easter_c22_scarab2","also":["easter_c22_ladybird2","easter_c22_sawstagbeetle2"]}]}"""
            ),
            (
                "easter_c22_groundbugs3",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"easter_c22_mantis1","also":["easter_c22_goliathbeetle1","easter_c22_caterpillar1"]}]}"""
            ),
            (
                "easter_c22_groundbugs4",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"easter_c22_mantis2","also":["easter_c22_goliathbeetle2","easter_c22_caterpillar2"]}]}"""
            ),
            ("easter_c23_craftbotcrackable", 9, 1, false, """{"target":8}"""),
            (
                "easter_c23_recylingbin",
                3,
                1,
                false,
                """{"target":1,"requiredEffectId":137,"rewardPlacement":"Inventory","rewards":[{"furni":"clothing_trash","weight":10},{"furni":"easter_c23_pileofclothes","also":["easter_c23_pileofmixedfood","easter_c23_pileofoldtech"],"weight":9},{"furni":"easter_c23_pileofclothes","also":["easter_c23_pileofclothes","easter_c23_pileofclothes"],"weight":9},{"furni":"easter_c23_pileofmixedfood","also":["easter_c23_pileofmixedfood","easter_c23_pileofmixedfood"],"weight":9},{"furni":"easter_c23_pileofoldtech","also":["easter_c23_pileofoldtech","easter_c23_pileofoldtech"],"weight":9},{"furni":"easter_c23_pileofclothes","also":["easter_c23_pileofclothes","easter_c23_pileofmixedfood"],"weight":9},{"furni":"easter_c23_pileofmixedfood","also":["easter_c23_pileofmixedfood","easter_c23_pileofoldtech"],"weight":9},{"furni":"easter_c23_pileofoldtech","also":["easter_c23_pileofoldtech","easter_c23_pileofclothes"],"weight":9},{"furni":"easter_c23_pileofclothes","also":["easter_c23_pileofclothes","easter_c23_pileofoldtech"],"weight":9},{"furni":"easter_c23_pileofmixedfood","also":["easter_c23_pileofmixedfood","easter_c23_pileofclothes"],"weight":9},{"furni":"easter_c23_pileofoldtech","also":["easter_c23_pileofoldtech","easter_c23_pileofmixedfood"],"weight":9}]}"""
            ),
            (
                "easter_c23_solarbox",
                3,
                1,
                false,
                """{"target":1,"requiredEffectId":137,"rewards":[{"furni":"easter_c23_solarenergy","weight":10},{"furni":"easter_c23_bigbattery","weight":30},{"furni":"easter_c23_battery","weight":60}]}"""
            ),
            (
                "easter_c24_airseed",
                5,
                1,
                false,
                """{"target":4,"requiredEffectId":6,"rewards":[{"furni":"easter_c24_airfruit","weight":95},{"furni":"easter_c24_shinyairfruit","weight":5}]}"""
            ),
            (
                "easter_c24_darkseed",
                5,
                1,
                false,
                """{"target":4,"requiredEffectId":180,"rewards":[{"furni":"easter_c24_darkfruit","weight":95},{"furni":"easter_c24_shinydarkfruit","weight":5}]}"""
            ),
            (
                "easter_c24_earthseed",
                5,
                1,
                false,
                """{"target":4,"rewards":[{"furni":"easter_c24_earthfruit","weight":95},{"furni":"easter_c24_shinyearthfruit","weight":5}]}"""
            ),
            (
                "easter_c24_elementalseed",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"easter_c24_airseed"},{"furni":"easter_c24_earthseed"},{"furni":"easter_c24_fireseed"},{"furni":"easter_c24_waterseed"}]}"""
            ),
            (
                "easter_c24_fairyseed",
                5,
                1,
                false,
                """{"target":4,"requiredEffectId":186,"rewards":[{"furni":"easter_c24_fairyfruit","weight":95},{"furni":"easter_c24_shinyfairyfruit","weight":5}]}"""
            ),
            (
                "easter_c24_fireseed",
                5,
                1,
                false,
                """{"target":4,"requiredEffectId":5,"rewards":[{"furni":"easter_c24_firefruit","weight":95},{"furni":"easter_c24_shinyfirefruit","weight":5}]}"""
            ),
            (
                "easter_c24_lightseed",
                5,
                1,
                false,
                """{"target":4,"requiredEffectId":137,"rewards":[{"furni":"easter_c24_lightfruit","weight":95},{"furni":"easter_c24_shinylightfruit","weight":5}]}"""
            ),
            (
                "easter_c24_rainbowseed",
                5,
                1,
                false,
                """{"target":4,"requiredEffectId":7,"rewards":[{"furni":"easter_c24_rainbowfruit","weight":95},{"furni":"easter_c24_shinyrainbowfruit","weight":5}]}"""
            ),
            (
                "easter_c24_waterseed",
                5,
                1,
                false,
                """{"target":4,"requiredEffectId":192,"rewards":[{"furni":"easter_c24_waterfruit","weight":95},{"furni":"easter_c24_shinywaterfruit","weight":5}]}"""
            ),
            (
                "easter_c25_designerbag",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"easter_c25_blueprint1","also":["easter_c25_blueprint3","easter_c25_craftingtools"],"weight":40},{"furni":"easter_c25_blueprint3","also":["easter_c25_blueprint3","easter_c25_blueprint3"],"weight":20},{"furni":"easter_c25_blueprint1","also":["easter_c25_blueprint1","easter_c25_blueprint1"],"weight":20},{"furni":"easter_c25_craftingtools","also":["easter_c25_craftingtools","easter_c25_craftingtools"],"weight":20}]}"""
            ),
            (
                "easter_c25_designerbag2",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"easter_c25_blueprint4","also":["easter_c25_blueprint2","easter_c25_hairgel"],"weight":40},{"furni":"easter_c25_blueprint2","also":["easter_c25_blueprint2","easter_c25_blueprint2"],"weight":20},{"furni":"easter_c25_blueprint4","also":["easter_c25_blueprint4","easter_c25_blueprint4"],"weight":20},{"furni":"easter_c25_hairgel","also":["easter_c25_hairgel","easter_c25_hairgel"],"weight":20}]}"""
            ),
            (
                "easter_c26_elvenbox",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"easter_c26_elvenegg1","also":["easter_c26_elvenegg1","easter_c26_elvenorganicmat"]},{"furni":"easter_c26_elvenegg2","also":["easter_c26_elvenegg2","easter_c26_elvenorganicmat"]},{"furni":"easter_c26_elvenegg4","also":["easter_c26_elvenegg4","easter_c26_elvencloth"]},{"furni":"easter_c26_elvenegg3","also":["easter_c26_elvenegg3","easter_c26_elvencloth"]},{"furni":"easter_c26_elvenegg1","also":["easter_c26_elvenegg3","easter_c26_elvenegg2"]},{"furni":"easter_c26_elvenegg4","also":["easter_c26_elvencloth","easter_c26_elvenorganicmat"]}]}"""
            ),
            (
                "fall_c23_deluxebasket",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"fall_c23_campfire","also":["fall_c23_phonecam","fall_c23_picnicblanket","clothing_habbobeanie"],"weight":12},{"furni":"fall_c23_campfire","also":["clothing_fluffyjacket","clothing_habbobeanie","fall_c23_leafpile"],"weight":26},{"furni":"fall_c23_phonecam","also":["clothing_pinaforedress","clothing_docfranks","fall_c23_leafcurtains"],"weight":26},{"furni":"fall_c23_picnicblanket","also":["fall_c23_shiba","clothing_knittedbunnybeanie","fall_c23_leafyfloor"],"weight":26},{"furni":"fall_c23_shiba","also":["fall_c23_samoyed","clothing_fluffyhat","clothing_docfranks"],"weight":5},{"furni":"clothing_fluffyjacket","also":["fall_c23_samoyed","clothing_fluffyhat","clothing_docfranks"],"weight":5}]}"""
            ),
            (
                "fall_c23_picnicbasket",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"fall_c23_leafpile","also":["fall_c23_leafyfloor","fall_c23_leafcurtains"],"weight":12},{"furni":"fall_c23_leafpile","also":["fall_c23_picnicblanket","clothing_knittedbunnybeanie"],"weight":26},{"furni":"clothing_habbobeanie","also":["fall_c23_phonecam","fall_c23_leafcurtains"],"weight":26},{"furni":"fall_c23_campfire","also":["fall_c23_leafyfloor","clothing_pinaforedress"],"weight":26},{"furni":"fall_c23_leafpile","also":["fall_c23_leafyfloor","fall_c23_shiba","fall_c23_leafcurtains"],"weight":5},{"furni":"fall_c23_leafpile","also":["fall_c23_leafyfloor","clothing_fluffyjacket","fall_c23_leafcurtains"],"weight":5}]}"""
            ),
            (
                "fantasy_c22_treasure1",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"fantasy_c22_metal","also":["fantasy_c22_metal","fantasy_c22_metal","fantasy_c22_bluepotion"]},{"furni":"fantasy_c22_wood","also":["fantasy_c22_wood","fantasy_c22_wood","fantasy_c22_greenpotion"]},{"furni":"fantasy_c22_cloth","also":["fantasy_c22_cloth","fantasy_c22_cloth","fantasy_c22_bluepotion"]},{"furni":"fantasy_c22_metal","also":["fantasy_c22_wood","fantasy_c22_cloth","fantasy_c22_greenpotion"]},{"furni":"fantasy_c22_metal","also":["fantasy_c22_metal","fantasy_c22_cloth","fantasy_c22_bluepotion"]},{"furni":"fantasy_c22_wood","also":["fantasy_c22_wood","fantasy_c22_metal","fantasy_c22_greenpotion"]},{"furni":"fantasy_c22_cloth","also":["fantasy_c22_cloth","fantasy_c22_wood","fantasy_c22_bluepotion"]},{"furni":"fantasy_c22_metal","also":["fantasy_c22_metal","fantasy_c22_wood","fantasy_c22_greenpotion"]},{"furni":"fantasy_c22_wood","also":["fantasy_c22_wood","fantasy_c22_cloth","fantasy_c22_redpotion"]},{"furni":"fantasy_c22_cloth","also":["fantasy_c22_cloth","fantasy_c22_metal","fantasy_c22_redpotion"]}]}"""
            ),
            (
                "fantasy_c22_treasure2",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"fantasy_c22_bluepotion","also":["fantasy_c22_bluepotion","fantasy_c22_bluepotion","fantasy_c22_rune"]},{"furni":"fantasy_c22_greenpotion","also":["fantasy_c22_greenpotion","fantasy_c22_greenpotion","fantasy_c22_coins"]},{"furni":"fantasy_c22_redpotion","also":["fantasy_c22_redpotion","fantasy_c22_redpotion","fantasy_c22_rune"]},{"furni":"fantasy_c22_bluepotion","also":["fantasy_c22_greenpotion","fantasy_c22_redpotion","fantasy_c22_coins"]},{"furni":"fantasy_c22_bluepotion","also":["fantasy_c22_bluepotion","fantasy_c22_redpotion","fantasy_c22_rune"]},{"furni":"fantasy_c22_greenpotion","also":["fantasy_c22_greenpotion","fantasy_c22_bluepotion","fantasy_c22_coins"]},{"furni":"fantasy_c22_redpotion","also":["fantasy_c22_redpotion","fantasy_c22_greenpotion","fantasy_c22_rune"]},{"furni":"fantasy_c22_bluepotion","also":["fantasy_c22_bluepotion","fantasy_c22_greenpotion","fantasy_c22_coins"]},{"furni":"fantasy_c22_greenpotion","also":["fantasy_c22_greenpotion","fantasy_c22_redpotion","fantasy_c22_crystal"]},{"furni":"fantasy_c22_redpotion","also":["fantasy_c22_redpotion","fantasy_c22_bluepotion","fantasy_c22_crystal"]}]}"""
            ),
            (
                "gacha_c25_gachamachine",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"gacha_c25_tiger"},{"furni":"gacha_c25_duck"},{"furni":"gacha_c25_octopus"},{"furni":"gacha_c25_sheep"},{"furni":"gacha_c25_hyena"},{"furni":"gacha_c25_owl"}]}"""
            ),
            (
                "gacha_c25_royalgacha",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"gacha_c25_royaltiger"},{"furni":"gacha_c25_royalduck"},{"furni":"gacha_c25_royaloctopus"},{"furni":"gacha_c25_royalsheep"},{"furni":"gacha_c25_royalhyena"},{"furni":"gacha_c25_royalowl"}]}"""
            ),
            (
                "giftbox_c25_crackable1",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"clothing_bunnyeyepatch","weight":5},{"furni":"clothing_bunnyface","weight":5},{"furni":"clothing_fashionsunglasses","weight":5},{"furni":"clothing_bunnyearrings","weight":5},{"furni":"clothing_thinscarf","weight":5},{"furni":"easter_c19_turnipbuddies","weight":10},{"furni":"easter_c22_barrelcactus","weight":15},{"furni":"easter_c19_meadow","weight":10},{"furni":"easter_c18_dragonflies","weight":10},{"furni":"easter_c20_heather","weight":10},{"furni":"easter_c17_appletree","weight":10}]}"""
            ),
            (
                "giftbox_c25_crackable2",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"clothing_survivalbackpack","weight":10},{"furni":"clothing_zebratop","weight":10},{"furni":"clothing_backcap","weight":10},{"furni":"clothing_sportsshade","weight":10},{"furni":"tent_camo","weight":10},{"furni":"army_c15_compass","weight":10},{"furni":"xmas14_hammock","weight":10},{"furni":"jungle_c16_tele","weight":5},{"furni":"clothing_sunburntface","weight":5},{"furni":"clothing_brnecklace","weight":5}]}"""
            ),
            (
                "giftbox_c25_crackable3",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"tiki_waterfall","weight":4},{"furni":"bw_shower","weight":14},{"furni":"coco_divan_c3","weight":14},{"furni":"st_palooza_balloons","weight":2},{"furni":"clothing_discohead","weight":8},{"furni":"jetset_tent","weight":14},{"furni":"clothing_armfloats","weight":8},{"furni":"xmas14_recliner","weight":14},{"furni":"fest_c19_coalicecream","weight":8},{"furni":"hblooza_icecream","weight":2},{"furni":"anc_sun","weight":4},{"furni":"paris_c15_parasol","weight":8}]}"""
            ),
            ("giftbox_c25_crackable4", 3, 1, false, """{"target":1}"""),
            (
                "giftbox_c25_crackable5",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"xmas_c25_penguin1","weight":16},{"furni":"xmas_c25_penguin2","weight":15},{"furni":"xmas_c25_penguin3","weight":13},{"furni":"xmas_c25_penguin4","weight":13},{"furni":"xmas_c25_penguin5","weight":12},{"furni":"xmas_c25_penguin6","weight":11},{"furni":"xmas_c25_penguin7","weight":11},{"furni":"xmas_c25_penguin8","weight":10}]}"""
            ),
            ("giftbox_c25_crackable6", 3, 1, false, """{"target":1}"""),
            (
                "giftbox_c25_crackable7",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"clothing_survivalbackpack","weight":16},{"furni":"easter_c20_campingessentials","weight":16},{"furni":"army_c15_bed","weight":12},{"furni":"clothing_nosebandage","weight":10},{"furni":"clothing_cheekbandage","weight":10},{"furni":"clothing_bandagedtorso","weight":8},{"furni":"clothing_bandagedhead","weight":8},{"furni":"tent_blue","weight":6},{"furni":"tent_orange","weight":6},{"furni":"clothing_respirator","weight":5},{"furni":"clothing_mouldytoastbackpack","weight":3}]}"""
            ),
            (
                "graffiti_c24_spraypaints",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"graffiti_c24_tealspray","also":["graffiti_c24_tealspray","graffiti_c24_tealspray","graffiti_c24_bluespray"]},{"furni":"graffiti_c24_bluespray","also":["graffiti_c24_bluespray","graffiti_c24_redspray","graffiti_c24_pinkspray"]},{"furni":"graffiti_c24_pinkspray","also":["graffiti_c24_pinkspray","graffiti_c24_yellowspray","graffiti_c24_yellowspray"]},{"furni":"graffiti_c24_bluespray","also":["graffiti_c24_redspray","graffiti_c24_yellowspray","graffiti_c24_yellowspray"]},{"furni":"graffiti_c24_tealspray","also":["graffiti_c24_yellowspray","graffiti_c24_redspray","graffiti_c24_redspray"]},{"furni":"graffiti_c24_pinkspray","also":["graffiti_c24_pinkspray","graffiti_c24_bluespray","graffiti_c24_yellowspray"]},{"furni":"graffiti_c24_tealspray","also":["graffiti_c24_tealspray","graffiti_c24_pinkspray","graffiti_c24_redspray"]},{"furni":"graffiti_c24_bluespray","also":["graffiti_c24_bluespray","graffiti_c24_yellowspray","graffiti_c24_pinkspray"]}]}"""
            ),
            (
                "graffiti_c24_tv",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"graffiti_c24_gloves","also":["graffiti_c24_gloves","graffiti_c24_bricks","graffiti_c24_bricks"],"weight":142},{"furni":"graffiti_c24_gloves","also":["graffiti_c24_gloves","graffiti_c24_flashlight","graffiti_c24_bricks"],"weight":284},{"furni":"graffiti_c24_gloves","also":["graffiti_c24_bricks","graffiti_c24_flashlight","graffiti_c24_flashlight"],"weight":142},{"furni":"clothing_splattermask","also":["graffiti_c24_bricks","graffiti_c24_flashlight","graffiti_c24_flashlight"],"weight":142},{"furni":"clothing_splattermask","also":["graffiti_c24_gloves","graffiti_c24_flashlight","graffiti_c24_flashlight"],"weight":142},{"furni":"clothing_splattermask","also":["clothing_splattermask","graffiti_c24_bricks","graffiti_c24_bricks"],"weight":142}]}"""
            ),
            (
                "habbo25_c25_bdayballoon",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"uni_c23_bubblejuicepong","weight":140},{"furni":"clothing_megaoversizedtshirt","weight":140},{"furni":"fest_c19_falafelstall","weight":120},{"furni":"vwave_c21_frenchbulldog","weight":120},{"furni":"clothing_balloonpigtails","weight":120},{"furni":"fest_c19_loverstent","weight":100},{"furni":"clothing_retrosuit","weight":100},{"furni":"clothing_poolpartyshades","weight":60},{"furni":"clothing_catfloat","weight":60},{"furni":"habbo25_c25_bdayballoon","weight":36},{"furni":"habbo25_c25_bdayballoon2","weight":4}]}"""
            ),
            (
                "habbo25_c25_bdayballoon2",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"barrier*4","weight":13},{"furni":"minirare_sleepingbag*10","weight":13},{"furni":"minirare_scifirocket*1","weight":11},{"furni":"minirare_elephant_statue*4","weight":10},{"furni":"minirare_pillow*9","weight":10},{"furni":"minirare_dragonlamp*0","weight":9},{"furni":"scifiport*11","weight":8},{"furni":"rare_fountain*7","weight":8},{"furni":"rare_parasol*5","weight":7},{"furni":"rare_fan*11","weight":7},{"furni":"rare_icecream*12","weight":4}]}"""
            ),
            (
                "hhistory_c24_goldtimecapsule",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"hhistory_c24_megasoap","also":["clothing_cargopants","hhistory_c24_pupchi1","hhistory_c24_cactus"]},{"furni":"hhistory_c24_megapopsicle","also":["hhistory_c24_megasushi","hhistory_c24_indestructiblephone","clothing_frostedtipshair"]},{"furni":"hhistory_c24_megajazztape","also":["hhistory_c24_megahorse","clothing_butterflycliphair","hhistory_c24_fluffby1"]},{"furni":"hhistory_c24_cheep","also":["hhistory_c24_megaflower","clothing_couturetracksuit","hhistory_c24_fluffby2"]},{"furni":"clothing_cargopants","also":["hhistory_c24_megaring","hhistory_c24_pupchi2","hhistory_c24_fluffby3"]},{"furni":"hhistory_c24_megapencil","also":["hhistory_c24_lavalamp4","clothing_frostedtipshair","hhistory_c24_fluffby4"]},{"furni":"hhistory_c24_8bittrip","also":["hhistory_c24_lavalamp5","clothing_fluffybuckethat","hhistory_c24_fluffby5"]},{"furni":"hhistory_c24_megamooncake","also":["hhistory_c24_lavalamp6","clothing_digipetnecklace","hhistory_c24_fluffby6"]}]}"""
            ),
            (
                "hhistory_c24_silvertimecapsule",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"hhistory_c24_megapencil","also":["hhistory_c24_megajazztape","hhistory_c24_cactus","hhistory_c24_lavalamp1"],"weight":12375},{"furni":"hhistory_c24_megapopsicle","also":["hhistory_c24_cheep","hhistory_c24_8bittrip","hhistory_c24_lavalamp2"],"weight":12375},{"furni":"hhistory_c24_megamooncake","also":["hhistory_c24_megasoap","hhistory_c24_megahorse","hhistory_c24_lavalamp3"],"weight":12375},{"furni":"hhistory_c24_megasushi","also":["hhistory_c24_megaflower","hhistory_c24_megaring"],"weight":12375},{"furni":"hhistory_c24_megasoap","also":["hhistory_c24_megaring","hhistory_c24_cactus"],"weight":12375},{"furni":"hhistory_c24_megapopsicle","also":["hhistory_c24_megasushi","hhistory_c24_megapencil"],"weight":12375},{"furni":"hhistory_c24_megajazztape","also":["hhistory_c24_megahorse","hhistory_c24_8bittrip"],"weight":12375},{"furni":"hhistory_c24_megamooncake","also":["hhistory_c24_cheep","hhistory_c24_megaflower"],"weight":12375},{"furni":"hhistory_c24_goldtimecapsule","weight":1000}]}"""
            ),
            (
                "hobbies_c26_boardgamescrackable",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"hobbies_c26_d4","also":["hobbies_c26_chair","hobbies_c26_chair"],"weight":15},{"furni":"hobbies_c26_d6","also":["hobbies_c26_dicetray","hobbies_c26_journal"],"weight":10},{"furni":"hobbies_c26_d8","also":["hobbies_c26_dmscreen","hobbies_c26_dicetray"],"weight":10},{"furni":"hobbies_c26_d10","also":["hobbies_c26_chair","hobbies_c26_journal"],"weight":10},{"furni":"hobbies_c26_d12","also":["hobbies_c26_gamingtable"],"weight":10},{"furni":"hobbies_c26_d20","also":["hobbies_c26_owlbear"],"weight":5},{"furni":"hobbies_c26_d4","also":["hobbies_c26_d6","hobbies_c26_d8","hobbies_c26_d10","hobbies_c26_d12","hobbies_c26_d20","clothing_fantasydemon"],"weight":5},{"furni":"hobbies_c26_ornated4","also":["hobbies_c26_chair","hobbies_c26_chair"],"weight":5},{"furni":"hobbies_c26_ornated6","also":["hobbies_c26_dicetray","hobbies_c26_journal"],"weight":5},{"furni":"hobbies_c26_ornated8","also":["hobbies_c26_dmscreen","hobbies_c26_dicetray"],"weight":5},{"furni":"hobbies_c26_ornated10","also":["hobbies_c26_chair","hobbies_c26_journal"],"weight":5},{"furni":"hobbies_c26_ornated12","also":["hobbies_c26_gamingtable"],"weight":5},{"furni":"hobbies_c26_d4","also":["hobbies_c26_d6","hobbies_c26_d8","hobbies_c26_d10","hobbies_c26_d12","hobbies_c26_d20"],"weight":4},{"furni":"hobbies_c26_ornated20","also":["hobbies_c26_owlbear"],"weight":3},{"furni":"hobbies_c26_ornated4","also":["hobbies_c26_ornated6","hobbies_c26_ornated8","hobbies_c26_ornated10","hobbies_c26_ornated12","hobbies_c26_ornated20","clothing_crownedfantasydemon"],"weight":2},{"furni":"hobbies_c26_ornated4","also":["hobbies_c26_ornated6","hobbies_c26_ornated8","hobbies_c26_ornated10","hobbies_c26_ornated12","hobbies_c26_ornated20"]}]}"""
            ),
            (
                "hobbies_c26_gardeningcrackable",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"hobbies_c26_wateringcan","also":["hobbies_c26_rose","hobbies_c26_gloves"],"weight":15},{"furni":"hobbies_c26_tool3","also":["hobbies_c26_flower","hobbies_c26_flowerpot1"],"weight":15},{"furni":"hobbies_c26_tool2","also":["hobbies_c26_rose","hobbies_c26_flowerpot2"],"weight":15},{"furni":"hobbies_c26_tool1","also":["hobbies_c26_flower","hobbies_c26_flowerpot1"],"weight":15},{"furni":"hobbies_c26_shovel","also":["hobbies_c26_dirtpile","hobbies_c26_flowerpot2"],"weight":15},{"furni":"hobbies_c26_birdfeeder","also":["hobbies_c26_wheelbarrow","hobbies_c26_dirtpile"],"weight":10},{"furni":"hobbies_c26_lawnmover","also":["hobbies_c26_waterhose","hobbies_c26_gloves"],"weight":10},{"furni":"hobbies_c26_preciouswateringcan","weight":5}]}"""
            ),
            (
                "hobbies_c26_preciouswateringcan",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"clothing_flowereye"},{"furni":"clothing_flowerheart"}]}"""
            ),
            (
                "hobbies_c26_sewingcrackable",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"hobbies_c26_hotgluegun","also":["hobbies_c26_buttons","hobbies_c26_fabricscissors","hobbies_c26_tapemeasure"],"weight":25},{"furni":"hobbies_c26_teddypincushion","also":["hobbies_c26_buttons","hobbies_c26_threads","hobbies_c26_fabricscissors"],"weight":25},{"furni":"hobbies_c26_tapemeasure","also":["hobbies_c26_threads","hobbies_c26_pincushion","hobbies_c26_mannequin"],"weight":15},{"furni":"hobbies_c26_sewingmachine","also":["hobbies_c26_threads","hobbies_c26_teddypincushion","hobbies_c26_buttons"],"weight":15},{"furni":"hobbies_c26_sewingmachine","also":["hobbies_c26_loosetags","hobbies_c26_mannequin","hobbies_c26_hotgluegun"],"weight":15},{"furni":"hobbies_c26_teddypincushion","also":["hobbies_c26_loosetags","clothing_snakeyscarf"],"weight":3},{"furni":"hobbies_c26_loosetags","also":["clothing_snakeyscarf","clothing_diykittyplush"],"weight":2}]}"""
            ),
            (
                "hween_c22_archdarkangel",
                23,
                1,
                false,
                """{"target":22,"requiredEffectId":162,"rewards":[{"furni":"clothing_gothiccoat"},{"furni":"clothing_gothichat"},{"furni":"clothing_gothicflowerhorns"},{"furni":"clothing_gothicdress"}]}"""
            ),
            (
                "hween_c22_darkangel",
                23,
                1,
                false,
                """{"target":22,"requiredEffectId":5,"rewardPlacement":"Inventory","rewards":[{"furni":"hween_c22_darkcandles","also":["hween_c22_darkcandles","hween_c22_darkcandles"]},{"furni":"hween_c22_roseskull","also":["hween_c22_roseskull","hween_c22_roseskull"]},{"furni":"hween_c22_darkfeathers","also":["hween_c22_darkfeathers","hween_c22_darkfeathers"]},{"furni":"hween_c22_darkskull","also":["hween_c22_darkskull","hween_c22_darkskull"]},{"furni":"hween_c22_darkcandles","also":["hween_c22_darkcandles","hween_c22_roseskull"]},{"furni":"hween_c22_roseskull","also":["hween_c22_roseskull","hween_c22_darkfeathers"]},{"furni":"hween_c22_darkfeathers","also":["hween_c22_darkfeathers","hween_c22_darkskull"]},{"furni":"hween_c22_darkskull","also":["hween_c22_darkskull","hween_c22_darkcandles"]},{"furni":"hween_c22_darkcandles","also":["hween_c22_roseskull","hween_c22_darkfeathers"]},{"furni":"hween_c22_roseskull","also":["hween_c22_darkfeathers","hween_c22_darkskull"]},{"furni":"hween_c22_darkfeathers","also":["hween_c22_darkskull","hween_c22_darkcandles"]},{"furni":"hween_c22_darkskull","also":["hween_c22_darkcandles","hween_c22_roseskull"]}]}"""
            ),
            (
                "hween_c22_darkangelstatue",
                3,
                1,
                false,
                """{"target":1,"requiredEffectId":5,"rewards":[{"furni":"hween_c22_darkangel","weight":95},{"furni":"hween_c22_archdarkangel","weight":5}]}"""
            ),
            (
                "hween_c23_isischest",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"hween_c23_isisjar1"},{"furni":"hween_c23_isisjar2"},{"furni":"hween_c23_isisjar3"},{"furni":"hween_c23_isisjar4"}]}"""
            ),
            (
                "hween_c23_osirischest",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"hween_c23_osirisjar1"},{"furni":"hween_c23_osirisjar2"},{"furni":"hween_c23_osirisjar3"},{"furni":"hween_c23_osirisjar4"}]}"""
            ),
            (
                "hween_c23_rachest",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"hween_c23_rajar1"},{"furni":"hween_c23_rajar2"},{"furni":"hween_c23_rajar3"},{"furni":"hween_c23_rajar4"}]}"""
            ),
            (
                "hween_c23_seth",
                23,
                1,
                false,
                """{"target":22,"requiredEffectId":162,"rewards":[{"furni":"hween_c23_osirischest"},{"furni":"hween_c23_isischest"},{"furni":"hween_c23_rachest"}]}"""
            ),
            ("hween_c23_seth3", 23, 2, false, """{"target":22,"rewardTo":"Cracker"}"""),
            (
                "hween_c24_craftbox",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"hween_c24_blueprints","weight":40},{"furni":"hween_c24_paintbrushes","weight":40},{"furni":"hween_c24_pens","weight":40},{"furni":"hween_c24_blueprints","also":["hween_c24_blueprints","hween_c24_blueprints"],"weight":60},{"furni":"hween_c24_paintbrushes","also":["hween_c24_paintbrushes","hween_c24_paintbrushes"],"weight":60},{"furni":"hween_c24_pens","also":["hween_c24_pens","hween_c24_pens"],"weight":60}]}"""
            ),
            (
                "hween_c24_neonmixer",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"hween_c24_yellowpaint","weight":40},{"furni":"hween_c24_bluepaint","weight":40},{"furni":"hween_c24_pinkpaint","weight":40},{"furni":"hween_c24_pinkpaint","also":["hween_c24_pinkpaint","hween_c24_pinkpaint"],"weight":60},{"furni":"hween_c24_bluepaint","also":["hween_c24_bluepaint","hween_c24_bluepaint"],"weight":60},{"furni":"hween_c24_yellowpaint","also":["hween_c24_yellowpaint","hween_c24_yellowpaint"],"weight":60}]}"""
            ),
            (
                "hween_c25_infected1",
                23,
                1,
                false,
                """{"target":22,"rewardPlacement":"Inventory","rewards":[{"furni":"hween_c25_infected2","weight":25},{"furni":"hween_c25_scrapfabric","also":["hween_c25_scrapfabric","hween_c25_scrapfabric"],"weight":25},{"furni":"hween_c25_scrapwood","also":["hween_c25_scrapwood","hween_c25_scrapwood"],"weight":25},{"furni":"hween_c25_scrapmetal","also":["hween_c25_scrapmetal","hween_c25_scrapmetal"],"weight":25},{"furni":"hween_c25_scrapfabric","also":["hween_c25_scrapwood","hween_c25_scrapmetal"],"weight":25}]}"""
            ),
            (
                "hween_c25_infected2",
                23,
                1,
                false,
                """{"target":22,"rewardPlacement":"Inventory","rewards":[{"furni":"hween_c25_infected3","weight":25},{"furni":"hween_c25_scrapclothing","also":["hween_c25_scrapclothing","hween_c25_scrapclothing"],"weight":25},{"furni":"hween_c25_tools","also":["hween_c25_tools","hween_c25_tools"],"weight":25},{"furni":"hween_c25_rope","also":["hween_c25_rope","hween_c25_rope"],"weight":25},{"furni":"hween_c25_scrapclothing","also":["hween_c25_tools","hween_c25_rope"],"weight":25}]}"""
            ),
            (
                "hween_c25_infected3",
                23,
                1,
                false,
                """{"target":22,"rewardPlacement":"Inventory","rewards":[{"furni":"clothing_fungushead"},{"furni":"clothing_backkatana"},{"furni":"hween_c25_infectedplushie"}]}"""
            ),
            (
                "lt_r26_crackable",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"clothing_r26_spider","weight":32},{"furni":"clothing_r26_toucancompanion","weight":22},{"furni":"lt_r26_parasol","weight":18},{"furni":"lt_r26_fountain","weight":10},{"furni":"lt_r26_fan","weight":10},{"furni":"lt_r26_icecream","weight":8}]}"""
            ),
            (
                "mexico_c24_deluxepinata",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"clothing_floralguitar","also":["mexico_c24_minipumpkins","mexico_c24_festivedrinks","mexico_c24_meatballskewers","mexico_c24_paintedchair","mexico_c24_partytable"],"weight":26},{"furni":"clothing_skullguitar","also":["mexico_c24_tacoplate","mexico_c24_festiveflags","clothing_roseinmouth","mexico_c24_tabledecorations"],"weight":26},{"furni":"clothing_mustache","also":["clothing_acousticguitar","mexico_c24_pricklyplant","clothing_charmnecklace","clothing_fluffyearrings"],"weight":26},{"furni":"clothing_classicsombrero","also":["clothing_charmnecklace","mexico_c24_archway","mexico_c24_oaxacan"],"weight":12},{"furni":"clothing_vibrantguitar","also":["mexico_c24_pricklyplant","clothing_mariachioutfit","clothing_classicsombrero"],"weight":5},{"furni":"clothing_vibrantguitar","also":["clothing_traditionaldress","mexico_c24_archway","mexico_c24_oaxacan"],"weight":5}]}"""
            ),
            (
                "mexico_c24_pinata",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"mexico_c24_tacoplate","also":["mexico_c24_minipumpkins","mexico_c24_meatballskewers","mexico_c24_festiveflags","clothing_mustache"],"weight":26},{"furni":"mexico_c24_festivedrinks","also":["mexico_c24_tacoplate","mexico_c24_tabledecorations","mexico_c24_meatballskewers","clothing_fluffyearrings"],"weight":26},{"furni":"mexico_c24_tabledecorations","also":["mexico_c24_festivedrinks","mexico_c24_minipumpkins","mexico_c24_festiveflags","clothing_roseinmouth"],"weight":26},{"furni":"mexico_c24_partytable","also":["mexico_c24_paintedchair","mexico_c24_paintedchair","clothing_acousticguitar"],"weight":12},{"furni":"clothing_classicsombrero","also":["mexico_c24_partytable","mexico_c24_pricklyplant","clothing_floralguitar"],"weight":5},{"furni":"mexico_c24_paintedchair","also":["clothing_charmnecklace","clothing_skullguitar","mexico_c24_archway"],"weight":5}]}"""
            ),
            (
                "mini_c24_magnifyingglass1",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory"}"""
            ),
            (
                "mini_c24_magnifyingglass2",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory"}"""
            ),
            (
                "mushroom_c21_crackable",
                3,
                1,
                false,
                """{"target":1,"requiredEffectId":186,"rewards":[{"furni":"mushroom_c21_chair","weight":17},{"furni":"mushroom_c21_table","weight":15},{"furni":"mushroom_c21_bigmushroom","weight":13},{"furni":"mushroom_c21_bouncymushroom","weight":13},{"furni":"mushroom_c21_shelves","weight":11},{"furni":"mushroom_c21_drawers","weight":11},{"furni":"mushroom_c21_archway","weight":10},{"furni":"clothing_petalhat","weight":4},{"furni":"clothing_mushroom2","weight":4},{"furni":"clothing_flowerdress","weight":2}]}"""
            ),
            (
                "neopets_c25_crackableegg",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"neopets_c25_aisha1"},{"furni":"neopets_c25_aisha2"},{"furni":"neopets_c25_aisha3"},{"furni":"neopets_c25_kougra1"},{"furni":"neopets_c25_kougra2"},{"furni":"neopets_c25_kougra3"},{"furni":"neopets_c25_cybunny1"},{"furni":"neopets_c25_cybunny2"},{"furni":"neopets_c25_cybunny3"},{"furni":"neopets_c25_shoyru1"},{"furni":"neopets_c25_shoyru2"},{"furni":"neopets_c25_shoyru3"},{"furni":"neopets_c25_lupe1"},{"furni":"neopets_c25_lupe2"},{"furni":"neopets_c25_lupe3"},{"furni":"neopets_c25_eyrie1"},{"furni":"neopets_c25_eyrie2"},{"furni":"neopets_c25_eyrie3"}]}"""
            ),
            (
                "neopets_c25_crackablexmas",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"neopets_c25_aishaxmas"},{"furni":"neopets_c25_kougraxmas"},{"furni":"neopets_c25_cybunnyxmas"},{"furni":"neopets_c25_kacheekxmas"},{"furni":"neopets_c25_shoyruxmas"},{"furni":"neopets_c25_lupexmas"},{"furni":"neopets_c25_acaraxmas"},{"furni":"neopets_c25_draikxmas"},{"furni":"neopets_c25_eyriexmas"},{"furni":"neopets_c25_unixmas"},{"furni":"neopets_c25_xweetokxmas"},{"furni":"neopets_c25_brucexmas"}]}"""
            ),
            (
                "neopets_c26_crackableeaster",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"neopets_c26_kacheek1"},{"furni":"neopets_c26_kacheek2"},{"furni":"neopets_c26_kacheek3"},{"furni":"neopets_c26_acara1"},{"furni":"neopets_c26_acara2"},{"furni":"neopets_c26_acara3"},{"furni":"neopets_c26_uni1"},{"furni":"neopets_c26_uni2"},{"furni":"neopets_c26_uni3"},{"furni":"neopets_c26_draik1"},{"furni":"neopets_c26_draik2"},{"furni":"neopets_c26_draik3"},{"furni":"neopets_c26_xweetok1"},{"furni":"neopets_c26_xweetok2"},{"furni":"neopets_c26_xweetok3"},{"furni":"neopets_c26_bruce1"},{"furni":"neopets_c26_bruce2"},{"furni":"neopets_c26_bruce3"}]}"""
            ),
            (
                "neopets_c26_crackableval",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"neopets_c26_kacheekval"},{"furni":"neopets_c26_bruceval"},{"furni":"neopets_c26_unival"},{"furni":"neopets_c26_aishaval"},{"furni":"neopets_c26_cybunnyval"},{"furni":"neopets_c26_lupeval"},{"furni":"neopets_c26_draikval"},{"furni":"neopets_c26_kougraval"},{"furni":"neopets_c26_shoyruval"},{"furni":"neopets_c26_xweetokfae"},{"furni":"neopets_c26_acarafae"},{"furni":"neopets_c26_eyriefae"}]}"""
            ),
            (
                "ny_r21_crackable",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"clothing_r21_nystarhat1","weight":4},{"furni":"clothing_r21_nystarhat2","weight":12},{"furni":"clothing_r21_nystarhat3","weight":16},{"furni":"clothing_r21_nystarhat4","weight":16},{"furni":"clothing_r21_nystarhat5","weight":12},{"furni":"clothing_r21_nystarhat6","weight":12},{"furni":"clothing_r21_nystarhat7","weight":12},{"furni":"clothing_r21_nystarhat8","weight":16}]}"""
            ),
            (
                "ny_r22_crackable",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"clothing_r22_nypartyhorn1","weight":12},{"furni":"clothing_r22_nypartyhorn2","weight":16},{"furni":"clothing_r22_nypartyhorn3","weight":12},{"furni":"clothing_r22_nypartyhorn4","weight":12},{"furni":"clothing_r22_nypartyhorn5","weight":16},{"furni":"clothing_r22_nypartyhorn6","weight":16},{"furni":"clothing_r22_nypartyhorn7","weight":12},{"furni":"clothing_r22_nypartyhorn8","weight":4}]}"""
            ),
            (
                "ny_r23_crackable",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"clothing_r22_nytie1","weight":12},{"furni":"clothing_r22_nytie2","weight":16},{"furni":"clothing_r22_nytie3","weight":4},{"furni":"clothing_r22_nytie4","weight":16},{"furni":"clothing_r22_nytie5","weight":12},{"furni":"clothing_r22_nytie6","weight":16},{"furni":"clothing_r22_nytie7","weight":12},{"furni":"clothing_r22_nytie8","weight":12}]}"""
            ),
            (
                "ny_r24_crackable",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"clothing_r24_starboppers3","weight":4},{"furni":"clothing_r24_starboppers1","weight":12},{"furni":"clothing_r24_starboppers2","weight":12},{"furni":"clothing_r24_starboppers4","weight":12},{"furni":"clothing_r24_starboppers5","weight":16},{"furni":"clothing_r24_starboppers6","weight":12},{"furni":"clothing_r24_starboppers7","weight":16},{"furni":"clothing_r24_starboppers8","weight":16}]}"""
            ),
            (
                "ny_r25_crackable",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"clothing_r25_nystargarland1","weight":12},{"furni":"clothing_r25_nystargarland2","weight":12},{"furni":"clothing_r25_nystargarland3","weight":4},{"furni":"clothing_r25_nystargarland4","weight":12},{"furni":"clothing_r25_nystargarland5","weight":16},{"furni":"clothing_r25_nystargarland6","weight":12},{"furni":"clothing_r25_nystargarland7","weight":16},{"furni":"clothing_r25_nystargarland8","weight":16}]}"""
            ),
            (
                "nyc_c23_bag1",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"nyc_c23_fastfood","also":["nyc_c23_bagels","nyc_c23_breakfast"]},{"furni":"nyc_c23_fastfood","also":["nyc_c23_fastfood","nyc_c23_fastfood","nyc_c23_blueprints"]},{"furni":"nyc_c23_bagels","also":["nyc_c23_bagels","nyc_c23_bagels","nyc_c23_blueprints"]},{"furni":"nyc_c23_breakfast","also":["nyc_c23_breakfast","nyc_c23_breakfast","nyc_c23_blueprints"]},{"furni":"nyc_c23_fastfood","also":["nyc_c23_fastfood","nyc_c23_bagels"]},{"furni":"nyc_c23_bagels","also":["nyc_c23_bagels","nyc_c23_breakfast"]},{"furni":"nyc_c23_breakfast","also":["nyc_c23_breakfast","nyc_c23_fastfood"]},{"furni":"nyc_c23_fastfood","also":["nyc_c23_fastfood","nyc_c23_breakfast"]},{"furni":"nyc_c23_bagels","also":["nyc_c23_bagels","nyc_c23_fastfood"]},{"furni":"nyc_c23_breakfast","also":["nyc_c23_breakfast","nyc_c23_bagels"]}]}"""
            ),
            (
                "nyc_c23_bag2",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"nyc_c23_sandwich","also":["nyc_c23_donuts","nyc_c23_salad"]},{"furni":"nyc_c23_sandwich","also":["nyc_c23_sandwich","nyc_c23_sandwich"]},{"furni":"nyc_c23_donuts","also":["nyc_c23_donuts","nyc_c23_donuts"]},{"furni":"nyc_c23_salad","also":["nyc_c23_salad","nyc_c23_salad"]},{"furni":"nyc_c23_sandwich","also":["nyc_c23_sandwich","nyc_c23_donuts"]},{"furni":"nyc_c23_donuts","also":["nyc_c23_donuts","nyc_c23_salad"]},{"furni":"nyc_c23_salad","also":["nyc_c23_salad","nyc_c23_sandwich"]},{"furni":"nyc_c23_sandwich","also":["nyc_c23_sandwich","nyc_c23_salad"]},{"furni":"nyc_c23_donuts","also":["nyc_c23_donuts","nyc_c23_sandwich"]},{"furni":"nyc_c23_salad","also":["nyc_c23_salad","nyc_c23_donuts"]}]}"""
            ),
            (
                "olympus_c25_pandorasboxcrackable",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"olympus_c25_zeusalter"},{"furni":"olympus_c25_aphroditealter"},{"furni":"olympus_c25_dionysusalter"},{"furni":"olympus_c25_hadesalter"},{"furni":"clothing_olympiantoga"}]}"""
            ),
            (
                "pj_c26_pillowcrackable",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"pj_c26_nightlightcat","also":["pj_c26_nightlightbunny","pj_c26_boardgames","pj_c26_toothbrushes"],"weight":15},{"furni":"pj_c26_nightlightghost","also":["pj_c26_nightlightbunny","pj_c26_boardgames","pj_c26_frankplush"],"weight":15},{"furni":"pj_c26_nightlightghost","also":["pj_c26_toothbrushes","pj_c26_duckplush","pj_c26_frankplush"],"weight":15},{"furni":"pj_c26_nightlightcat","also":["pj_c26_duckplush","clothing_borrowedshirt3"],"weight":10},{"furni":"pj_c26_nightlightbunny","also":["pj_c26_toothbrushes","clothing_borrowedshirt2"],"weight":10},{"furni":"pj_c26_nightlightghost","also":["pj_c26_boardgames","clothing_borrowedshirt1"],"weight":10},{"furni":"pj_c26_nightlightduck","also":["pj_c26_sleepytimeduck","clothing_bedroll"],"weight":10},{"furni":"pj_c26_nightlightcat","also":["pj_c26_pillowdragon","clothing_sleepytimebun"],"weight":5},{"furni":"pj_c26_nightlightduck","also":["pj_c26_duckplush","clothing_sharkonesie"],"weight":5},{"furni":"pj_c26_sleepytimeduck","also":["pj_c26_frankplush","clothing_sleepytimeadonis"],"weight":5}]}"""
            ),
            (
                "rainbow_c21_crackable1",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"rainbow_c19_lights","weight":18},{"furni":"rainbow_c19_chair","weight":18},{"furni":"rainbow_c19_rug","weight":16},{"furni":"rainbow_c19_table","weight":16},{"furni":"rainbow_c19_flags","weight":15},{"furni":"rainbow_c19_bed","weight":12},{"furni":"rainbow_c21_crackable2","weight":5}]}"""
            ),
            (
                "rainbow_c21_crackable2",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"rainbow_c21_lightbulbheart","weight":225},{"furni":"rainbow_c21_balloonarch","weight":225},{"furni":"rainbow_c21_rainbowroad","weight":225},{"furni":"rainbow_c21_bunting","weight":225},{"furni":"clothing_pridecape","weight":25},{"furni":"clothing_pridehoodie","weight":25},{"furni":"clothing_rainbowwings","weight":25},{"furni":"clothing_rainbowundercut","weight":25}]}"""
            ),
            (
                "school_c22_crackable",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"school_c22_paintings","also":["school_c22_arttable"],"weight":32},{"furni":"school_c22_equipment","also":["school_c22_duffelbag"],"weight":32},{"furni":"school_c22_cello","also":["school_c22_xylophone"],"weight":32},{"furni":"clothing_schoolblazer","weight":4}]}"""
            ),
            (
                "skorea_c22_idolbox",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"skorea_c22_makeup1","also":["skorea_c22_photocard3","skorea_c22_vinyl2"],"weight":14},{"furni":"skorea_c22_makeup2","also":["skorea_c22_photocard2","skorea_c22_vinyl3"],"weight":14},{"furni":"skorea_c22_makeup3","also":["skorea_c22_photocard1","skorea_c22_vinyl1"],"weight":14},{"furni":"skorea_c22_makeup1","also":["skorea_c22_photocard2","skorea_c22_vinyl1"],"weight":14},{"furni":"skorea_c22_makeup2","also":["skorea_c22_photocard1","skorea_c22_vinyl2"],"weight":14},{"furni":"skorea_c22_makeup3","also":["skorea_c22_photocard3","skorea_c22_vinyl3"],"weight":14},{"furni":"skorea_c22_makeup1","also":["skorea_c22_photocard3","skorea_c22_vinyl2","clothing_idolhairlong"],"weight":2},{"furni":"skorea_c22_makeup2","also":["skorea_c22_photocard2","skorea_c22_vinyl3","clothing_glittertop"],"weight":3},{"furni":"skorea_c22_makeup3","also":["skorea_c22_photocard1","skorea_c22_vinyl1","clothing_cutepunkskirt"],"weight":3},{"furni":"skorea_c22_makeup1","also":["skorea_c22_photocard2","skorea_c22_vinyl1","clothing_idolhairlong"],"weight":2},{"furni":"skorea_c22_makeup2","also":["skorea_c22_photocard1","skorea_c22_vinyl2","clothing_glittertop"],"weight":3},{"furni":"skorea_c22_makeup3","also":["skorea_c22_photocard3","skorea_c22_vinyl3","clothing_cutepunkskirt"],"weight":3}]}"""
            ),
            (
                "skorea_c22_idolbox2",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"skorea_c22_lightstick1","also":["skorea_c22_photocard6","skorea_c22_vinyl5"],"weight":14},{"furni":"skorea_c22_lightstick3","also":["skorea_c22_photocard4","skorea_c22_vinyl6"],"weight":14},{"furni":"skorea_c22_lightstick2","also":["skorea_c22_photocard5","skorea_c22_vinyl4"],"weight":14},{"furni":"skorea_c22_lightstick1","also":["skorea_c22_photocard4","skorea_c22_vinyl4"],"weight":14},{"furni":"skorea_c22_lightstick3","also":["skorea_c22_photocard5","skorea_c22_vinyl5"],"weight":14},{"furni":"skorea_c22_lightstick2","also":["skorea_c22_photocard6","skorea_c22_vinyl6"],"weight":14},{"furni":"skorea_c22_lightstick1","also":["skorea_c22_photocard6","skorea_c22_vinyl5","clothing_2layeredshirt"],"weight":3},{"furni":"skorea_c22_lightstick3","also":["skorea_c22_photocard4","skorea_c22_vinyl6","clothing_idolhairshort"],"weight":2},{"furni":"skorea_c22_lightstick2","also":["skorea_c22_photocard5","skorea_c22_vinyl4","clothing_grungetrousers"],"weight":3},{"furni":"skorea_c22_lightstick1","also":["skorea_c22_photocard4","skorea_c22_vinyl4","clothing_idolhairshort"],"weight":2},{"furni":"skorea_c22_lightstick3","also":["skorea_c22_photocard5","skorea_c22_vinyl5","clothing_2layeredshirt"],"weight":3},{"furni":"skorea_c22_lightstick2","also":["skorea_c22_photocard6","skorea_c22_vinyl6","clothing_grungetrousers"],"weight":3}]}"""
            ),
            (
                "spa_c20_crackable1A",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"spa_c20_bbmint","also":["spa_c20_bbcharcoal","spa_c20_bbcharcoal","clothing_eyecitrus","spa_c20_lavendersalt","clothing_towelhair","clothing_towelwrapfull"]},{"furni":"spa_c20_bbcharcoal","also":["spa_c20_bbmint","spa_c20_bbmint","clothing_eyestrawberry","spa_c20_incense","clothing_eyemask","clothing_towelwrapfull"]}]}"""
            ),
            (
                "spa_c20_crackable2A",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"spa_c20_bbrose","also":["spa_c20_bbcitrus","spa_c20_bbcitrus","clothing_eyecucumber","spa_c20_lavendersalt","clothing_towelhair","clothing_towelwraphalf"]},{"furni":"spa_c20_bbcitrus","also":["spa_c20_bbrose","spa_c20_bbrose","clothing_eyetomato","spa_c20_incense","clothing_eyemask","clothing_towelwraphalf"]}]}"""
            ),
            (
                "stellar_c23_starcage",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"stellar_c23_aries","also":["stellar_c23_taurus","stellar_c23_gemini"]},{"furni":"stellar_c23_cancer","also":["stellar_c23_leo","stellar_c23_virgo"]},{"furni":"stellar_c23_libra","also":["stellar_c23_scorpio","stellar_c23_sagittarius"]},{"furni":"stellar_c23_capricorn","also":["stellar_c23_aquarius","stellar_c23_pisces"]},{"furni":"stellar_c23_aries","also":["stellar_c23_cancer","stellar_c23_libra"]},{"furni":"stellar_c23_taurus","also":["stellar_c23_scorpio","stellar_c23_aquarius"]},{"furni":"stellar_c23_virgo","also":["stellar_c23_sagittarius","stellar_c23_pisces"]}]}"""
            ),
            (
                "thai_c21_woodcarvingset",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"thai_c21_woodenendtable","weight":24},{"furni":"thai_c21_woodensofa","weight":24},{"furni":"thai_c21_woodenstatue","weight":24},{"furni":"thai_c21_woodenwardrobe","weight":24},{"furni":"thai_r21_clothingbox","weight":4}]}"""
            ),
            (
                "thai_r21_clothingbox",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"clothing_thaibunhair","weight":25},{"furni":"clothing_thaihat","weight":25},{"furni":"clothing_thaidress","weight":25},{"furni":"clothing_thaisuit","weight":25}]}"""
            ),
            ("val_c22_brokenvan", 3, 1, false, """{"target":1}"""),
            (
                "wonderland_c25_crackable",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"wonderland_c25_spadecard","also":["wonderland_c25_spadecard","wonderland_c25_drinkme"],"weight":330},{"furni":"wonderland_c25_clubcard","also":["wonderland_c25_clubcard","wonderland_c25_teacup"],"weight":330},{"furni":"wonderland_c25_spadecard","also":["wonderland_c25_spadecard","wonderland_c25_clubcard"],"weight":165},{"furni":"wonderland_c25_clubcard","also":["wonderland_c25_clubcard","wonderland_c25_spadecard"],"weight":165}]}"""
            ),
            (
                "wonderland_c25_redcrackable",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"wonderland_c25_heartcard","also":["wonderland_c25_heartcard","wonderland_c25_eatme"],"weight":330},{"furni":"wonderland_c25_diamondcard","also":["wonderland_c25_diamondcard","wonderland_c25_teapot"],"weight":330},{"furni":"wonderland_c25_heartcard","also":["wonderland_c25_heartcard","wonderland_c25_diamondcard"],"weight":165},{"furni":"wonderland_c25_diamondcard","also":["wonderland_c25_diamondcard","wonderland_c25_heartcard"],"weight":165}]}"""
            ),
            (
                "xmas_c21_crackableletter",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"xmas_c21_lockpick","also":["xmas_c21_key5","xmas_c21_map1"]},{"furni":"xmas_c21_lockpick","also":["xmas_c21_key5","xmas_c21_map2"]},{"furni":"xmas_c21_lockpick","also":["xmas_c21_key4","xmas_c21_map3"]},{"furni":"xmas_c21_lockpick","also":["xmas_c21_key4","xmas_c21_map4"]},{"furni":"xmas_c21_lockpick","also":["xmas_c21_key2","xmas_c21_map5"]},{"furni":"xmas_c21_lockpick","also":["xmas_c21_key2","xmas_c21_map6"]}]}"""
            ),
            (
                "xmas_c22_present1",
                21,
                2,
                false,
                """{"target":20,"rewardTo":"Cracker","rewards":[{"furni":"xmas_c22_ornament4"},{"furni":"xmas_c22_ornament3"},{"furni":"xmas_c22_ornament1"},{"furni":"xmas_c22_ornament2"}]}"""
            ),
            (
                "xmas_c22_present2",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"xmas_c22_ornament3","also":["xmas_c22_ornament4","xmas_c22_candycanes","xmas_c22_wrappingpaper"]},{"furni":"xmas_c22_ornament4","also":["xmas_c22_ornament4","xmas_c22_ornament4","xmas_c22_candycanes"]},{"furni":"xmas_c22_ornament3","also":["xmas_c22_ornament3","xmas_c22_ornament3","xmas_c22_wrappingpaper"]},{"furni":"xmas_c22_ornament3","also":["xmas_c22_ornament3","xmas_c22_ornament4","xmas_c22_ornament4"]},{"furni":"xmas_c22_wrappingpaper","also":["xmas_c22_wrappingpaper","xmas_c22_wrappingpaper","xmas_c22_candycanes"]},{"furni":"xmas_c22_candycanes","also":["xmas_c22_candycanes","xmas_c22_candycanes","xmas_c22_wrappingpaper"]},{"furni":"xmas_c22_ornament4","also":["xmas_c22_ornament4","xmas_c22_ornament4","xmas_c22_wrappingpaper"]},{"furni":"xmas_c22_ornament3","also":["xmas_c22_ornament3","xmas_c22_ornament3","xmas_c22_candycanes"]}]}"""
            ),
            (
                "xmas_c23_crackable1",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"xmas_c23_eggs","also":["xmas_c23_milkpowder","xmas_c23_cakemix"],"weight":165},{"furni":"xmas_c23_sugar","also":["xmas_c23_cocao","xmas_c23_eggs"],"weight":165},{"furni":"xmas_c23_cocao","also":["xmas_c23_eggs","xmas_c23_milkpowder"],"weight":165},{"furni":"xmas_c23_milkpowder","also":["xmas_c23_cakemix","xmas_c23_water"],"weight":165},{"furni":"xmas_c23_cakemix","also":["xmas_c23_water","xmas_c23_sugar"],"weight":165},{"furni":"xmas_c23_sugar","also":["xmas_c23_cocao","xmas_c23_water"],"weight":165},{"furni":"xmas_c23_goldenticket","weight":10}]}"""
            ),
            (
                "xmas_c23_crackable2",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"xmas_c23_chocolatebar","also":["xmas_c23_berryfruits","xmas_c23_mint"],"weight":158},{"furni":"xmas_c23_sprinkles","also":["xmas_c23_sprinkles","xmas_c23_chocolatebar"],"weight":158},{"furni":"xmas_c23_chocolatebar","also":["xmas_c23_chocolatebar","xmas_c23_berryfruits"],"weight":158},{"furni":"xmas_c23_berryfruits","also":["xmas_c23_berryfruits","xmas_c23_mint"],"weight":158},{"furni":"xmas_c23_mint","also":["xmas_c23_mint","xmas_c23_sprinkles"],"weight":158},{"furni":"xmas_c23_sprinkles","also":["xmas_c23_berryfruits","xmas_c23_mint"],"weight":158},{"furni":"xmas_c23_goldenticket","weight":50}]}"""
            ),
            (
                "xmas_c24_crackable1",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"xmas_c24_hungryducks"},{"furni":"xmas_c24_ducktrapgame"},{"furni":"xmas_c24_kickeroo"},{"furni":"xmas_c24_crackable2"}]}"""
            ),
            (
                "xmas_c24_crackable2",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"xmas_c24_patchbaby"},{"furni":"xmas_c24_retroconsole"},{"furni":"xmas_c24_drawpad"},{"furni":"xmas_c24_crackable3"}]}"""
            ),
            (
                "xmas_c24_crackable3",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"xmas_c24_pogoball"},{"furni":"xmas_c24_mrfrankhead"},{"furni":"xmas_c24_bonniepocket"},{"furni":"xmas_c24_crackable4"}]}"""
            ),
            (
                "xmas_c24_crackable4",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"clothing_hollydress"},{"furni":"clothing_dressinggown"}]}"""
            ),
            (
                "xmas_c25_treedecorcrackable",
                3,
                1,
                false,
                """{"target":1,"rewardPlacement":"Inventory","rewards":[{"furni":"xmas_c25_treebaubles","also":["xmas_c25_treelights","xmas_c25_treelights"]},{"furni":"xmas_c25_treetinsel","also":["xmas_c25_treebaubles","xmas_c25_treebaubles"]},{"furni":"xmas_c25_treesnowflake","also":["xmas_c25_treetinsel","xmas_c25_treetinsel"]},{"furni":"xmas_c25_treecandycane","also":["xmas_c25_treesnowflake","xmas_c25_treesnowflake"]},{"furni":"xmas_c25_treelights","also":["xmas_c25_treecandycane","xmas_c25_treecandycane"]}]}"""
            ),
            (
                "xmas_r25_habububox",
                3,
                1,
                false,
                """{"target":1,"rewards":[{"furni":"xmas_r25_habubulove","weight":16},{"furni":"xmas_r25_habubufun","weight":14},{"furni":"xmas_r25_habubucosy","weight":13},{"furni":"xmas_r25_habubuhappiness","weight":12},{"furni":"xmas_r25_habubucalm","weight":11},{"furni":"xmas_r25_habubuhope","weight":10},{"furni":"xmas_r25_habubudream","weight":10},{"furni":"xmas_r25_habubuluck","weight":9},{"furni":"xmas_r25_habubuglam","also":["clothing_r25_habubuglam"],"weight":5}]}"""
            ),
        ];

        /// <summary>Name, the usage policy (2 everyone), the <c>vending</c> section.</summary>
        private static readonly (string Name, int Usage, string Vending)[] VENDING =
        [
            ("ads_711", 2, """{"handItems":[33]}"""),
            ("ads_calip_cola", 2, """{"handItems":[26]}"""),
            ("ads_cheetos", 2, """{"handItems":[52]}"""),
            ("ads_cheetos_hotdog", 2, """{"handItems":[56]}"""),
            ("ads_chocapic", 2, """{"handItems":[54]}"""),
            ("ads_chups", 2, """{"handItems":[48]}"""),
            ("ads_cl_moodi", 2, """{"handItems":[45,46,47]}"""),
            ("ads_clearasil_vend", 2, """{"handItems":[1046],"animates":false}"""),
            ("ads_clearasil_vend2", 2, """{"handItems":[1046],"animates":false}"""),
            ("ads_dfrisss", 2, """{"handItems":[72]}"""),
            ("ads_droetker_paula", 2, """{"handItems":[49]}"""),
            ("ads_dtlrare", 2, """{"handItems":[1048],"animates":false}"""),
            ("ads_dtlrare_2", 2, """{"handItems":[1052],"animates":false}"""),
            ("ads_dtlrare_rc", 2, """{"handItems":[1052],"animates":false}"""),
            ("ads_grefusa_yum", 2, """{"handItems":[51]}"""),
            ("ads_hh_safe", 2, """{"handItems":[59]}"""),
            ("ads_honeymonster", 2, """{"handItems":[1028],"animates":false}"""),
            ("ads_liisu", 2, """{"handItems":[1]}"""),
            ("ads_mall_coffeem", 2, """{"handItems":[41]}"""),
            ("ads_oc_soda", 2, """{"handItems":[42]}"""),
            ("ads_oc_soda_cherry", 2, """{"handItems":[57]}"""),
            ("ads_pepsi0", 2, """{"handItems":[55]}"""),
            ("ads_sunnyvend", 2, """{"handItems":[61]}"""),
            ("ads_suun", 2, """{"handItems":[1]}"""),
            ("antibully_machine", 2, """{"handItems":[1095],"animates":false}"""),
            ("arabian_teamk", 2, """{"handItems":[27]}"""),
            ("army_c15_compass", 2, """{"handItems":[1072],"animates":false}"""),
            ("attic15_chest", 2, """{"handItems":[1065,1066,1067],"animates":false}"""),
            ("bar_armas", 2, """{"handItems":[1,2]}"""),
            ("bar_basic", 2, """{"handItems":[1,2]}"""),
            ("bar_polyfon", 2, """{"handItems":[1,2]}"""),
            ("bathroom_sinkmodule1", 2, """{"handItems":[18]}"""),
            ("bathroom_sinkmodule2", 2, """{"handItems":[18]}"""),
            ("bathroom_sinkmodule3", 2, """{"handItems":[18]}"""),
            ("bathroom_sinkmodule4", 2, """{"handItems":[18]}"""),
            ("bathroom_toiletroll", 2, """{"handItems":[1059],"animates":false}"""),
            ("bazaar_c17_bubblejuiceblower", 2, """{"handItems":[19],"animates":false}"""),
            ("bazaar_c17_chillies", 2, """{"handItems":[112],"animates":false}"""),
            ("bazaar_c17_sticklizard", 2, """{"handItems":[1078],"animates":false}"""),
            ("bazaar_r17_fountain", 2, """{"handItems":[18]}"""),
            ("bling11_slot", 2, """{"handItems":[32,35,40],"animates":false}"""),
            ("bling_fridge", 2, """{"handItems":[35]}"""),
            ("bling_fridge_restricted", 2, """{"handItems":[50]}"""),
            ("bolly_phant", 2, """{"handItems":[35]}"""),
            ("bolly_vase", 2, """{"handItems":[1019],"animates":false}"""),
            ("bonusrare18_1", 2, """{"handItems":[113,114,115],"animates":false}"""),
            ("bonusrare20_3", 2, """{"handItems":[53],"animates":false}"""),
            ("calippo", 2, """{"handItems":[26]}"""),
            ("china_c21_orangebasket", 2, """{"handItems":[38],"animates":false}"""),
            ("cine_popcorn", 2, """{"handItems":[63]}"""),
            ("cland15_flosstree", 2, """{"handItems":[92,93,94,95,79],"animates":false}"""),
            ("cland15_unipoo", 2, """{"handItems":[4,75,76,77],"animates":false}"""),
            ("cland_c15_swirltree", 2, """{"handItems":[92,93,94,95],"animates":false}"""),
            ("classic2_burger", 2, """{"handItems":[109],"animates":false}"""),
            ("classic2_drinkmach", 2, """{"handItems":[18,1,29,30,42,66],"animates":false}"""),
            ("classic2_drinkmach2", 2, """{"handItems":[18,1,29,30,42,66],"animates":false}"""),
            ("classic2_grill", 2, """{"handItems":[109],"animates":false}"""),
            ("classic3_ticket", 2, """{"handItems":[1070],"animates":false}"""),
            ("classic7_drinkcabinet", 2, """{"handItems":[42,43,66],"animates":false}"""),
            ("classic7_drinkdispenser", 1, """{"handItems":[29,30],"animates":false}"""),
            ("classic7_fridge", 2, """{"handItems":[42,43,66],"animates":false}"""),
            ("country_well", 2, """{"handItems":[7]}"""),
            (
                "cpunk_c15_bar",
                2,
                """{"handItems":[35,53,62,1041,87,90,24,29,30],"animates":false}"""
            ),
            ("diner_gumvendor", 2, """{"handItems":[67,67,67,69,69,69,68],"animates":false}"""),
            ("dino_c15_nest", 2, """{"handItems":[1073,1074,1075,1076],"animates":false}"""),
            ("dtl_r20_gold", 2, """{"handItems":[1093],"animates":false}"""),
            ("easel_5", 2, """{"handItems":[1051],"animates":false}"""),
            ("easter14_palmtree", 2, """{"handItems":[1050],"animates":false}"""),
            ("easter_c17_appletree", 2, """{"handItems":[83],"animates":false}"""),
            ("easter_c17_peachtree", 2, """{"handItems":[37],"animates":false}"""),
            ("easter_c17_peartree", 2, """{"handItems":[36],"animates":false}"""),
            ("easter_c18_lemontree", 2, """{"handItems":[116],"animates":false}"""),
            ("easter_c18_veg", 2, """{"handItems":[98],"animates":false}"""),
            ("easter_c20_fishingpole", 2, """{"handItems":[1090,1091,1092],"animates":false}"""),
            ("eco_fruits1", 2, """{"handItems":[36,37,38,39]}"""),
            ("eco_fruits2", 2, """{"handItems":[36,37,38,39]}"""),
            ("eco_fruits3", 2, """{"handItems":[36,37,38,39]}"""),
            ("eco_tree1", 2, """{"handItems":[38]}"""),
            ("eco_tree2", 2, """{"handItems":[36]}"""),
            ("es_roaster", 2, """{"handItems":[60]}"""),
            ("exe_drinks_cabinet", 2, """{"handItems":[40]}"""),
            ("exe_icecream", 2, """{"handItems":[4]}"""),
            ("fest_c19_bobbatea", 2, """{"handItems":[124,125,126],"animates":false}"""),
            ("fest_c19_coalicecream", 2, """{"handItems":[127,128],"animates":false}"""),
            ("fridge", 2, """{"handItems":[3,4,5,6]}"""),
            ("garden_flo1", 2, """{"handItems":[1007]}"""),
            ("garden_flo2", 2, """{"handItems":[1008]}"""),
            ("garden_flo3", 2, """{"handItems":[1009]}"""),
            ("gift_c18_cookiejar", 2, """{"handItems":[117],"animates":false}"""),
            ("giftflowers", 2, """{"handItems":[1006],"animates":false}"""),
            ("gold_c15_arc_hole", 2, """{"handItems":[34],"animates":false}"""),
            ("gothic_bowl", 2, """{"handItems":[62]}"""),
            ("hal_cauldron", 2, """{"handItems":[3]}"""),
            ("hblooza14_cafe_b", 2, """{"handItems":[85,86],"animates":false}"""),
            ("hblooza14_cafe_p", 2, """{"handItems":[85,86],"animates":false}"""),
            ("hblooza14_cafe_y", 2, """{"handItems":[85,86],"animates":false}"""),
            ("hblooza14_candystall", 2, """{"handItems":[84,63,79,80,48],"animates":false}"""),
            (
                "hblooza14_drinkstall",
                2,
                """{"handItems":[6,24,30,32,57,66,67,72],"animates":false}"""
            ),
            ("hblooza14_duckhook", 2, """{"handItems":[1053],"animates":false}"""),
            ("hblooza14_duckhookhc", 2, """{"handItems":[1036],"animates":false}"""),
            ("hblooza_bubblejuice", 2, """{"handItems":[19],"animates":false}"""),
            ("hblooza_candyfloss", 2, """{"handItems":[79,80],"animates":false}"""),
            ("hblooza_chicken", 2, """{"handItems":[70],"animates":false}"""),
            ("hblooza_hotdog", 2, """{"handItems":[81],"animates":false}"""),
            ("hblooza_icecream", 2, """{"handItems":[4,75,76,77],"animates":false}"""),
            ("hblooza_kiosk", 2, """{"handItems":[1,3,19,31,36,38,63,81,1036],"animates":false}"""),
            ("hblooza_popcorn", 2, """{"handItems":[63],"animates":false}"""),
            ("hc16_3", 2, """{"handItems":[50],"animates":false}"""),
            ("hc17_11", 2, """{"handItems":[35]}"""),
            ("hc21_1", 2, """{"handItems":[1096],"animates":false}"""),
            ("hc21_11", 2, """{"handItems":[3,36,83,100,103,107],"animates":false}"""),
            ("hc2_coffee", 2, """{"handItems":[53]}"""),
            ("hc_arab_teamk", 2, """{"handItems":[27]}"""),
            ("hc_btlr", 2, """{"handItems":[24]}"""),
            ("hcc_minibar", 2, """{"handItems":[3]}"""),
            ("hosp_c19_drinksvend", 2, """{"handItems":[29,30,42,43],"animates":false}"""),
            ("hosptl_bed", 2, """{"handItems":[1011],"animates":false}"""),
            ("hosptl_cab1", 2, """{"handItems":[1011,1013,1014,1015],"animates":false}"""),
            ("hosptl_cab2", 2, """{"handItems":[1011,1013,1014,1015],"animates":false}"""),
            ("hween08_sink", 2, """{"handItems":[29]}"""),
            ("hween08_sink2", 2, """{"handItems":[30]}"""),
            ("hween10_zombie", 2, """{"handItems":[58],"animates":false}"""),
            ("hween11_punch", 2, """{"handItems":[34,58,29]}"""),
            (
                "hween12_grabby",
                2,
                """{"handItems":[1035,1037,1038,1039,1040,1041,1042,1044,1045],"animates":false}"""
            ),
            ("hween13_tree", 2, """{"handItems":[83],"animates":false}"""),
            ("hween14_altarpieces3", 2, """{"handItems":[1061],"animates":false}"""),
            ("hween_c18_labshelf", 2, """{"handItems":[1043,1044],"animates":false}"""),
            ("hween_c18_medicineshelf", 2, """{"handItems":[44,19,1014],"animates":false}"""),
            (
                "hween_c18_spareparts",
                2,
                """{"handItems":[58,1015,1035,1039,1040,1045],"animates":false}"""
            ),
            ("hween_c19_pumpkinpatch", 2, """{"handItems":[1083],"animates":false}"""),
            ("hween_r18_antiquechemset", 2, """{"handItems":[44]}"""),
            ("hyacinth1", 2, """{"handItems":[1021],"animates":false}"""),
            ("hyacinth2", 2, """{"handItems":[1022],"animates":false}"""),
            ("joulutahti", 2, """{"handItems":[1023],"animates":false}"""),
            ("joulutahti_notrd", 2, """{"handItems":[1023],"animates":false}"""),
            ("jp_teamaker", 2, """{"handItems":[28]}"""),
            ("js_bling_fridge", 2, """{"handItems":[35]}"""),
            ("js_c16_drkcab", 2, """{"handItems":[43],"animates":false}"""),
            ("jungle_c16_bush", 2, """{"handItems":[1006,1037,1038],"animates":false}"""),
            ("ktchn15_bubblejuicerack", 2, """{"handItems":[24,29,50,74,101],"animates":false}"""),
            ("ktchn15_coffeemaker", 2, """{"handItems":[41,53],"animates":false}"""),
            ("ktchn15_fridge", 2, """{"handItems":[3,36,37,38,39]}"""),
            ("ktchn_fridge", 2, """{"handItems":[3,36,37,38,39]}"""),
            ("ktchn_inspctr", 2, """{"handItems":[34]}"""),
            ("limo_b_mid3", 2, """{"handItems":[40]}"""),
            ("lm_bananadrink", 2, """{"handItems":[66]}"""),
            ("mall_c17_kiosk", 2, """{"handItems":[37,36,83,29,30,34,42,38],"animates":false}"""),
            ("mall_r17_coffeem", 2, """{"handItems":[41],"animates":false}"""),
            (
                "market_c19_dairyfridge",
                2,
                """{"handItems":[107,129,130,42,43,66,113,114,115],"animates":false}"""
            ),
            ("market_c19_deli1", 2, """{"handItems":[34,81,111,132,136],"animates":false}"""),
            ("market_c19_deli2", 2, """{"handItems":[39,71,84,89,130,131,135],"animates":false}"""),
            ("market_c19_drygoods", 2, """{"handItems":[63,1028,117,138,1089],"animates":false}"""),
            ("market_c19_dvds", 2, """{"handItems":[1085,1086],"animates":false}"""),
            (
                "market_c19_icecreamfreezer",
                2,
                """{"handItems":[137,4,75,76,77,127,128],"animates":false}"""
            ),
            (
                "market_c19_icecreamtubs",
                2,
                """{"handItems":[4,75,76,77,127,128,137],"animates":false}"""
            ),
            ("market_c19_meat", 2, """{"handItems":[136,70,81,109,122],"animates":false}"""),
            (
                "market_c19_stationary",
                2,
                """{"handItems":[1087,1088,1003,1068,1069,1070,1004,1005],"animates":false}"""
            ),
            (
                "market_c19_vegfruit",
                2,
                """{"handItems":[3,36,37,38,83,98,99,100,112,116,133,134,1084],"animates":false}"""
            ),
            (
                "matic_dispenser",
                2,
                """{"handItems":[1032,1033,1034,1035,1036,1037,1038,1032,1033,1034,1035,1036,1037,1038,1,3,28,29,34,36,37,38,39,58,70,71,1013,1014,1015,1019,1029,1051,1031],"animates":false}"""
            ),
            ("matic_slime_duck", 2, """{"handItems":[1036],"animates":false}"""),
            ("matic_water_dispenser", 2, """{"handItems":[18],"animates":false}"""),
            ("md_limukaappi", 2, """{"handItems":[19]}"""),
            ("mm_lemon_drink", 2, """{"handItems":[64]}"""),
            ("mocchamaster", 2, """{"handItems":[8,9,10,11,12,13,14,15,16,17]}"""),
            ("ny2015_bar", 2, """{"handItems":[35],"animates":false}"""),
            ("ny2015_cctray", 2, """{"handItems":[89],"animates":false}"""),
            ("ny2015_drktray", 2, """{"handItems":[90],"animates":false}"""),
            ("olympics_c16_cadorack", 2, """{"handItems":[104],"animates":false}"""),
            ("olympics_c16_graperack", 2, """{"handItems":[105],"animates":false}"""),
            ("olympics_c16_merch", 2, """{"handItems":[102],"animates":false}"""),
            ("olympics_c16_nanarack", 1, """{"handItems":[103],"animates":false}"""),
            ("olympics_c16_weightrack", 2, """{"handItems":[108],"animates":false}"""),
            ("olympics_r16_smoothie", 2, """{"handItems":[106],"animates":false}"""),
            ("olympics_r16_vendingmchn", 2, """{"handItems":[107],"animates":false}"""),
            ("paris15_cake", 2, """{"handItems":[96],"animates":false}"""),
            ("paris_c15_breadstall", 2, """{"handItems":[97],"animates":false}"""),
            (
                "paris_c15_flowerstl",
                2,
                """{"handItems":[1000,1001,1002,1006,1007,1008,1009,1019,1021,1022],"animates":false}"""
            ),
            ("paris_c15_vegstall1", 2, """{"handItems":[100],"animates":false}"""),
            ("paris_c15_vegstall2", 2, """{"handItems":[99],"animates":false}"""),
            ("paris_c15_vegstall3", 2, """{"handItems":[98],"animates":false}"""),
            ("party_tray", 2, """{"handItems":[31]}"""),
            ("pcnc_carrot", 2, """{"handItems":[3]}"""),
            ("pirate_barrel2", 2, """{"handItems":[38],"animates":false}"""),
            ("pirate_barrel3", 2, """{"handItems":[34],"animates":false}"""),
            ("pirate_cannonballs", 2, """{"handItems":[1047],"animates":false}"""),
            ("pirate_navdesk", 2, """{"handItems":[82],"animates":false}"""),
            ("plant_rose", 2, """{"handItems":[1000],"animates":false}"""),
            ("plant_rose_black", 2, """{"handItems":[1001],"animates":false}"""),
            ("plant_sunflower", 2, """{"handItems":[1002],"animates":false}"""),
            ("pudding", 2, """{"handItems":[1024],"animates":false}"""),
            ("purablk_c16_bar", 2, """{"handItems":[1,2],"animates":false}"""),
            ("purablk_c16_fridge", 2, """{"handItems":[3,4,5,6],"animates":false}"""),
            ("qt_sum11_ictrolley", 2, """{"handItems":[4]}"""),
            ("qt_xm10_elephant", 2, """{"handItems":[35]}"""),
            ("rainbow_c19_flags", 2, """{"handItems":[1082],"animates":false}"""),
            ("rare_blackrosegold_icecream", 2, """{"handItems":[4],"animates":false}"""),
            ("rare_colourable_icecream", 2, """{"handItems":[4]}"""),
            ("rare_icecream", 2, """{"handItems":[4]}"""),
            ("rare_icecream_campaign", 2, """{"handItems":[4]}"""),
            ("rare_r21_coffeesiphon", 2, """{"handItems":[146],"animates":false}"""),
            ("room_cof15_counter1", 2, """{"handItems":[89,1024,1053],"animates":false}"""),
            (
                "room_cof15_cup",
                2,
                """{"handItems":[8,9,10,11,12,13,14,15,16,17,53,54],"animates":false}"""
            ),
            ("room_cof15_cup2", 2, """{"handItems":[85,86],"animates":false}"""),
            ("room_cof15_shelf", 2, """{"handItems":[1003,1004,1005],"animates":false}"""),
            ("room_info15_fridge", 2, """{"handItems":[42],"animates":false}"""),
            ("room_pcnc15_carrot", 2, """{"handItems":[3],"animates":false}"""),
            ("room_pcnc15_hotdog", 2, """{"handItems":[81],"animates":false}"""),
            ("room_pcnc15_soda", 2, """{"handItems":[42,43],"animates":false}"""),
            ("room_thr15_bubjuice", 2, """{"handItems":[19],"animates":false}"""),
            ("room_thr15_candy", 2, """{"handItems":[79,80,84,48,67,68,69],"animates":false}"""),
            ("room_thr15_hotdog", 2, """{"handItems":[81],"animates":false}"""),
            ("room_thr15_icecream", 2, """{"handItems":[4,75,76,77],"animates":false}"""),
            ("room_thr15_popcorn", 2, """{"handItems":[63],"animates":false}"""),
            ("room_wl15_mag1", 2, """{"handItems":[1068,1069,1070],"animates":false}"""),
            ("room_wlof15_bookcase", 2, """{"handItems":[1003,1004,1005],"animates":false}"""),
            ("safe_silo", 2, """{"handItems":[17]}"""),
            ("safe_silo_pb", 2, """{"handItems":[3]}"""),
            ("samovar", 2, """{"handItems":[1]}"""),
            ("santorini_c17_rockpool", 2, """{"handItems":[111],"animates":false}"""),
            ("sb_cans", 1, """{"handItems":[1060],"animates":false}"""),
            ("school_cafe", 2, """{"handItems":[71,36,37,3],"animates":false}"""),
            ("sf_mbar", 2, """{"handItems":[44]}"""),
            ("shelves_silo", 2, """{"handItems":[1003,1004,1005]}"""),
            ("sink", 2, """{"handItems":[18]}"""),
            ("st_hween14_skulls", 2, """{"handItems":[1062,1063,1064],"animates":false}"""),
            ("st_palooza_balloons", 2, """{"handItems":[1054,1055,1056,1057],"animates":false}"""),
            ("summer_c17_burgertruck", 2, """{"handItems":[109],"animates":false}"""),
            ("summer_icebox", 2, """{"handItems":[43]}"""),
            ("suncity_c19_wateroutlet", 2, """{"handItems":[1081],"animates":false}"""),
            ("sunsetcafe_c20_coffeemachine", 2, """{"handItems":[53],"animates":false}"""),
            ("tablet_vendor", 2, """{"handItems":[1030],"animates":false}"""),
            ("tablet_vendorg", 2, """{"handItems":[1071],"animates":false}"""),
            ("toaster", 2, """{"handItems":[71],"animates":false}"""),
            ("tokyo_c18_bugsmachine", 2, """{"handItems":[1079,1080,1043],"animates":false}"""),
            ("tokyo_c18_drinksmachine", 2, """{"handItems":[118,119,42,43,66],"animates":false}"""),
            ("tokyo_c18_snackdisplay", 2, """{"handItems":[122],"animates":false}"""),
            ("tokyo_c18_snackdisplay2", 2, """{"handItems":[120,121],"animates":false}"""),
            ("track12_mini_torch", 2, """{"handItems":[1031],"animates":false}"""),
            ("track12_tea_mother", 2, """{"handItems":[10]}"""),
            ("tray_glasstower", 2, """{"handItems":[40]}"""),
            ("turkey", 2, """{"handItems":[70],"animates":false}"""),
            ("uni_fridge", 2, """{"handItems":[18,3,29,30,34,36,37],"animates":false}"""),
            ("val13_easel_1", 2, """{"handItems":[1051],"animates":false}"""),
            ("val13_easel_2", 2, """{"handItems":[1051],"animates":false}"""),
            ("val13_easel_3", 2, """{"handItems":[1051],"animates":false}"""),
            ("val13_easel_4", 2, """{"handItems":[1051],"animates":false}"""),
            ("val13_easel_5", 2, """{"handItems":[1051],"animates":false}"""),
            ("val15_tea", 2, """{"handItems":[91],"animates":false}"""),
            ("val_c20_chocfountain", 2, """{"handItems":[143,144],"animates":false}"""),
            ("val_cauldron", 2, """{"handItems":[25]}"""),
            ("vikings_basket1", 2, """{"handItems":[3,36,37],"animates":false}"""),
            ("vikings_basket2", 2, """{"handItems":[34,70,1035,1040],"animates":false}"""),
            ("wildwest_cabinet", 2, """{"handItems":[88],"animates":false}"""),
            ("wildwest_light", 2, """{"handItems":[1058],"animates":false}"""),
            ("wildwest_pump", 2, """{"handItems":[87],"animates":false}"""),
            ("xm09_cocoa", 2, """{"handItems":[15]}"""),
            ("xmas08_hole", 2, """{"handItems":[34]}"""),
            ("xmas11_balloon", 2, """{"handItems":[1029],"animates":false}"""),
            ("xmas11_btlr", 2, """{"handItems":[15]}"""),
            ("xmas12_barrel", 2, """{"handItems":[73],"animates":false}"""),
            ("xmas12_nutcracker", 2, """{"handItems":[60],"animates":false}"""),
            ("xmas13_candycane1", 2, """{"handItems":[48,1025,84],"animates":false}"""),
            ("xmas13_eggnogbowl", 2, """{"handItems":[73],"animates":false}"""),
            ("xmas13_gingerbread", 2, """{"handItems":[84],"animates":false}"""),
            ("xmas13_icecream", 2, """{"handItems":[4]}"""),
            ("xmas13_snowflake7", 2, """{"handItems":[18]}"""),
            ("xmas13_toolbox", 2, """{"handItems":[1049],"animates":false}"""),
            ("xmas14_tikibar", 2, """{"handItems":[31],"animates":false}"""),
            ("xmas14_tikibar2", 2, """{"handItems":[31],"animates":false}"""),
            ("xmas15_nutcrackerltd", 2, """{"handItems":[60],"animates":false}"""),
            ("xmas_c15_store1", 2, """{"handItems":[1065,1066,1067,1026,1053],"animates":false}"""),
            ("xmas_c15_store2", 2, """{"handItems":[95,94,93,92,67,68,69,48],"animates":false}"""),
            ("xmas_c15_store3", 2, """{"handItems":[81],"animates":false}"""),
            ("xmas_c15_store4", 2, """{"handItems":[84],"animates":false}"""),
            ("xmas_c16_mailshelf", 2, """{"handItems":[110],"animates":false}"""),
            ("xmas_c17_chestnutstall", 2, """{"handItems":[60],"animates":false}"""),
            ("xmas_c18_borscht", 2, """{"handItems":[123],"animates":false}"""),
            ("xmas_c19_stackedicedrinks", 2, """{"handItems":[142],"animates":false}"""),
            ("xmas_giftbag", 2, """{"handItems":[1025,1026,1027],"animates":false}"""),
            ("xmas_r20_magicsword", 2, """{"handItems":[1094],"animates":false}"""),
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

        /// <summary>Furni whose behaviour is their logic alone, with the logic.</summary>
        private static readonly (string Name, string Logic)[] LOGICS =
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
            ("bb_rnd_tele", "random_teleport"),
            ("hween13_tile1", "random_teleport"),
            ("hween13_tile2", "random_teleport"),
            ("room_gh15_rtele", "random_teleport"),
            ("hole", "floor_hole"),
            ("hole1x1", "floor_hole"),
            ("hole2", "floor_hole"),
            ("hole3", "floor_hole"),
        ];

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
