using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// What a respect did to one player's counters: a respect or pet respect spent by the giver, or a
/// respect received. Written in the same transaction as the change to the player, so a respect
/// that is retried finds it and never spends or credits that player twice.
/// </summary>
[Comment(
    "What a respect did to one player's counters, written with the change so it is never made twice."
)]
[Table("player_respect_changes")]
[PrimaryKey(nameof(RespectId), nameof(Kind))]
[Index(nameof(PlayerId))]
public sealed class PlayerRespectChangeEntity
{
    /// <summary>The <c>player_respects</c> or <c>pet_respects</c> row this change belongs to.</summary>
    [Column("respect_id")]
    public Guid RespectId { get; set; }

    [Column("kind")]
    public RespectChangeKind Kind { get; set; }

    [Column("player_id")]
    public int PlayerId { get; set; }

    /// <summary>Whether it was made; a spend is refused when none are left today.</summary>
    [Column("accepted")]
    public bool Accepted { get; set; }

    /// <summary>The counter after the change: respects left today, or the respect total received.</summary>
    [Column("total_after")]
    public int TotalAfter { get; set; }

    [Column("created_at")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }
}
