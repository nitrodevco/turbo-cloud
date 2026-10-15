using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Turbo.Achievements.Configuration;

/// <summary>
/// Habbo's Introduction track ("Start your Habbo Journey!") as Habbo sent it to a new account on
/// 2026-10-12 (<c>RewardTracks</c>, hhus): its 30 tasks, points, premium and 49 prizes as they are -
/// duckets, diamonds, badges, furni (named by sprite, found in the hotel's furniture), 30
/// Habbicons (found in the hotel's Habbicon collections), 2 pets (of the hotel's pet palettes)
/// and a bot.
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
