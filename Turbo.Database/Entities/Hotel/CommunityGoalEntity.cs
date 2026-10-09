using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Hotel.Enums;

namespace Turbo.Database.Entities.Hotel;

/// <summary>
/// A community goal: from <see cref="StartsAt"/> to <see cref="EndsAt"/> the hotel plays it
/// together, buying from its catalog pages (and, in a voting goal, voting for a side). The goal
/// shown is the last one started.
/// </summary>
[Table("community_goals")]
[Index(nameof(Code), IsUnique = true)]
[Index(nameof(StartsAt))]
public class CommunityGoalEntity : TurboEntity
{
    public const int CODE_MAX_LENGTH = 64;
    public const int LIST_MAX_LENGTH = 255;

    [Column("code")]
    [StringLength(CODE_MAX_LENGTH)]
    public required string Code { get; set; }

    [Column("mode")]
    public CommunityGoalMode Mode { get; set; }

    [Column("starts_at")]
    public DateTime StartsAt { get; set; }

    [Column("ends_at")]
    public DateTime EndsAt { get; set; }

    /// <summary>The scores its levels are reached at, rising, comma-separated.</summary>
    [Column("level_scores")]
    [StringLength(LIST_MAX_LENGTH)]
    public required string LevelScores { get; set; }

    /// <summary>The last rank of each prize band, rising, comma-separated.</summary>
    [Column("reward_ranks")]
    [StringLength(LIST_MAX_LENGTH)]
    public required string RewardRanks { get; set; }

    [Column("side_one_page_id")]
    public int? SideOnePageId { get; set; }

    [Column("side_two_page_id")]
    public int? SideTwoPageId { get; set; }
}
