using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// One avatar effect a player owns: a row per effect id, however many copies. A row exists
/// while the player has a copy waiting (<see cref="InactiveCount"/>), one running
/// (<see cref="ExpiresAt"/>) or the effect for good (<see cref="IsPermanent"/>), and is deleted
/// when the last copy runs out. A permanent row holds no copies and no timer.
/// </summary>
[Table("player_effects")]
[Index(nameof(PlayerEntityId), nameof(EffectId), IsUnique = true)]
public class PlayerEffectEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    /// <summary>The client's effect type.</summary>
    [Column("effect_id")]
    public required int EffectId { get; set; }

    /// <summary>0 for an effect, 1 for a costume; the client reads it to open the right editor tab.</summary>
    [Column("sub_type")]
    [DefaultValue(0)]
    public int SubType { get; set; }

    /// <summary>Copies not yet activated.</summary>
    [Column("inactive_count")]
    [DefaultValue(0)]
    public int InactiveCount { get; set; }

    [Column("is_permanent")]
    [DefaultValue(false)]
    public bool IsPermanent { get; set; }

    /// <summary>
    /// When the running copy runs out (UTC); null when none is running. Absolute rather than a
    /// remaining time, so it keeps counting while the player is offline and across restarts.
    /// </summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }
}
