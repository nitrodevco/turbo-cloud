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
}
