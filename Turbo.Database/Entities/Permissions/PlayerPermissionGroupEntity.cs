using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Permissions;

/// <summary>A group a player holds directly. The default group is held without a row.</summary>
[Table("player_permission_groups")]
[Index(nameof(PlayerEntityId), nameof(GroupEntityId), IsUnique = true)]
// A group edit re-resolves its online members, which reads by group.
[Index(nameof(GroupEntityId))]
public class PlayerPermissionGroupEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("group_id")]
    public required int GroupEntityId { get; set; }

    /// <summary>UTC. Null for a permanent membership.</summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }

    [ForeignKey(nameof(GroupEntityId))]
    public PermissionGroupEntity? GroupEntity { get; set; }
}
