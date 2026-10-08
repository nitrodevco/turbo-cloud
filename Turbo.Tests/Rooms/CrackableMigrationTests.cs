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
}
