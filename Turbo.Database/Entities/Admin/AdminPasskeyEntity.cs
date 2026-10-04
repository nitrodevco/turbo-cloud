using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Admin;

/// <summary>
/// A passkey a player signs in to the admin panel with: the public key their authenticator made,
/// never anything secret. Passkeys are the panel's only sign-in, so a player with none has no
/// way in. Owned by the player's admin account grain.
/// </summary>
[Table("admin_passkeys")]
[Index(nameof(PlayerEntityId))]
[Index(nameof(CredentialId), IsUnique = true)]
public class AdminPasskeyEntity : TurboEntity
{
    [Column("player_id")]
    public int PlayerEntityId { get; set; }

    /// <summary>The authenticator's id for the credential. WebAuthn allows up to 1023 bytes.</summary>
    [Column("credential_id")]
    [MaxLength(1023)]
    public required byte[] CredentialId { get; set; }

    /// <summary>The credential's public key, COSE-encoded.</summary>
    [Column("public_key")]
    public required byte[] PublicKey { get; set; }

    /// <summary>The authenticator's last signature counter, to notice a cloned authenticator.</summary>
    [Column("sign_count")]
    public long SignCount { get; set; }

    /// <summary>What the player calls it ("Laptop", "Phone").</summary>
    [Column("name")]
    [MaxLength(64)]
    public required string Name { get; set; }

    /// <summary>The authenticator model, when it says.</summary>
    [Column("aaguid")]
    public Guid AaGuid { get; set; }

    [Column("last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
