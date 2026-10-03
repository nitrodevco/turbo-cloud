using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class AchievementMatchTests
{
    private static AchievementDefinition Definition(AchievementMatch? match) =>
        new()
        {
            Id = 100400,
            Key = "match",
            Revision = 1,
            Category = "explore",
            Source = AchievementSources.VISIT,
            Reducer = AchievementReducer.Distinct,
            Match = match,
            Levels = [new() { Requirement = 1, BadgeCode = "ACH_Match1" }],
        };

    private static AchievementFact Fact(string value, DateTime? at = null) =>
        new()
        {
            OperationId = "op",
            Source = AchievementSources.VISIT,
            Value = value,
            OccurredAtUtc = at ?? new DateTime(2026, 12, 25, 12, 0, 0, DateTimeKind.Utc),
        };

    [Fact]
    public void WithoutAValueListEveryFactMatches()
    {
        Definition(null).Matches(Fact("anything")).Should().BeTrue();
        Definition(new AchievementMatch()).Matches(Fact("anything")).Should().BeTrue();
        Definition(new AchievementMatch()).Matches(Fact("")).Should().BeTrue();
    }

    [Fact]
    public void AValueListMatchesOnlyItsExactValues()
    {
        var definition = Definition(new AchievementMatch { Values = ["10", "20"] });

        definition.Matches(Fact("10")).Should().BeTrue();
        definition.Matches(Fact("20")).Should().BeTrue();
        definition.Matches(Fact("30")).Should().BeFalse();
        definition.Matches(Fact("1")).Should().BeFalse();
        definition.Matches(Fact("")).Should().BeFalse();
    }

    [Fact]
    public void AValueListIsCaseSensitive() =>
        Definition(new AchievementMatch { Values = ["Lobby"] })
            .Matches(Fact("lobby"))
            .Should()
            .BeFalse();

    [Fact]
    public void ACountedValueIsTheFactsOwnUnlessTheDefinitionAsksForTheUtcDate()
    {
        Definition(null).CountedValue(Fact("room-7")).Should().Be("room-7");
        Definition(new AchievementMatch { ValueFrom = AchievementValueSource.Fact })
            .CountedValue(Fact("room-7"))
            .Should()
            .Be("room-7");
    }

    [Fact]
    public void TheUtcDateChangesAtMidnightUtcExactly()
    {
        var definition = Definition(
            new AchievementMatch { ValueFrom = AchievementValueSource.UtcDate }
        );
        var lastTick = new DateTime(2026, 12, 25, 0, 0, 0, DateTimeKind.Utc)
            .AddDays(1)
            .AddTicks(-1);
        var midnight = new DateTime(2026, 12, 26, 0, 0, 0, DateTimeKind.Utc);

        definition.CountedValue(Fact("", lastTick)).Should().Be("2026-12-25");
        definition.CountedValue(Fact("", midnight)).Should().Be("2026-12-26");
    }

    [Fact]
    public void ASourceAllowsItsPrimaryReducerAndOnlyTheOnesItListed()
    {
        var source = new AchievementSourceDefinition(
            "test.source",
            1,
            AchievementReducer.CalendarStreak,
            [AchievementReducer.Counter]
        );

        source.Allows(AchievementReducer.CalendarStreak).Should().BeTrue();
        source.Allows(AchievementReducer.Counter).Should().BeTrue();
        source.Allows(AchievementReducer.Distinct).Should().BeFalse();
        new AchievementSourceDefinition("plain", 1, AchievementReducer.Counter)
            .Allows(AchievementReducer.Maximum)
            .Should()
            .BeFalse();
    }
}
