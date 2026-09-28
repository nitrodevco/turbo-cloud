using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Database.Entities.Permissions;

/// <summary>A meta value set on one player, beating anything their groups set.</summary>
[Table("player_permission_meta")]
[Index(nameof(PlayerEntityId), nameof(Key), IsUnique = true)]
public class PlayerPermissionMetaEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("meta_key")]
    [MaxLength(PermissionNodeFormat.MAX_LENGTH)]
    public required string Key { get; set; }

    /// <summary>Text; the reader of the key parses it.</summary>
    [Column("value")]
    public required string Value { get; set; }

    /// <summary>UTC. Null for a permanent assignment.</summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
