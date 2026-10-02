using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Moderation.Enums;

namespace Turbo.Database.Entities.Moderation;

/// <summary>
/// A hotel-wide sanction on a player. Only bans live here: a silence and a trade lock are
/// temporary denials in the permission tables, which already expire and audit themselves. The
/// ids are plain columns, not foreign keys, so the record of a sanction outlives the player row
/// and the staff member who issued it. A lifted ban is kept and stamped, never deleted.
/// </summary>
[Table("player_sanctions")]
[Index(nameof(PlayerEntityId), nameof(Kind))]
public class PlayerSanctionEntity : TurboEntity
{
    public const int REASON_MAX_LENGTH = 255;

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("kind")]
    public required SanctionKind Kind { get; set; }

    [Column("reason")]
    [StringLength(REASON_MAX_LENGTH)]
    public required string Reason { get; set; }

    /// <summary>The staff member who issued it; null for the console.</summary>
    [Column("issuer_id")]
    public int? IssuerEntityId { get; set; }

    /// <summary>When it ends; null when it does not.</summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    [Column("revoked_by")]
    public int? RevokedByEntityId { get; set; }
}
