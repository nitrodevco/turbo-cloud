using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Database.Entities.Permissions;

/// <summary>A node a group grants (<see cref="Value"/> true) or denies (false).</summary>
[Table("permission_group_nodes")]
[Index(nameof(GroupEntityId), nameof(Node), IsUnique = true)]
public class PermissionGroupNodeEntity : TurboEntity
{
    [Column("group_id")]
    public required int GroupEntityId { get; set; }

    /// <summary>A node or a wildcard (<c>room.*</c>, <c>*</c>).</summary>
    [Column("node")]
    [MaxLength(PermissionNodeFormat.MAX_LENGTH)]
    public required string Node { get; set; }

    [Column("value")]
    public required bool Value { get; set; }

    /// <summary>UTC. Null for a permanent assignment.</summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [ForeignKey(nameof(GroupEntityId))]
    public PermissionGroupEntity? GroupEntity { get; set; }
}
