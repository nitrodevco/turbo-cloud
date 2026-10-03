using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using Turbo.Database.Entities.Achievements;

namespace Turbo.Database.Achievements;

/// <summary>
/// The only reader and writer of a progress row's open awards, so the JSON shape and the
/// <see cref="AchievementProgressEntity.PendingDelivery"/> flag that recovery polls cannot drift.
/// </summary>
public static class AchievementOpenAwards
{
    public const string NONE = "[]";

    /// <summary>The key a reward receipt is derived from. It is stable for a player, achievement and level.</summary>
    public static string AwardKey(int playerId, int achievementId, int level) =>
        $"achievement:{playerId}:{achievementId}:{level}";

    public static ImmutableArray<AchievementOpenAward> ReadOpenAwards(
        this AchievementProgressEntity progress
    ) =>
        progress.OpenAwards is NONE or ""
            ? []
            : JsonSerializer.Deserialize<ImmutableArray<AchievementOpenAward>>(progress.OpenAwards);

    public static void WriteOpenAwards(
        this AchievementProgressEntity progress,
        IEnumerable<AchievementOpenAward> awards
    )
    {
        var ordered = awards.OrderBy(x => x.Level).ToImmutableArray();
        progress.OpenAwards = ordered.IsEmpty ? NONE : JsonSerializer.Serialize(ordered);
        progress.PendingDelivery = ordered.Any(x => !x.Completed);
    }

    /// <summary>Replaces the open award for the same level.</summary>
    public static void ReplaceOpenAward(
        this AchievementProgressEntity progress,
        AchievementOpenAward award
    ) =>
        progress.WriteOpenAwards(
            progress.ReadOpenAwards().Where(x => x.Level != award.Level).Append(award)
        );

    public static void RemoveOpenAward(this AchievementProgressEntity progress, int level) =>
        progress.WriteOpenAwards(progress.ReadOpenAwards().Where(x => x.Level != level));
}
