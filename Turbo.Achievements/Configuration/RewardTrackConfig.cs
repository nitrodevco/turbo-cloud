using System.Collections.Generic;
using System.Linq;

namespace Turbo.Achievements.Configuration;

/// <summary>
/// The hotel's reward tracks. Habbo's content (the Introduction track's 30 tasks and 49 prizes)
/// is not shown in full anywhere, so the server ships with no track; a hotel lists its own here.
/// </summary>
public sealed class RewardTrackConfig
{
    public const string SECTION_NAME = "Turbo:RewardTracks";

    /// <summary>When off, the client is told reward tracks are disabled and claims are refused.</summary>
    public bool Enabled { get; set; } = true;

    public List<RewardTrackDefinition> Tracks { get; set; } = [];

    /// <summary>What is wrong with the tracks, or null when they can be used.</summary>
    public string? Problem()
    {
        if (Tracks.Select(x => x.Id).Distinct().Count() != Tracks.Count)
            return "track ids must be unique";

        foreach (var track in Tracks)
        {
            if (string.IsNullOrWhiteSpace(track.Id))
                return "a track has no id";

            if (track.Tasks.Select(x => x.Id).Distinct().Count() != track.Tasks.Count)
                return $"track {track.Id}: task ids must be unique";

            if (track.Prizes.Select(x => x.Id).Distinct().Count() != track.Prizes.Count)
                return $"track {track.Id}: prize ids must be unique";

            if (
                track.Premium is { } premium
                && (
                    premium.TaskPointsBoost < 1
                    || premium.InstantPoints < 0
                    || premium.CostCredits < 0
                    || premium.CostDiamonds < 0
                )
            )
                return $"track {track.Id}: premium boost must be at least 1 and its points and costs not negative";

            foreach (var task in track.Tasks)
            {
                if (
                    string.IsNullOrWhiteSpace(task.Id) || string.IsNullOrWhiteSpace(task.ActionType)
                )
                    return $"track {track.Id}: a task has no id or action type";

                if (
                    task.Levels.Count == 0
                    || task.Levels.Any(x => x.RequiredCount < 1 || x.Points < 0)
                    || task.Levels.Zip(task.Levels.Skip(1))
                        .Any(x => x.First.RequiredCount >= x.Second.RequiredCount)
                )
                    return $"track {track.Id} task {task.Id}: levels must count up from 1 and give no negative points";
            }

            foreach (var prize in track.Prizes)
                if (
                    string.IsNullOrWhiteSpace(prize.Id)
                    || prize.RequiredPoints < 0
                    || prize.Amount < 1
                )
                    return $"track {track.Id}: prize {prize.Id} needs an id, points not below 0 and an amount of at least 1";
        }

        return null;
    }
}
