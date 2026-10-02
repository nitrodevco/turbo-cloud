using System.Collections.Immutable;
using Turbo.Achievements;
using Turbo.Database.Entities.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;
using Xunit;

namespace Turbo.Tests.Achievements;

public class AchievementProjectionTests
{
    [Fact]
    public void ToSnapshot_ProjectsCumulativeProgressTowardTheNextLevel()
    {
        var definition = Definition(AchievementReducer.Counter, [100, 300]);
        var progress = new AchievementProgressEntity { Value = 180, EarnedLevel = 1 };

        var snapshot = AchievementProjection.ToSnapshot(definition, progress);

        Assert.Equal(2, snapshot.Level);
        Assert.Equal("ACH_Test2", snapshot.BadgeId);
        Assert.Equal(100, snapshot.ScoreAtStartOfLevel);
        Assert.Equal(300, snapshot.ScoreLimitTotal);
        Assert.Equal(180, snapshot.CurrentPointsTotal);
        Assert.False(snapshot.FinalLevel);
    }

    [Fact]
    public void ToSnapshot_MapsTheFinalEarnedLevelToItsTargetBadge()
    {
        var definition = Definition(AchievementReducer.Counter, [100, 300]);
        var progress = new AchievementProgressEntity { Value = 350, EarnedLevel = 2 };

        var snapshot = AchievementProjection.ToSnapshot(definition, progress);

        Assert.Equal(2, snapshot.Level);
        Assert.Equal("ACH_Test2", snapshot.BadgeId);
        Assert.Equal(100, snapshot.ScoreAtStartOfLevel);
        Assert.Equal(300, snapshot.ScoreLimitTotal);
        Assert.Equal(350, snapshot.CurrentPointsTotal);
        Assert.True(snapshot.FinalLevel);
    }

    [Fact]
    public void ToSnapshot_ClampsNegativeAndOverIntProgressWithoutOverflow()
    {
        var definition = Definition(AchievementReducer.Counter, [int.MaxValue]);

        var belowZero = AchievementProjection.ToSnapshot(
            definition,
            new AchievementProgressEntity { Value = -10 }
        );
        var aboveIntMax = AchievementProjection.ToSnapshot(
            definition,
            new AchievementProgressEntity { Value = long.MaxValue }
        );

        Assert.Equal(0, belowZero.CurrentPointsTotal);
        Assert.Equal(int.MaxValue, aboveIntMax.CurrentPointsTotal);
    }

    [Fact]
    public void ToSnapshot_UsesRankPositionAndDoesNotSubtractCumulativeBoundaries()
    {
        var definition = Definition(AchievementReducer.Rank, [10, 3]);
        var progress = new AchievementProgressEntity { Value = 4, EarnedLevel = 1 };

        var snapshot = AchievementProjection.ToSnapshot(definition, progress);

        Assert.Equal(2, snapshot.Level);
        Assert.Equal("ACH_Test2", snapshot.BadgeId);
        Assert.Equal(0, snapshot.ScoreAtStartOfLevel);
        Assert.Equal(3, snapshot.ScoreLimitTotal);
        Assert.Equal(4, snapshot.CurrentPointsTotal);
        Assert.False(snapshot.FinalLevel);
    }

    private static AchievementDefinition Definition(
        AchievementReducer reducer,
        int[] requirements
    ) =>
        new()
        {
            Id = 1001,
            Key = "test_achievement",
            Revision = 1,
            Category = "test",
            Source = "test.source",
            Reducer = reducer,
            Levels = requirements
                .Select(
                    (requirement, index) =>
                        new AchievementLevelDefinition
                        {
                            Requirement = requirement,
                            BadgeCode = $"ACH_Test{index + 1}",
                            Score = 10,
                            Rewards = ImmutableArray<AchievementReward>.Empty,
                        }
                )
                .ToImmutableArray(),
        };
}
