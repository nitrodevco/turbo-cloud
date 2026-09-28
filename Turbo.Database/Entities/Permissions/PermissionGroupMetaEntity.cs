using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Database.Entities.Permissions;

/// <summary>A meta value a group sets (<c>client.security_level = 5</c>).</summary>
[Table("permission_group_meta")]
// One permanent and one temporary row may coexist: the temporary one wins while it lasts.
[Index(nameof(GroupEntityId), nameof(Key), nameof(IsTemporary), IsUnique = true)]
public class PermissionGroupMetaEntity : TurboEntity
{
    [Column("group_id")]
    public required int GroupEntityId { get; set; }

    [Column("meta_key")]
    [MaxLength(PermissionNodeFormat.MAX_LENGTH)]
    public required string Key { get; set; }

    /// <summary>Text; the reader of the key parses it.</summary>
    [Column("value")]
    public required string Value { get; set; }

    /// <summary>UTC. Null for a permanent assignment.</summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Whether <see cref="ExpiresAt"/> is set. Kept by the writer; part of the unique key.</summary>
    [Column("is_temporary")]
    public bool IsTemporary { get; set; }

    [ForeignKey(nameof(GroupEntityId))]
    public PermissionGroupEntity? GroupEntity { get; set; }
}
