using System.Collections.Immutable;
using System.Text.Json;
using Turbo.Achievements;
using Turbo.Database.Entities.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;
using Xunit;

namespace Turbo.Tests.Achievements;

public class AchievementReducerEngineTests
{
    [Fact]
    public void Counter_QualifiesImmediatelyAndKeepsCountingPastTheLastLevel()
    {
        var definition = Definition(AchievementReducer.Counter, 10, 20);
        var progress = new AchievementProgressEntity();

        AchievementReducerEngine.Apply(
            progress,
            definition,
            Fact(amount: 23),
            maxDistinctValues: 10
        );

        Assert.Equal(23, progress.Value);
        Assert.True(
            AchievementReducerEngine.Qualifies(definition, definition.Levels[0], progress.Value)
        );
        Assert.True(
            AchievementReducerEngine.Qualifies(definition, definition.Levels[1], progress.Value)
        );
        AchievementReducerEngine.Apply(
            progress,
            definition,
            Fact(amount: 7),
            maxDistinctValues: 10
        );
        Assert.Equal(30, progress.Value);
    }

    [Fact]
    public void Counter_ThrowsInsteadOfWrappingOnOverflow()
    {
        var definition = Definition(AchievementReducer.Counter, int.MaxValue);
        var progress = new AchievementProgressEntity { Value = long.MaxValue - 1 };

        Assert.Throws<OverflowException>(() =>
            AchievementReducerEngine.Apply(
                progress,
                definition,
                Fact(amount: 2),
                maxDistinctValues: 10
            )
        );
        Assert.Equal(long.MaxValue - 1, progress.Value);
    }

    [Fact]
    public void Maximum_RetainsTheLargestObservedValue()
    {
        var definition = Definition(AchievementReducer.Maximum, 10);
        var progress = new AchievementProgressEntity();

        AchievementReducerEngine.Apply(
            progress,
            definition,
            Fact(amount: 12),
            maxDistinctValues: 10
        );
        AchievementReducerEngine.Apply(
            progress,
            definition,
            Fact(amount: 3),
            maxDistinctValues: 10
        );

        Assert.Equal(12, progress.Value);
    }

    [Fact]
    public void Distinct_CountsEachNewValueOnceAndEnforcesItsStorageLimit()
    {
        var progress = new AchievementProgressEntity();

        AchievementReducerEngine.ApplyDistinct(progress, Fact(value: "room-a"), true, 2);
        AchievementReducerEngine.ApplyDistinct(progress, Fact(value: "room-a"), false, 2);
        AchievementReducerEngine.ApplyDistinct(progress, Fact(value: "room-b"), true, 2);

        Assert.Equal(2, progress.Value);
        Assert.Equal(2, progress.DistinctCount);
        Assert.Throws<InvalidOperationException>(() =>
            AchievementReducerEngine.ApplyDistinct(progress, Fact(value: "room-c"), true, 2)
        );
        Assert.Equal(2, progress.Value);
        Assert.Throws<InvalidOperationException>(() =>
            AchievementReducerEngine.ApplyDistinct(progress, Fact(value: " "), true, 2)
        );
    }

    [Fact]
    public void Distinct_IsNotAppliedInlineAnymore()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AchievementReducerEngine.Apply(
                new AchievementProgressEntity(),
                Definition(AchievementReducer.Distinct, 2),
                Fact(value: "room-a"),
                maxDistinctValues: 2
            )
        );
    }

    [Fact]
    public void CalendarStreak_IgnoresDuplicateAndOutOfOrderUtcDays()
    {
        var definition = Definition(AchievementReducer.CalendarStreak, 2);
        var progress = new AchievementProgressEntity();
        var firstDay = Utc(2026, 1, 1);

        AchievementReducerEngine.Apply(
            progress,
            definition,
            Fact(occurredAtUtc: firstDay),
            maxDistinctValues: 10
        );
        AchievementReducerEngine.Apply(
            progress,
            definition,
            Fact(occurredAtUtc: firstDay),
            maxDistinctValues: 10
        );
        AchievementReducerEngine.Apply(
            progress,
            definition,
            Fact(occurredAtUtc: firstDay.AddDays(-1)),
            maxDistinctValues: 10
        );

        Assert.Equal(1, progress.Streak);
        Assert.Equal(firstDay, progress.LastDayUtc);

        AchievementReducerEngine.Apply(
            progress,
            definition,
            Fact(occurredAtUtc: firstDay.AddDays(1)),
            maxDistinctValues: 10
        );
        Assert.Equal(2, progress.Streak);
        Assert.Equal(2, progress.Value);
    }

    [Fact]
    public void CalendarStreak_ResetsAfterAMissedUtcDay()
    {
        var definition = Definition(AchievementReducer.CalendarStreak, 2);
        var progress = new AchievementProgressEntity();
        var firstDay = Utc(2026, 1, 1);

        AchievementReducerEngine.Apply(
            progress,
            definition,
            Fact(occurredAtUtc: firstDay),
            maxDistinctValues: 10
        );
        AchievementReducerEngine.Apply(
            progress,
            definition,
            Fact(occurredAtUtc: firstDay.AddDays(1)),
            maxDistinctValues: 10
        );
        AchievementReducerEngine.Apply(
            progress,
            definition,
            Fact(occurredAtUtc: firstDay.AddDays(3)),
            maxDistinctValues: 10
        );

        Assert.Equal(1, progress.Streak);
        Assert.Equal(2, progress.Value);
    }

    [Fact]
    public void ElapsedSeconds_MergesOverlappingAndRepeatedUtcIntervals()
    {
        var definition = Definition(AchievementReducer.ElapsedSeconds, 60);
        var progress = new AchievementProgressEntity();
        var start = Utc(2026, 1, 1);

        ApplyInterval(progress, definition, start, start.AddSeconds(60), "session-a");
        ApplyInterval(
            progress,
            definition,
            start.AddSeconds(30),
            start.AddSeconds(90),
            "session-b"
        );
        ApplyInterval(progress, definition, start, start.AddSeconds(60), "session-replay");

        Assert.Equal(90, progress.Value);
        var intervals = JsonSerializer.Deserialize<List<long[]>>(progress.IntervalsJson)!;
        var interval = Assert.Single(intervals);
        Assert.Equal(start.Ticks, interval[0]);
        Assert.Equal(start.AddSeconds(90).Ticks, interval[1]);
    }

    [Fact]
    public void ElapsedSeconds_RejectsNonUtcOrInvalidIntervals()
    {
        var definition = Definition(AchievementReducer.ElapsedSeconds, 60);
        var progress = new AchievementProgressEntity();
        var start = Utc(2026, 1, 1);
        var nonUtc = DateTime.SpecifyKind(start, DateTimeKind.Local);

        Assert.Throws<InvalidOperationException>(() =>
            AchievementReducerEngine.Apply(
                progress,
                definition,
                Fact(
                    intervalStartUtc: nonUtc,
                    intervalEndUtc: start.AddSeconds(5),
                    occurredAtUtc: start.AddSeconds(5),
                    sessionId: "session-a"
                ),
                maxDistinctValues: 10
            )
        );
        Assert.Throws<InvalidOperationException>(() =>
            AchievementReducerEngine.Apply(
                progress,
                definition,
                Fact(
                    intervalStartUtc: start.AddSeconds(10),
                    intervalEndUtc: start,
                    occurredAtUtc: start.AddSeconds(10),
                    sessionId: "session-b"
                ),
                maxDistinctValues: 10
            )
        );
    }

    [Fact]
    public void ElapsedSeconds_EnforcesItsIntervalStorageLimit()
    {
        var definition = Definition(AchievementReducer.ElapsedSeconds, 60);
        var progress = new AchievementProgressEntity();
        var start = Utc(2026, 1, 1);
        ApplyInterval(progress, definition, start, start.AddSeconds(1), "session-a");
        ApplyInterval(
            progress,
            definition,
            start.AddDays(1),
            start.AddDays(1).AddSeconds(1),
            "session-b"
        );

        Assert.Throws<InvalidOperationException>(() =>
            ApplyInterval(
                progress,
                definition,
                start.AddDays(2),
                start.AddDays(2).AddSeconds(1),
                "session-c",
                maxDistinctValues: 2
            )
        );
        Assert.Equal(2, progress.Value);
    }

    [Fact]
    public void Rank_KeepsTheBestPositiveRankAndQualifiesAtOrAboveItsThreshold()
    {
        var definition = Definition(AchievementReducer.Rank, 10, 3);
        var progress = new AchievementProgressEntity();

        AchievementReducerEngine.Apply(
            progress,
            definition,
            Fact(amount: 8),
            maxDistinctValues: 10
        );
        AchievementReducerEngine.Apply(
            progress,
            definition,
            Fact(amount: 0),
            maxDistinctValues: 10
        );
        AchievementReducerEngine.Apply(
            progress,
            definition,
            Fact(amount: 12),
            maxDistinctValues: 10
        );
        Assert.Equal(8, progress.Value);
        Assert.True(
            AchievementReducerEngine.Qualifies(definition, definition.Levels[0], progress.Value)
        );
        Assert.False(
            AchievementReducerEngine.Qualifies(definition, definition.Levels[1], progress.Value)
        );

        AchievementReducerEngine.Apply(
            progress,
            definition,
            Fact(amount: 2),
            maxDistinctValues: 10
        );
        Assert.Equal(2, progress.Value);
        Assert.True(
            AchievementReducerEngine.Qualifies(definition, definition.Levels[1], progress.Value)
        );
        Assert.False(AchievementReducerEngine.Qualifies(definition, definition.Levels[0], 0));
    }

    private static void ApplyInterval(
        AchievementProgressEntity progress,
        AchievementDefinition definition,
        DateTime start,
        DateTime end,
        string sessionId,
        int maxDistinctValues = 10
    ) =>
        AchievementReducerEngine.Apply(
            progress,
            definition,
            Fact(
                intervalStartUtc: start,
                intervalEndUtc: end,
                occurredAtUtc: end,
                sessionId: sessionId
            ),
            maxDistinctValues
        );

    private static AchievementDefinition Definition(
        AchievementReducer reducer,
        params int[] requirements
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

    private static AchievementFact Fact(
        long amount = 1,
        string value = "",
        DateTime? occurredAtUtc = null,
        DateTime? intervalStartUtc = null,
        DateTime? intervalEndUtc = null,
        string? sessionId = null
    ) =>
        new()
        {
            OperationId = Guid.NewGuid().ToString("N"),
            Source = "test.source",
            OccurredAtUtc = occurredAtUtc ?? Utc(2026, 1, 1),
            Amount = amount,
            Value = value,
            IntervalStartUtc = intervalStartUtc,
            IntervalEndUtc = intervalEndUtc,
            SessionId = sessionId,
        };

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
