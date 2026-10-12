using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Moderation.Enums;

namespace Turbo.Database.Entities.Moderation;

/// <summary>
/// One moderation action. <c>moderation_sanctions</c> holds what is in force (a ban), and the
/// permission grains what a player may do (a silence); this is the history of every action, from
/// every place it can be taken, read by who it was done to, who did it, or where.
/// </summary>
[Comment(
    "Every moderation action: who did what to which player or room, from where, why and until when."
)]
[Table("moderation_actions")]
// A player's history, a staff member's actions, a room's moderation, one kind of action: each
// read newest first and paged by id.
[Index(nameof(TargetPlayerId), nameof(Id))]
[Index(nameof(ActorId), nameof(Id))]
[Index(nameof(RoomId), nameof(Id))]
[Index(nameof(Action), nameof(Id))]
public class ModerationActionEntity : TurboLogEntity
{
    public const int REASON_MAX_LENGTH = 255;
    public const int DETAIL_MAX_LENGTH = 500;

    [Column("action")]
    public required ModerationActionType Action { get; set; }

    [Column("source")]
    public required ModerationSource Source { get; set; }

    /// <summary>The player who took the action; null for the console and the hotel itself.</summary>
    [Column("actor_id")]
    public int? ActorId { get; set; }

    [Column("target_player_id")]
    public int? TargetPlayerId { get; set; }

    [Column("room_id")]
    public int? RoomId { get; set; }

    [Column("guild_id")]
    public int? GuildId { get; set; }

    [Column("reason")]
    [StringLength(REASON_MAX_LENGTH)]
    public string? Reason { get; set; }

    /// <summary>When what the action does ends; null when it does not last, or lasts for good.</summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>What the action was about, where its kind has one: a badge code, a message, a count.</summary>
    [Column("detail")]
    [StringLength(DETAIL_MAX_LENGTH)]
    public string? Detail { get; set; }
}
