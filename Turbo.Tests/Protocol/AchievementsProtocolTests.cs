using Turbo.Primitives.Achievements.Enums;
using Turbo.Primitives.Achievements.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

public class AchievementsProtocolTests
{
    [Fact]
    public void AchievementsList_WritesCountRecordsAndDefaultCategory()
    {
        var achievements = new[]
        {
            Snapshot(achievementId: 31, start: 100, limit: 250, total: 180),
            Snapshot(
                achievementId: 32,
                level: 4,
                badgeId: "ACH_builder4",
                start: 250,
                limit: 400,
                total: 250,
                finalLevel: true
            ),
        };

        var packet = PacketHarness.Encode(
            new AchievementsEventMessageComposer
            {
                Achievements = achievements,
                DefaultCategory = "achievements",
            }
        );

        Assert.Equal(2, packet.PopInt());
        AssertAchievement(packet, achievements[0]);
        AssertAchievement(packet, achievements[1]);
        Assert.Equal("achievements", packet.PopString());
        Assert.True(packet.End);
    }

    [Fact]
    public void AchievementUpdate_WritesTheSameCumulativeAchievementRecord()
    {
        var achievement = Snapshot(start: 100, limit: 250, total: 180);

        var packet = PacketHarness.Encode(
            new AchievementEventMessageComposer { Achievement = achievement }
        );

        AssertAchievement(packet, achievement);
        Assert.True(packet.End);
    }

    [Fact]
    public void AchievementScore_WritesTheScore()
    {
        var packet = PacketHarness.Encode(
            new AchievementsScoreEventMessageComposer { Score = 527 }
        );

        Assert.Equal(527, packet.PopInt());
        Assert.True(packet.End);
    }

    [Fact]
    public void AchievementNotification_WritesTheClientFieldOrder()
    {
        var packet = PacketHarness.Encode(
            new HabboAchievementNotificationMessageComposer
            {
                Type = 1,
                Level = 3,
                BadgeId = 84,
                BadgeCode = "ACH_builder3",
                PointsTotal = 180,
                LevelRewardPoints = 10,
                LevelRewardPointType = 0,
                BonusPoints = 2,
                AchievementId = 31,
                RemovedBadgeCode = "ACH_builder2",
                Category = "builder",
                ShowDialogToUser = true,
                OwnerCount = 16,
                BadgeRarityId = 2,
            }
        );

        Assert.Equal(1, packet.PopInt());
        Assert.Equal(3, packet.PopInt());
        Assert.Equal(84, packet.PopInt());
        Assert.Equal("ACH_builder3", packet.PopString());
        Assert.Equal(180, packet.PopInt());
        Assert.Equal(10, packet.PopInt());
        Assert.Equal(0, packet.PopInt());
        Assert.Equal(2, packet.PopInt());
        Assert.Equal(31, packet.PopInt());
        Assert.Equal("ACH_builder2", packet.PopString());
        Assert.Equal("builder", packet.PopString());
        Assert.True(packet.PopBoolean());
        Assert.Equal(16, packet.PopInt());
        Assert.Equal(2, packet.PopInt());
        Assert.True(packet.End);
    }

    private static AchievementSnapshot Snapshot(
        int achievementId = 31,
        int level = 3,
        string badgeId = "ACH_builder3",
        int start = 100,
        int limit = 250,
        int total = 180,
        bool finalLevel = false
    ) =>
        new()
        {
            AchievementId = achievementId,
            Level = level,
            BadgeId = badgeId,
            ScoreAtStartOfLevel = start,
            ScoreLimitTotal = limit,
            LevelRewardPoints = 10,
            LevelRewardPointType = 0,
            CurrentPointsTotal = total,
            FinalLevel = finalLevel,
            Category = "builder",
            SubCategory = "all",
            LevelCount = 4,
            DisplayMethod = 0,
            State = AchievementState.Enabled,
        };

    private static void AssertAchievement(
        Turbo.Primitives.Packets.ClientPacket packet,
        AchievementSnapshot achievement
    )
    {
        Assert.Equal(achievement.AchievementId, packet.PopInt());
        Assert.Equal(achievement.Level, packet.PopInt());
        Assert.Equal(achievement.BadgeId, packet.PopString());
        Assert.Equal(achievement.ScoreAtStartOfLevel, packet.PopInt());
        Assert.Equal(achievement.ScoreLimitTotal, packet.PopInt());
        Assert.Equal(achievement.LevelRewardPoints, packet.PopInt());
        Assert.Equal(achievement.LevelRewardPointType, packet.PopInt());
        Assert.Equal(achievement.CurrentPointsTotal, packet.PopInt());
        Assert.Equal(achievement.FinalLevel, packet.PopBoolean());
        Assert.Equal(achievement.Category, packet.PopString());
        Assert.Equal(achievement.SubCategory, packet.PopString());
        Assert.Equal(achievement.LevelCount, packet.PopInt());
        Assert.Equal(achievement.DisplayMethod, packet.PopInt());
        Assert.Equal((short)achievement.State, packet.PopShort());
    }
}
