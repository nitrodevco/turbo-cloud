using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Migrations;
using Turbo.Furniture;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Players.Enums;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The crackable section the migration writes for each of Sulake's crackables is what the logic
/// reads: every one parses, names only known enum values, and every reward it lists can be given.
/// </summary>
public sealed class CrackableMigrationTests
{
    private static (string Name, int States, int Usage, bool Walkable, string Crackable)[] Rows() =>
        ((string, int, int, bool, string)[])
            typeof(RefineCrackableFurni)
                .GetField("CRACKABLES", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;

    private static CrackableData Read(string crackable) =>
        FurnitureExtraDataSections.Read<CrackableData>(
            new ExtraData(null),
            $$"""{"{{CrackableData.SECTION}}":{{crackable}}}""",
            CrackableData.SECTION,
            NullLogger.Instance
        )!;

    [Fact]
    public void Every_crackable_section_reads_and_every_listed_reward_can_be_given()
    {
        var rows = Rows();

        rows.Should().HaveCount(170);
        rows.Select(x => x.Name).Should().OnlyHaveUniqueItems();
        // The club boxes and the 73 crackables whose contents a source lists.
        rows.Count(x => Read(x.Crackable).HasReward).Should().Be(77);

        foreach (var row in rows)
        {
            var data = Read(row.Crackable);

            data.Target.Should().BeGreaterThan(0, row.Name);
            row.States.Should().BePositive(row.Name);
            row.Usage.Should().BeOneOf([1, 2], row.Name);
            data.Rewards.Where(x => !x.IsValid).Should().BeEmpty(row.Name);
        }
    }

    [Theory]
    [InlineData("bc_gift_14days", SubscriptionType.BuildersClub, 14)]
    [InlineData("bc_gift_31days", SubscriptionType.BuildersClub, 31)]
    [InlineData("hc_gift_14days", SubscriptionType.HabboClub, 14)]
    [InlineData("hc_gift_31days", SubscriptionType.HabboClub, 31)]
    public void The_club_boxes_hold_their_membership(string name, SubscriptionType type, int days)
    {
        var data = Read(Rows().Single(x => x.Name == name).Crackable);

        data.Target.Should().Be(1);
        data.Rewards.Should()
            .ContainSingle()
            .Which.Should()
            .Match<CrackableReward>(x => x.Subscription == type && x.SubscriptionDays == days);
    }

    [Fact]
    public void Pinatas_are_everyones_walked_under_with_the_stick()
    {
        foreach (var row in Rows().Where(x => x.Name.Contains("pinata")))
        {
            var data = Read(row.Crackable);

            row.Usage.Should().Be(2, row.Name);
            row.Walkable.Should().BeTrue(row.Name);
            data.HitOn.Should().Be(CrackableHitOn.Walk, row.Name);
            data.RequiredEffectId.Should().Be(158, row.Name);
            data.Target.Should().Be(100, row.Name);
        }
    }

    private static (string Name, int States, int Usage, string Crackable)[] NewerRows() =>
        ((string, int, int, string)[])
            typeof(MapNewerCrackableFurni)
                .GetField("CRACKABLES", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;

    [Fact]
    public void Every_newer_crackable_section_reads_and_none_is_mapped_twice()
    {
        var rows = NewerRows();

        rows.Should().HaveCount(136);
        rows.Select(x => x.Name)
            .Should()
            .OnlyHaveUniqueItems()
            .And.NotIntersectWith(Rows().Select(x => x.Name));
        rows.Count(x => Read(x.Crackable).HasReward).Should().Be(128);

        foreach (var row in rows)
        {
            var data = Read(row.Crackable);

            data.Target.Should().BeGreaterThan(0, row.Name);
            row.States.Should().BePositive(row.Name);
            row.Usage.Should().BeOneOf([1, 2], row.Name);
            data.Rewards.Where(x => !x.IsValid).Should().BeEmpty(row.Name);
        }
    }

    [Fact]
    public void A_fallen_angel_gives_three_decorations_at_once_to_the_inventory_wearing_a_torch()
    {
        var data = Read(NewerRows().Single(x => x.Name == "hween_c22_darkangel").Crackable);

        data.Target.Should().Be(22);
        data.AcceptsEffect(5).Should().BeTrue();
        data.AcceptsEffect(0).Should().BeFalse();
        data.RewardPlacement.Should().Be(CrackablePlacement.Inventory);
        data.Rewards.Should().OnlyContain(x => x.Also.Length == 2);
    }

    [Fact]
    public void The_fossil_rock_takes_either_pickaxe()
    {
        var data = Read(NewerRows().Single(x => x.Name == "dino_c22_fossilrock").Crackable);

        data.AcceptsEffect(182).Should().BeTrue();
        data.AcceptsEffect(183).Should().BeTrue();
        data.AcceptsEffect(158).Should().BeFalse();
    }

    /// <summary>
    /// A filled section as the database ends up with it: <c>FillCrackableChains</c>'s contents merged
    /// over what <c>RefineCrackableFurni</c> wrote (JSON_MERGE_PATCH: its keys replace, the rest stay).
    /// </summary>
    private static CrackableData Filled(string name)
    {
        var fills = ((string Name, string Contents)[])
            typeof(FillCrackableChains)
                .GetField("CONTENTS", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;
        var section = System
            .Text.Json.Nodes.JsonNode.Parse(Rows().Single(x => x.Name == name).Crackable)!
            .AsObject();

        foreach (
            var (key, value) in System
                .Text.Json.Nodes.JsonNode.Parse(fills.Single(x => x.Name == name).Contents)!
                .AsObject()
                .ToArray()
        )
            section[key] = value?.DeepClone();

        return Read(section.ToJsonString());
    }

    [Fact]
    public void Every_filled_crackable_holds_rewards_that_can_be_given_and_keeps_how_it_is_hit()
    {
        var fills = ((string Name, string Contents)[])
            typeof(FillCrackableChains)
                .GetField("CONTENTS", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;

        fills.Should().HaveCount(81);
        fills
            .Select(x => x.Name)
            .Should()
            .OnlyHaveUniqueItems()
            .And.BeSubsetOf(Rows().Select(x => x.Name));

        foreach (var (name, _) in fills)
        {
            var data = Filled(name);
            var before = Read(Rows().Single(x => x.Name == name).Crackable);

            data.HasReward.Should().BeTrue(name);
            data.Rewards.Where(x => !x.IsValid).Should().BeEmpty(name);
            data.Draws.SelectMany(x => x.Rewards).Where(x => !x.IsValid).Should().BeEmpty(name);
            (data.Target, data.RequiredEffectId, data.RewardPlacement, data.FinalHitAchievement)
                .Should()
                .Be(
                    (
                        before.Target,
                        before.RequiredEffectId,
                        before.RewardPlacement,
                        before.FinalHitAchievement
                    ),
                    name
                );
        }
    }

    [Fact]
    public void A_coral_kingdom_chest_gives_three_commons_and_one_more_at_60_30_10()
    {
        var data = Filled("coralking_c18_treasurechest");

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
        var box = Filled("xmas_c19_box1");
        var total = (double)box.Rewards.Sum(x => x.Weight);

        (box.Rewards.Single(x => x.Furni == "xmas_c19_box2").Weight / total).Should().Be(0.5);
        Filled("xmas_c19_box6")
            .Rewards.Should()
            .ContainSingle()
            .Which.Furni.Should()
            .Be("clothing_icecrown");
    }
}
