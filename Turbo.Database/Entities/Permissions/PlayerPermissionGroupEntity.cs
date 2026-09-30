using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Permissions;

/// <summary>A group a player holds directly. The default group is held without a row.</summary>
[Table("player_permission_groups")]
// One permanent and one temporary row may coexist: the temporary one wins while it lasts.
[Index(nameof(PlayerEntityId), nameof(GroupEntityId), nameof(IsTemporary), IsUnique = true)]
// A group edit re-resolves its online members, which reads by group.
[Index(nameof(GroupEntityId))]
public class PlayerPermissionGroupEntity : TurboEntity, IPermissionExpiringEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("group_id")]
    public required int GroupEntityId { get; set; }

    /// <summary>UTC. Null for a permanent membership.</summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Whether <see cref="ExpiresAt"/> is set. Kept by the writer; part of the unique key.</summary>
    [Column("is_temporary")]
    public bool IsTemporary { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }

    [ForeignKey(nameof(GroupEntityId))]
    public PermissionGroupEntity? GroupEntity { get; set; }
}
