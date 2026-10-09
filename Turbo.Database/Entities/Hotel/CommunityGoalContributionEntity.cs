using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Hotel;

/// <summary>
/// What one player has given a community goal: to each side (a goal of one side has only the
/// first), and the side they voted for. <see cref="Score"/> is both together, kept for ranking.
/// </summary>
[Table("community_goal_contributions")]
[Index(nameof(GoalEntityId), nameof(PlayerEntityId), IsUnique = true)]
[Index(nameof(GoalEntityId), nameof(Score))]
public class CommunityGoalContributionEntity : TurboEntity
{
    [Column("goal_id")]
    public int GoalEntityId { get; set; }

    [Column("player_id")]
    public int PlayerEntityId { get; set; }

    [Column("side_one")]
    public int SideOne { get; set; }

    [Column("side_two")]
    public int SideTwo { get; set; }

    [Column("score")]
    public int Score { get; set; }

    /// <summary>1 or 2; null for a player who has not voted.</summary>
    [Column("voted_side")]
    public int? VotedSide { get; set; }

    [ForeignKey(nameof(GoalEntityId))]
    public CommunityGoalEntity? GoalEntity { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
