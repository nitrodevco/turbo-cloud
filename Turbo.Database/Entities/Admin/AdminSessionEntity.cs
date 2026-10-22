using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Admin;

/// <summary>
/// A signed-in admin panel session, kept so a restart does not sign staff out. Holds the hash of
/// the session token, never the token: a copy of the table signs nobody in. Owned by the admin
/// auth grain, which deletes the row when the session ends.
/// </summary>
[Table("admin_sessions")]
[Index(nameof(TokenHash), IsUnique = true)]
[Index(nameof(PlayerEntityId))]
public class AdminSessionEntity : TurboEntity
{
    [Column("player_id")]
    public int PlayerEntityId { get; set; }

    /// <summary>SHA-256 of the session token, upper-case hex.</summary>
    [Column("token_hash")]
    [MaxLength(64)]
    public required string TokenHash { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
