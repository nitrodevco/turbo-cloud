using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Achievements;

/// <summary>
/// Everything about one player's one achievement: reducer state, the levels earned, and the levels
/// still being delivered or announced. Levels deliver strictly in order, so a single cursor and a
/// short list of open awards replace a row per level.
/// </summary>
[Table("achievement_progress")]
[PrimaryKey(nameof(PlayerId), nameof(AchievementId))]
// Recovery looks for players with an undelivered award across every player.
[Index(nameof(PendingDelivery), nameof(PlayerId))]
public sealed class AchievementProgressEntity
{
    public int PlayerId { get; set; }
    public int AchievementId { get; set; }
    public long Value { get; set; }
    public long ForwardAdjustment { get; set; }
    public long Streak { get; set; }
    public DateTime? LastDayUtc { get; set; }

    /// <summary>The highest level the progress qualifies for. Each level above <see cref="CompletedLevel"/> is an open award.</summary>
    public int EarnedLevel { get; set; }

    /// <summary>The highest level whose rewards, badge and score are all delivered.</summary>
    public int CompletedLevel { get; set; }

    /// <summary>The frozen scores of every completed level, so a player's total is a sum of these rows.</summary>
    public int ScoreEarned { get; set; }

    /// <summary>When the last level was completed, for support questions. History is not kept per level.</summary>
    public DateTime? LastLevelAtUtc { get; set; }

    /// <summary>Distinct values counted so far; the values live in <c>achievement_distinct_values</c>.</summary>
    public int DistinctCount { get; set; }

    [Column(TypeName = "longtext")]
    [MaxLength(int.MaxValue)]
    public string IntervalsJson { get; set; } = "[]";

    /// <summary>Earned levels not yet finished, as JSON. Read and written through <c>AchievementOpenAwards</c>.</summary>
    [Column(TypeName = "longtext")]
    [MaxLength(int.MaxValue)]
    public string OpenAwards { get; set; } = "[]";

    /// <summary>An open award still has rewards, a badge or score to deliver. Kept in step by <c>AchievementOpenAwards</c>.</summary>
    public bool PendingDelivery { get; set; }
}
