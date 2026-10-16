using FluentAssertions;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Messages.Outgoing.Inventory.Badges;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

/// <summary>
/// A badge's owner count as Habbo writes it (official client capture, 498 badge rows): up to
/// 1000 as it is, and 1000 for any badge more players own - never more.
/// </summary>
public sealed class BadgeOwnerCountWireTests
{
    [Theory]
    [InlineData(28, 28)]
    [InlineData(999, 999)]
    [InlineData(1000, 1000)]
    [InlineData(250000, 1000)]
    public void A_new_badge_says_at_most_a_thousand_owners(int owners, int written)
    {
        var packet = PacketHarness.Encode(
            new BadgeReceivedEventMessageComposer
            {
                Badge = new PlayerBadgeSnapshot
                {
                    BadgeId = 7,
                    BadgeCode = "ACH_Login1",
                    SlotId = 0,
                    OwnerCount = owners,
                    Rarity = BadgeRarityType.Common,
                },
            }
        );

        packet.PopInt().Should().Be(7);
        packet.PopString().Should().Be("ACH_Login1");
        packet.PopInt().Should().Be(written);
        packet.PopInt().Should().Be((int)BadgeRarityType.Common);
    }

    [Fact]
    public void An_achievement_level_says_at_most_a_thousand_owners()
    {
        var packet = PacketHarness.Encode(
            new HabboAchievementNotificationMessageComposer
            {
                Type = 1,
                Level = 2,
                BadgeId = 7,
                BadgeCode = "ACH_Login2",
                PointsTotal = 20,
                LevelRewardPoints = 0,
                LevelRewardPointType = 0,
                BonusPoints = 0,
                AchievementId = 1,
                RemovedBadgeCode = "ACH_Login1",
                Category = "identity",
                ShowDialogToUser = false,
                OwnerCount = 250000,
                BadgeRarityId = 0,
            }
        );

        for (var i = 0; i < 3; i++)
            packet.PopInt();
        packet.PopString();
        for (var i = 0; i < 5; i++)
            packet.PopInt();
        packet.PopString();
        packet.PopString();
        packet.PopBoolean();

        packet.PopInt().Should().Be(1000);
    }
}
