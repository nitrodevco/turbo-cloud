using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;

namespace Turbo.Achievements;

/// <summary>Published Habbo requirements with explicit hotel extensions and reward policy.</summary>
public static class AchievementDefaults
{
    public const string SOURCE_URL = "https://www.habbo.com/api/public/achievements";

    // Pin the public snapshot: startup and reload must not depend on a remote API.
    // Creation times in the snapshot describe definitions, never activation cutoffs.
    private static readonly ImmutableDictionary<string, JsonElement> PublishedCatalog =
        LoadPublishedCatalog();

    public static ImmutableArray<AchievementDefinition> Definitions { get; } =
    [
        Published(
            1001,
            "online",
            AchievementSources.ONLINE,
            AchievementReducer.ElapsedSeconds,
            60,
            "AllTimeHotelPresence"
        ),
        Published(
            1002,
            "login",
            AchievementSources.LOGIN,
            AchievementReducer.CalendarStreak,
            1,
            "Login"
        ),
        Published(
            1003,
            "account-age",
            AchievementSources.ACCOUNT_AGE,
            AchievementReducer.Maximum,
            1,
            "RegistrationDuration"
        ),
        Published(
            1004,
            "figure",
            AchievementSources.FIGURE,
            AchievementReducer.Counter,
            1,
            "AvatarLooks"
        ),
        Create(
            1005,
            "motto",
            "identity",
            AchievementSources.MOTTO,
            AchievementReducer.Counter,
            1,
            [1],
            "ACH_Motto",
            true,
            0
        ),
        Published(
            1006,
            "hc-duration",
            AchievementSources.HC,
            AchievementReducer.Maximum,
            2678400,
            "VipHC"
        ),
        Published(
            1007,
            "purchased-hc",
            AchievementSources.PURCHASED_HC,
            AchievementReducer.Maximum,
            1,
            "HC"
        ),
        Published(
            1008,
            "rooms-visited",
            AchievementSources.VISIT,
            AchievementReducer.Distinct,
            1,
            "RoomEntry"
        ),
        Published(
            1009,
            "furniture-use",
            AchievementSources.FURNITURE,
            AchievementReducer.Counter,
            1,
            "HabboExplorer"
        ),
        Published(
            1010,
            "respect-given",
            AchievementSources.RESPECT_GIVEN,
            AchievementReducer.Counter,
            1,
            "RespectGiven"
        ),
        Published(
            1011,
            "respect-received",
            AchievementSources.RESPECT_RECEIVED,
            AchievementReducer.Counter,
            1,
            "RespectEarned"
        ),
        Published(
            1012,
            "pets-owned",
            AchievementSources.PETS,
            AchievementReducer.Maximum,
            1,
            "PetLover"
        ),
        Published(
            1013,
            "pet-nutrition",
            AchievementSources.NUTRITION,
            AchievementReducer.Counter,
            1,
            "PetFeeding"
        ),
        Published(
            1014,
            "pet-levels",
            AchievementSources.PET_LEVEL,
            AchievementReducer.Counter,
            1,
            "PetLevelUp"
        ),
        Published(
            1015,
            "pet-respect-given",
            AchievementSources.PET_RESPECT_GIVEN,
            AchievementReducer.Counter,
            1,
            "PetRespectGiver"
        ),
        Published(
            1016,
            "pet-respect-received",
            AchievementSources.PET_RESPECT_RECEIVED,
            AchievementReducer.Counter,
            1,
            "PetRespectReceiver"
        ),
        Create(
            1017,
            "floor-heights",
            "room_builder",
            AchievementSources.FLOOR_HEIGHTS,
            AchievementReducer.Maximum,
            1,
            [2, 3, 4, 5, 6],
            "ACH_HabboBuilder",
            false,
            0
        ),
        Create(
            1018,
            "room-rank",
            "room_builder",
            AchievementSources.ROOM_RANK,
            AchievementReducer.Rank,
            1,
            [2000, 1000, 500, 250, 100, 50, 10, 5, 1],
            "ACH_RoomRank",
            true,
            1
        ),
    ];

    /// <summary>
    /// The first id of the published records the hotel has no hook for. Each is the API's own id
    /// added to this, so an id never changes when the snapshot is refreshed.
    /// </summary>
    public const int UNHOOKED_ID_START = 10000;

    /// <summary>
    /// Every other published record. Nothing in the hotel records the facts most of them need, so
    /// they ship disabled (retired ones archived) on <see cref="AchievementSources.UNHOOKED"/> with
    /// their real category and thresholds: a hotel can keep, edit, retire or move any of them to a
    /// source something records, and a plugin or a later release can hook one up.
    /// </summary>
    public static ImmutableArray<AchievementDefinition> Unhooked { get; } = BuildUnhooked();

    /// <summary>
    /// Published records that cannot be shipped faithfully, and why. They are listed in the
    /// coverage CSV instead.
    /// </summary>
    public static ImmutableArray<(string Name, string Reason)> NotRepresentable { get; } =
        PublishedCatalog
            .Select(x => (Name: x.Key, Reason: WhyNotRepresentable(x.Value)))
            .Where(x => x.Reason is not null)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => (x.Name, x.Reason!))
            .ToImmutableArray();

    private static ImmutableArray<AchievementDefinition> BuildUnhooked()
    {
        var mapped = Definitions
            .Select(x => AchievementBadgeCodes.BaseOf(x.Levels[0].BadgeCode))
            .Where(x => x.StartsWith("ACH_", StringComparison.Ordinal))
            .Select(x => x["ACH_".Length..])
            .ToHashSet(StringComparer.Ordinal);

        return
        [
            .. PublishedCatalog
                .Where(x => !mapped.Contains(x.Key) && WhyNotRepresentable(x.Value) is null)
                .OrderBy(x => x.Value.GetProperty("achievement").GetProperty("id").GetInt32())
                .Select(x => Unhook(x.Key, x.Value)),
        ];
    }

    private static string? WhyNotRepresentable(JsonElement published)
    {
        if (
            !published.TryGetProperty("levelRequirements", out var levels)
            || levels.GetArrayLength() == 0
        )
            return "The record has no levels.";
        var scores = levels
            .EnumerateArray()
            .Select(x => x.GetProperty("requiredScore").GetInt32())
            .ToArray();
        for (var i = 1; i < scores.Length; i++)
            if (scores[i] <= scores[i - 1])
                return "Its thresholds do not strictly rise.";

        return null;
    }

    private static AchievementDefinition Unhook(string name, JsonElement published)
    {
        var achievement = published.GetProperty("achievement");
        var id = UNHOOKED_ID_START + achievement.GetProperty("id").GetInt32();
        // A name that ends in a digit would run into the level number (bazaar17 level 1 would
        // read as level 171), so those take an underscore before it.
        var badgePrefix = "ACH_" + name + (char.IsAsciiDigit(name[^1]) ? "_" : "");

        return Create(
            id,
            KeyOf(name),
            achievement.GetProperty("category").GetString()!,
            AchievementSources.UNHOOKED,
            AchievementReducer.Counter,
            1,
            published
                .GetProperty("levelRequirements")
                .EnumerateArray()
                .Select(x => x.GetProperty("requiredScore").GetInt32())
                .ToArray(),
            badgePrefix,
            false,
            0
        ) with
        {
            Revision = 1,
            State =
                achievement.GetProperty("state").GetString() == "ARCHIVED"
                    ? AchievementState.Archived
                    : AchievementState.Disabled,
        };
    }

    /// <summary><c>RoomEntry</c> becomes <c>room-entry</c>.</summary>
    private static string KeyOf(string name) =>
        Regex
            .Replace(Regex.Replace(name, "([a-z0-9])([A-Z])", "$1-$2"), "[^A-Za-z0-9]+", "-")
            .Trim('-')
            .ToLowerInvariant();

    private static ImmutableDictionary<string, JsonElement> LoadPublishedCatalog()
    {
        using var stream =
            typeof(AchievementDefaults).Assembly.GetManifestResourceStream(
                "Turbo.Achievements.Resources.habbo-achievements-2026-10-02.json"
            )
            ?? throw new InvalidOperationException(
                "The published achievement snapshot is missing."
            );
        return (
            JsonSerializer.Deserialize<JsonElement[]>(stream)
            ?? throw new InvalidOperationException("The published achievement snapshot is invalid.")
        ).ToImmutableDictionary(
            x => x.GetProperty("achievement").GetProperty("name").GetString()!,
            StringComparer.Ordinal
        );
    }

    private static AchievementDefinition Published(
        int id,
        string key,
        string source,
        AchievementReducer reducer,
        int divisor,
        string publishedName
    )
    {
        var published = PublishedCatalog[publishedName];
        var achievement = published.GetProperty("achievement");
        var state = achievement.GetProperty("state").GetString();
        var requirements = published.GetProperty("levelRequirements").EnumerateArray().ToArray();
        if (
            !requirements
                .Select((x, index) => x.GetProperty("level").GetInt32() == index + 1)
                .All(x => x)
        )
            throw new InvalidOperationException("Published achievement levels must be contiguous.");
        return Create(
            id,
            key,
            achievement.GetProperty("category").GetString()!,
            source,
            reducer,
            divisor,
            requirements.Select(x => x.GetProperty("requiredScore").GetInt32()).ToArray(),
            "ACH_" + publishedName,
            true,
            0
        ) with
        {
            State = state switch
            {
                "ENABLED" => AchievementState.Enabled,
                "ARCHIVED" => AchievementState.Archived,
                "OFF_SEASON" => AchievementState.OffSeason,
                _ => AchievementState.Disabled,
            },
        };
    }

    private static AchievementDefinition Create(
        int id,
        string key,
        string category,
        string source,
        AchievementReducer reducer,
        int divisor,
        int[] requirements,
        string badgePrefix,
        bool enabled,
        int displayMethod
    ) =>
        new()
        {
            Id = id,
            Key = key,
            Revision = 2,
            Category = category,
            Source = source,
            Reducer = reducer,
            UnitDivisor = divisor,
            Order = id,
            State = enabled ? AchievementState.Enabled : AchievementState.Disabled,
            DisplayMethod = displayMethod,
            Levels = requirements
                .Select(
                    (requirement, index) =>
                        new AchievementLevelDefinition
                        {
                            Requirement = requirement,
                            BadgeCode = badgePrefix + (index + 1),
                            Score = 10,
                        }
                )
                .ToImmutableArray(),
        };
}
