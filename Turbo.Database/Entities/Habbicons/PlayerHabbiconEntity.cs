using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Habbicons.Enums;

namespace Turbo.Database.Entities.Habbicons;

/// <summary>
/// A Habbicon a player has: claimable, owned or favourited. The Habbicons themselves are
/// configuration, so <see cref="HabbiconId"/> names one there.
/// </summary>
[Table("player_habbicons")]
[Index(nameof(PlayerEntityId), nameof(HabbiconId), IsUnique = true)]
public class PlayerHabbiconEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("habbicon_id")]
    public required int HabbiconId { get; set; }

    [Column("state")]
    public required HabbiconState State { get; set; }

    /// <summary>When the player last showed or sent it; null when never. The newest are the recent ones.</summary>
    [Column("last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }
}
