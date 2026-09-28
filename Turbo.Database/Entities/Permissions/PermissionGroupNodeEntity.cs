using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Database.Entities.Permissions;

/// <summary>A node a group grants (<see cref="Value"/> true) or denies (false).</summary>
[Table("permission_group_nodes")]
// One permanent and one temporary row may coexist: the temporary one wins while it lasts.
[Index(nameof(GroupEntityId), nameof(Node), nameof(IsTemporary), IsUnique = true)]
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

    /// <summary>Whether <see cref="ExpiresAt"/> is set. Kept by the writer; part of the unique key.</summary>
    [Column("is_temporary")]
    public bool IsTemporary { get; set; }

    [ForeignKey(nameof(GroupEntityId))]
    public PermissionGroupEntity? GroupEntity { get; set; }
}
