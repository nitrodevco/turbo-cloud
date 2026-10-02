using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Turbo.Database.Entities.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;

namespace Turbo.Achievements;

/// <summary>Pure progression over admitted facts. Elapsed ranges are merged across sessions to avoid double credit.</summary>
public static class AchievementReducerEngine
{
    public static void Apply(
        AchievementProgressEntity progress,
        AchievementDefinition definition,
        AchievementFact fact,
        int maxDistinctValues
    )
    {
        switch (definition.Reducer)
        {
            case AchievementReducer.Counter:
                progress.Value = checked(progress.Value + fact.Amount);
                break;
            case AchievementReducer.Maximum:
                progress.Value = Math.Max(progress.Value, fact.Amount);
                break;
            case AchievementReducer.Distinct:
                throw new InvalidOperationException(
                    "Distinct facts are applied against the value table by ApplyDistinct."
                );
            case AchievementReducer.CalendarStreak:
                var day = fact.OccurredAtUtc.Date;
                if (progress.LastDayUtc is { } last && day <= last)
                    break;
                progress.Streak =
                    progress.LastDayUtc == day.AddDays(-1) ? checked(progress.Streak + 1) : 1;
                progress.LastDayUtc = day;
                progress.Value = Math.Max(progress.Value, progress.Streak);
                break;
            case AchievementReducer.ElapsedSeconds:
                if (
                    fact.IntervalStartUtc is not { Kind: DateTimeKind.Utc } start
                    || fact.IntervalEndUtc is not { Kind: DateTimeKind.Utc } end
                    || start > end
                    || end > fact.OccurredAtUtc
                    || string.IsNullOrWhiteSpace(fact.SessionId)
                )
                    throw new InvalidOperationException(
                        "Elapsed facts require valid durable interval and session identities."
                    );
                var intervals =
                    JsonSerializer.Deserialize<List<long[]>>(progress.IntervalsJson) ?? [];
                intervals.Add([start.Ticks, end.Ticks]);
                var merged = new List<long[]>();
                foreach (var interval in intervals.OrderBy(x => x[0]))
                {
                    if (merged.Count == 0 || merged[^1][1] < interval[0])
                        merged.Add(interval);
                    else
                        merged[^1][1] = Math.Max(merged[^1][1], interval[1]);
                }
                if (merged.Count > maxDistinctValues)
                    throw new InvalidOperationException(
                        "Interval progression storage limit reached."
                    );
                progress.IntervalsJson = JsonSerializer.Serialize(merged);
                progress.Value = checked(
                    merged.Sum(x => x[1] - x[0]) / TimeSpan.TicksPerSecond
                    + progress.ForwardAdjustment
                );
                break;
            case AchievementReducer.Rank:
                if (fact.Amount > 0)
                    progress.Value =
                        progress.Value == 0 ? fact.Amount : Math.Min(progress.Value, fact.Amount);
                break;
            default:
                throw new InvalidOperationException("Unknown admitted reducer.");
        }
    }

    /// <summary>
    /// A distinct value. <paramref name="added"/> is whether the value was new to the player's
    /// value table; a repeat only refreshes the displayed total.
    /// </summary>
    public static void ApplyDistinct(
        AchievementProgressEntity progress,
        AchievementFact fact,
        bool added,
        int maxDistinctValues
    )
    {
        if (string.IsNullOrWhiteSpace(fact.Value))
            throw new InvalidOperationException("Distinct facts require a value.");
        if (added)
        {
            if (progress.DistinctCount >= maxDistinctValues)
                throw new InvalidOperationException("Distinct progression storage limit reached.");
            progress.DistinctCount++;
        }
        progress.Value = checked(progress.DistinctCount + progress.ForwardAdjustment);
    }

    public static bool Qualifies(
        AchievementDefinition definition,
        AchievementLevelDefinition level,
        long rawValue
    ) =>
        definition.Reducer == AchievementReducer.Rank
            ? rawValue > 0 && rawValue <= level.Requirement
            : rawValue / definition.UnitDivisor >= level.Requirement;
}
