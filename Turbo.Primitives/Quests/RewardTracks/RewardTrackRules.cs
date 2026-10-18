using System;
using System.Collections.Generic;
using System.Linq;

namespace Turbo.Primitives.Quests.RewardTracks;

/// <summary>What makes a set of reward tracks usable, as the AS3 RewardTrack classes read them.</summary>
public static class RewardTrackRules
{
    public const int ID_MAX_LENGTH = 64;

    /// <summary>What is wrong with the tracks, or null when they can be used.</summary>
    public static string? Problem(IReadOnlyList<RewardTrackDefinition> tracks)
    {
        if (tracks.Select(x => x.Id).Distinct().Count() != tracks.Count)
            return "track ids must be unique";

        foreach (var track in tracks)
        {
            if (string.IsNullOrWhiteSpace(track.Id) || track.Id.Length > ID_MAX_LENGTH)
                return $"a track has no id, or one over {ID_MAX_LENGTH} characters";

            if (track.StartsAt is { } start && track.EndsAt is { } end && end <= start)
                return $"track {track.Id}: it must end after it starts";

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
                    string.IsNullOrWhiteSpace(task.Id)
                    || string.IsNullOrWhiteSpace(task.ActionType)
                    || task.Id.Length > ID_MAX_LENGTH
                    || task.ActionType.Length > ID_MAX_LENGTH
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
                    || prize.Id.Length > ID_MAX_LENGTH
                    || prize.RequiredPoints < 0
                    || prize.Amount < 1
                )
                    return $"track {track.Id}: prize {prize.Id} needs an id, points not below 0 and an amount of at least 1";
        }

        return null;
    }

    /// <summary>Throws <see cref="ArgumentException"/> when <see cref="Problem"/> finds one.</summary>
    public static void Check(IReadOnlyList<RewardTrackDefinition> tracks)
    {
        if (Problem(tracks) is { } problem)
            throw new ArgumentException(problem, nameof(tracks));
    }
}
