using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Security;

/// <summary>
/// A sign-in to the public site. The browser holds the token in a cookie; this keeps only its
/// SHA-256, so the table can't be used to sign in. Kept here rather than in memory, so a restart
/// doesn't sign everyone out.
/// </summary>
[Table("web_sessions")]
[Index(nameof(TokenHash), IsUnique = true)]
[Index(nameof(PlayerEntityId))]
public class WebSessionEntity : TurboEntity
{
    public const int TOKEN_HASH_LENGTH = 64;

    [Column("token_hash")]
    [StringLength(TOKEN_HASH_LENGTH)]
    public required string TokenHash { get; set; }

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("expires_at")]
    public required DateTime ExpiresAt { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }
}
