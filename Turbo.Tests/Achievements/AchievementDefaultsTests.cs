using System.Text.Json;
using Turbo.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;
using Xunit;

namespace Turbo.Tests.Achievements;

public class AchievementDefaultsTests
{
    [Fact]
    public void Defaults_ContainAllRequestedFamiliesWithIncreasingBadgeLevels()
    {
        var expectedPrefixes = new Dictionary<string, string>
        {
            ["online"] = "ACH_AllTimeHotelPresence",
            ["login"] = "ACH_Login",
            ["account-age"] = "ACH_RegistrationDuration",
            ["figure"] = "ACH_AvatarLooks",
            ["motto"] = "ACH_Motto",
            ["hc-duration"] = "ACH_VipHC",
            ["purchased-hc"] = "ACH_HC",
            ["rooms-visited"] = "ACH_RoomEntry",
            ["furniture-use"] = "ACH_HabboExplorer",
            ["respect-given"] = "ACH_RespectGiven",
            ["respect-received"] = "ACH_RespectEarned",
            ["pets-owned"] = "ACH_PetLover",
            ["pet-nutrition"] = "ACH_PetFeeding",
            ["pet-levels"] = "ACH_PetLevelUp",
            ["pet-respect-given"] = "ACH_PetRespectGiver",
            ["pet-respect-received"] = "ACH_PetRespectReceiver",
            ["floor-heights"] = "ACH_HabboBuilder",
            ["room-rank"] = "ACH_RoomRank",
        };
        var definitions = AchievementDefaults.Definitions;

        Assert.Equal(18, definitions.Length);
        Assert.Equal(expectedPrefixes.Keys.Order(), definitions.Select(x => x.Key).Order());
        Assert.Equal(18, AchievementDefaults.Sources.Length);

        foreach (var definition in definitions)
        {
            var prefix = expectedPrefixes[definition.Key];
            Assert.NotEmpty(definition.Levels);
            Assert.All(
                definition.Levels.Select((level, index) => (level, index)),
                item => Assert.Equal($"{prefix}{item.index + 1}", item.level.BadgeCode)
            );

            var requirements = definition.Levels.Select(x => x.Requirement).ToArray();
            if (definition.Reducer == AchievementReducer.Rank)
            {
                Assert.Equal(requirements.OrderByDescending(x => x), requirements);
            }
            else
            {
                Assert.Equal(requirements.OrderBy(x => x), requirements);
            }
        }
    }

    [Fact]
    public void PublishedFamiliesMatchThePublicSnapshotExactly()
    {
        using var stream = typeof(AchievementDefaults).Assembly.GetManifestResourceStream(
            "Turbo.Achievements.Resources.habbo-achievements-2026-10-02.json"
        );
        Assert.NotNull(stream);
        using var snapshot = JsonDocument.Parse(stream);
        Assert.Equal(167, snapshot.RootElement.GetArrayLength());
        var published = snapshot
            .RootElement.EnumerateArray()
            .ToDictionary(x => x.GetProperty("achievement").GetProperty("name").GetString()!);
        var matched = AchievementDefaults.Definitions.Where(x =>
            x.Key is not ("motto" or "floor-heights" or "room-rank")
        );
        Assert.Equal(15, matched.Count());
        foreach (var definition in matched)
        {
            var badge = definition.Levels[0].BadgeCode;
            var source = published[badge[4..^1]];
            var metadata = source.GetProperty("achievement");
            Assert.Equal(metadata.GetProperty("category").GetString(), definition.Category);
            Assert.Equal(
                metadata.GetProperty("state").GetString() switch
                {
                    "ENABLED" => AchievementState.Enabled,
                    "ARCHIVED" => AchievementState.Archived,
                    "OFF_SEASON" => AchievementState.OffSeason,
                    _ => AchievementState.Disabled,
                },
                definition.State
            );
            Assert.Equal(
                source
                    .GetProperty("levelRequirements")
                    .EnumerateArray()
                    .Select(x => x.GetProperty("requiredScore").GetInt32()),
                definition.Levels.Select(x => x.Requirement)
            );
            Assert.All(
                definition.Levels,
                x =>
                {
                    Assert.Equal(10, x.Score);
                    Assert.Empty(x.Rewards);
                }
            );
        }
        Assert.Equal(
            "ARCHIVED",
            published["BasicClub"].GetProperty("achievement").GetProperty("state").GetString()
        );
        Assert.DoesNotContain(
            AchievementDefaults.Definitions,
            x =>
                x.Accrues(DateTime.UtcNow)
                && x.Levels[0].BadgeCode.StartsWith("ACH_BasicClub", StringComparison.Ordinal)
        );
        var membership = AchievementDefaults.Definitions.Single(x => x.Key == "hc-duration");
        Assert.Equal(0, membership.Levels[0].Requirement);
        Assert.Equal(31 * 86400, membership.UnitDivisor);
    }
}
