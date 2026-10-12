using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Turbo.Achievements.Configuration;

/// <summary>
/// Habbo's Introduction track ("Start your Habbo Journey!") as Habbo sent it to a new account on
/// 2026-10-12 (<c>RewardTracks</c>, hhus): its 30 tasks, points and premium as they are, and the
/// 16 of its 49 prizes Turbo can give - duckets, diamonds, badges and furni (named by sprite, found
/// in the hotel's furniture). Its 30 Habbicons, 2 pets and a bot are left out.
/// </summary>
public static class HabboRewardTracks
{
    private const string INTRODUCTION_RESOURCE =
        "Turbo.Achievements.Resources.habbo-reward-track-introduction-2026-10-12.json";

    private static readonly JsonSerializerOptions JSON = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static RewardTrackDefinition Introduction()
    {
        using var stream =
            typeof(HabboRewardTracks).Assembly.GetManifestResourceStream(INTRODUCTION_RESOURCE)
            ?? throw new InvalidOperationException("Habbo's Introduction track is missing.");

        return JsonSerializer.Deserialize<RewardTrackDefinition>(stream, JSON)
            ?? throw new InvalidOperationException("Habbo's Introduction track is invalid.");
    }
}
