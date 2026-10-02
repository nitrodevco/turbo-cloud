using FluentAssertions;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class AchievementWindowTests
{
    private static readonly DateTime From = new(2026, 12, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Until = new(2027, 1, 6, 0, 0, 0, DateTimeKind.Utc);

    private static AchievementDefinition Definition(AchievementState state) =>
        new()
        {
            Id = 100300,
            Key = "window",
            Revision = 1,
            Category = "christmas",
            Source = AchievementSources.LOGIN,
            Reducer = AchievementReducer.CalendarStreak,
            State = state,
            ActiveFromUtc = From,
            ActiveUntilUtc = Until,
            Levels = [new() { Requirement = 1, BadgeCode = "ACH_Window1" }],
        };

    [Fact]
    public void BeforeTheWindowItIsHiddenAndAccruesNothing()
    {
        var definition = Definition(AchievementState.Enabled);
        var justBefore = From.AddTicks(-1);

        definition.EffectiveState(justBefore).Should().Be(AchievementState.Disabled);
        definition.Accrues(justBefore).Should().BeFalse();
        definition.IsKnownToClient(justBefore).Should().BeFalse();
        definition.IsListedFor(justBefore, showArchived: true).Should().BeFalse();
    }

    [Fact]
    public void TheWindowStartsAtItsFirstInstantAndEndsBeforeItsLast()
    {
        var definition = Definition(AchievementState.Enabled);

        definition.EffectiveState(From).Should().Be(AchievementState.Enabled);
        definition.EffectiveState(Until.AddTicks(-1)).Should().Be(AchievementState.Enabled);
        definition.Accrues(From).Should().BeTrue();
        definition.Accrues(Until.AddTicks(-1)).Should().BeTrue();
    }

    [Fact]
    public void AfterTheWindowItIsArchivedSoOnlyThoseWhoProgressedItSeeIt()
    {
        var definition = Definition(AchievementState.Enabled);

        definition.EffectiveState(Until).Should().Be(AchievementState.Archived);
        definition.Accrues(Until).Should().BeFalse();
        definition.IsKnownToClient(Until).Should().BeTrue();
        definition.IsListedFor(Until, showArchived: false).Should().BeFalse();
        definition.IsListedFor(Until, showArchived: true).Should().BeTrue();
    }

    [Theory]
    [InlineData(AchievementState.Disabled)]
    [InlineData(AchievementState.Archived)]
    [InlineData(AchievementState.OffSeason)]
    public void AWindowOnlyEverAffectsAnEnabledAchievement(AchievementState state)
    {
        var definition = Definition(state);

        definition.EffectiveState(From.AddDays(-30)).Should().Be(state);
        definition.EffectiveState(From.AddDays(1)).Should().Be(state);
        definition.EffectiveState(Until.AddDays(30)).Should().Be(state);
    }

    [Fact]
    public void AHalfOpenWindowNeedsNoBothEnds()
    {
        var openEnded = Definition(AchievementState.Enabled) with { ActiveUntilUtc = null };
        var openStart = Definition(AchievementState.Enabled) with { ActiveFromUtc = null };

        openEnded.Accrues(Until.AddYears(10)).Should().BeTrue();
        openEnded.Accrues(From.AddTicks(-1)).Should().BeFalse();
        openStart.Accrues(From.AddYears(-10)).Should().BeTrue();
        openStart.Accrues(Until).Should().BeFalse();
    }
}
