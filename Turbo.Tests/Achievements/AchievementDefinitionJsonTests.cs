using System.Text.Json;
using FluentAssertions;
using Turbo.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class AchievementDefinitionJsonTests
{
    private static AchievementDefinition Definition(AchievementState state) =>
        new()
        {
            Id = 100200,
            Key = "json-state",
            Revision = 1,
            Category = "identity",
            Source = AchievementSources.FIGURE,
            Reducer = AchievementReducer.Counter,
            State = state,
            Levels = [new() { Requirement = 1, BadgeCode = "ACH_JsonState1" }],
        };

    [Theory]
    [InlineData(AchievementState.Disabled)]
    [InlineData(AchievementState.Enabled)]
    [InlineData(AchievementState.Archived)]
    [InlineData(AchievementState.OffSeason)]
    public void EveryStateSurvivesAStoreAndRead(AchievementState state) =>
        AchievementDefinitionJson
            .Read(JsonSerializer.Serialize(Definition(state)))
            .State.Should()
            .Be(state);

    [Fact]
    public void ADefinitionWithNeitherStateNorFlagsIsEnabled()
    {
        var json = LegacyJson(null, null);

        AchievementDefinitionJson.Read(json).State.Should().Be(AchievementState.Enabled);
    }

    [Theory]
    [InlineData(true, false, AchievementState.Enabled)]
    [InlineData(false, false, AchievementState.Disabled)]
    [InlineData(false, true, AchievementState.Archived)]
    [InlineData(true, true, AchievementState.Archived)]
    public void LegacyFlagsMapToTheStateTheyMeant(
        bool enabled,
        bool archived,
        AchievementState expected
    ) => AchievementDefinitionJson.Read(LegacyJson(enabled, archived)).State.Should().Be(expected);

    [Fact]
    public void ALegacyDisabledFlagIsNeverReadAsEnabled() =>
        AchievementDefinitionJson
            .Read(LegacyJson(false, null))
            .State.Should()
            .Be(AchievementState.Disabled);

    [Fact]
    public void ExplicitStateWinsOverLegacyFlags()
    {
        var json = LegacyJson(true, false)
            .Replace("{", "{\"State\":\"OffSeason\",", StringComparison.Ordinal);

        AchievementDefinitionJson.Read(json).State.Should().Be(AchievementState.OffSeason);
    }

    [Fact]
    public void AnImportedArrayIsUpgradedPerDefinition()
    {
        var json =
            $"[{LegacyJson(false, true)},{JsonSerializer.Serialize(Definition(AchievementState.OffSeason))}]";

        AchievementDefinitionJson
            .ReadAll(json)
            .Select(x => x.State)
            .Should()
            .Equal(AchievementState.Archived, AchievementState.OffSeason);
    }

    [Fact]
    public void AWindowReadFromJsonKeepsItsUtcTimes()
    {
        var json = JsonSerializer
            .Serialize(Definition(AchievementState.Enabled))
            .Replace(
                "\"ActiveFromUtc\":null",
                "\"ActiveFromUtc\":\"2026-12-25T00:00:00Z\"",
                StringComparison.Ordinal
            )
            .Replace(
                "\"ActiveUntilUtc\":null",
                "\"ActiveUntilUtc\":\"2027-01-06T00:00:00Z\"",
                StringComparison.Ordinal
            );

        var read = AchievementDefinitionJson.Read(json);

        read.ActiveFromUtc.Should().Be(new DateTime(2026, 12, 25, 0, 0, 0, DateTimeKind.Utc));
        read.ActiveFromUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        read.ActiveUntilUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    private static string LegacyJson(bool? enabled, bool? archived)
    {
        var json = JsonSerializer.Serialize(Definition(AchievementState.Enabled));
        json = json.Replace("\"State\":\"Enabled\",", "", StringComparison.Ordinal)
            .Replace(",\"State\":\"Enabled\"", "", StringComparison.Ordinal);
        var flags =
            (enabled is { } e ? $"\"Enabled\":{(e ? "true" : "false")}," : "")
            + (archived is { } a ? $"\"Archived\":{(a ? "true" : "false")}," : "");

        return json.Replace("{", "{" + flags, StringComparison.Ordinal)
            .Replace(",}", "}", StringComparison.Ordinal);
    }
}
