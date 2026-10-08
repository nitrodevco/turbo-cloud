using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Migrations;
using Turbo.Furniture;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// What <c>MapFurnitureBehaviours</c> writes is what the logics read: every crackable and vending
/// section parses, every reward can be given, every logic it names is one the room registers,
/// and no furni is mapped twice.
/// </summary>
public sealed class FurnitureBehaviourMigrationTests
{
    private static T Field<T>(string name) =>
        (T)
            typeof(MapFurnitureBehaviours)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;

    private static (
        string Name,
        int States,
        int Usage,
        bool Walkable,
        string Crackable
    )[] Crackables() => Field<(string, int, int, bool, string)[]>("CRACKABLES");

    private static (string Name, int Usage, string Vending)[] Vending() =>
        Field<(string, int, string)[]>("VENDING");

    private static (string Name, string Logic)[] Logics() => Field<(string, string)[]>("LOGICS");

    private static T Read<T>(string section, string json)
        where T : class =>
        FurnitureExtraDataSections.Read<T>(
            new ExtraData(null),
            $$"""{"{{section}}":{{json}}}""",
            section,
            NullLogger.Instance
        )!;

    private static CrackableData Crackable(string name) =>
        Read<CrackableData>(
            CrackableData.SECTION,
            Crackables().Single(x => x.Name == name).Crackable
        );

    private static VendingMachineData Vending(string name) =>
        Read<VendingMachineData>(
            VendingMachineData.SECTION,
            Vending().Single(x => x.Name == name).Vending
        );

    [Fact]
    public void Every_furni_is_mapped_once_to_a_logic_the_room_registers()
    {
        var registered = typeof(FurnitureEffectAreaLogic)
            .Assembly.GetTypes()
            .Select(x => x.GetCustomAttribute<RoomObjectLogicAttribute>()?.Key)
            .OfType<string>()
            .ToHashSet();
        var names = Crackables()
            .Select(x => x.Name)
            .Concat(Vending().Select(x => x.Name))
            .Concat(Logics().Select(x => x.Name));

        names.Should().OnlyHaveUniqueItems();
        registered.Should().Contain(["crackable", "vending_machine"]);
        Logics().Select(x => x.Logic).Should().OnlyContain(x => registered.Contains(x));
        Logics()
            .Select(x => x.Logic)
            .Distinct()
            .Should()
            .BeEquivalentTo([
                "effect_box",
                "effect_provider",
                "effect_area",
                "effect_tile",
                "random_teleport",
                "floor_hole",
            ]);
    }

    [Fact]
    public void Every_crackable_section_reads_and_every_listed_reward_can_be_given()
    {
        var rows = Crackables();

        // 170 in the XML data and 136 newer; contents for the club boxes and 284 a source lists.
        rows.Should().HaveCount(306);
        rows.Count(x => Read<CrackableData>(CrackableData.SECTION, x.Crackable).HasReward)
            .Should()
            .Be(286);

        foreach (var row in rows)
        {
            var data = Read<CrackableData>(CrackableData.SECTION, row.Crackable);

            data.Target.Should().BeGreaterThan(0, row.Name);
            row.States.Should().BePositive(row.Name);
            row.Usage.Should().BeOneOf([1, 2], row.Name);
            data.Rewards.Where(x => !x.IsValid).Should().BeEmpty(row.Name);
            data.Draws.SelectMany(x => x.Rewards).Where(x => !x.IsValid).Should().BeEmpty(row.Name);
        }
    }

    [Theory]
    [InlineData("bc_gift_14days", SubscriptionType.BuildersClub, 14)]
    [InlineData("bc_gift_31days", SubscriptionType.BuildersClub, 31)]
    [InlineData("hc_gift_14days", SubscriptionType.HabboClub, 14)]
    [InlineData("hc_gift_31days", SubscriptionType.HabboClub, 31)]
    public void The_club_boxes_hold_their_membership(string name, SubscriptionType type, int days)
    {
        var data = Crackable(name);

        data.Target.Should().Be(1);
        data.Rewards.Should()
            .ContainSingle()
            .Which.Should()
            .Match<CrackableReward>(x => x.Subscription == type && x.SubscriptionDays == days);
    }

    [Fact]
    public void Pinatas_are_everyones_walked_under_with_the_stick()
    {
        foreach (var row in Crackables().Where(x => x.Name.StartsWith("hblooza_pinata")))
        {
            var data = Crackable(row.Name);

            row.Usage.Should().Be(2, row.Name);
            row.Walkable.Should().BeTrue(row.Name);
            data.HitOn.Should().Be(CrackableHitOn.Walk, row.Name);
            data.RequiredEffectId.Should().Be(158, row.Name);
            data.Target.Should().Be(100, row.Name);
        }
    }

    [Fact]
    public void A_fallen_angel_gives_three_decorations_at_once_to_the_inventory_wearing_a_torch()
    {
        var data = Crackable("hween_c22_darkangel");

        data.Target.Should().Be(22);
        data.AcceptsEffect(5).Should().BeTrue();
        data.AcceptsEffect(0).Should().BeFalse();
        data.RewardPlacement.Should().Be(CrackablePlacement.Inventory);
        data.Rewards.Should().OnlyContain(x => x.Also.Length == 2);
    }

    [Fact]
    public void The_fossil_rock_takes_either_pickaxe()
    {
        var data = Crackable("dino_c22_fossilrock");

        data.AcceptsEffect(182).Should().BeTrue();
        data.AcceptsEffect(183).Should().BeTrue();
        data.AcceptsEffect(158).Should().BeFalse();
    }

    [Fact]
    public void A_coral_kingdom_chest_gives_three_commons_and_one_more_at_60_30_10()
    {
        var data = Crackable("coralking_c18_treasurechest");

        data.RewardPlacement.Should().Be(CrackablePlacement.Inventory);
        data.Draws.Should().HaveCount(2);
        data.Draws[0].Count.Should().Be(3);
        data.Draws[0].Rewards.Should().HaveCount(15);

        var extra = data.Draws[1].Rewards;
        var total = (double)extra.Sum(x => x.Weight);
        var common = data.Draws[0].Rewards.Select(x => x.Furni).ToHashSet();
        var rare = new[] { "clothing_r18_seawreath", "clothing_r18_goldfish" };

        (extra.Where(x => common.Contains(x.Furni)).Sum(x => x.Weight) / total).Should().Be(0.6);
        (extra.Where(x => rare.Contains(x.Furni)).Sum(x => x.Weight) / total).Should().Be(0.1);
    }

    [Fact]
    public void A_winter_palace_box_is_the_next_box_half_the_time_and_the_last_a_crown_of_frost()
    {
        var box = Crackable("xmas_c19_box1");
        var total = (double)box.Rewards.Sum(x => x.Weight);

        (box.Rewards.Single(x => x.Furni == "xmas_c19_box2").Weight / total).Should().Be(0.5);
        Crackable("xmas_c19_box6")
            .Rewards.Should()
            .ContainSingle()
            .Which.Furni.Should()
            .Be("clothing_icecrown");
    }

    [Fact]
    public void A_growing_plant_keeps_its_watering_can_and_achievement_with_its_contents()
    {
        var rose = Crackable("easter_c18_rose1");

        rose.Target.Should().Be(12);
        rose.RequiredEffectId.Should().Be(192);
        rose.FinalHitAchievement.Should().Be("AdvancedHorticulturist");
        rose.Rewards.Select(x => x.Furni)
            .Should()
            .BeEquivalentTo([
                "easter_c18_rose1",
                "easter_c18_rose2",
                "easter_c18_rose3",
                "easter_c18_rose4",
                "easter_c18_badflower",
            ]);
    }

    [Fact]
    public void Every_vending_section_reads_with_hand_items_to_give()
    {
        var rows = Vending();

        // 280 in the XML data and 91 newer.
        rows.Should().HaveCount(371);

        foreach (var row in rows)
        {
            Read<VendingMachineData>(VendingMachineData.SECTION, row.Vending)
                .HandItems.Should()
                .NotBeEmpty(row.Name)
                .And.OnlyContain(x => x > 0);
            row.Usage.Should().BeOneOf([1, 2], row.Name);
        }
    }

    [Theory]
    [InlineData("fridge", new[] { 3, 4, 5, 6 }, true)]
    [InlineData("rare_icecream", new[] { 4 }, true)]
    [InlineData("brbirthday_c25_coxinha", new[] { 169 }, false)]
    [InlineData("nft_samovar", new[] { 1 }, true)]
    [InlineData("nft_h25_xmasicm", new[] { 4 }, true)]
    public void A_vending_furni_hands_out_its_items(string name, int[] handItems, bool animates)
    {
        var data = Vending(name);

        data.HandItems.Should().Equal(handItems);
        data.Animates.Should().Be(animates);
    }

    [Fact]
    public void The_effect_furni_and_the_new_user_rooms_tiles_are_mapped()
    {
        Logics().Count(x => x.Logic.StartsWith("effect_")).Should().Be(81);
        Logics().Should().Contain(("room_noob_fxremove", "effect_tile"));
        Logics().Should().Contain(("cpunk15_gunvender", "effect_provider"));
        Logics().Should().Contain(("bb_rnd_tele", "random_teleport"));
        Logics().Should().Contain(("hole", "floor_hole"));
    }
}
