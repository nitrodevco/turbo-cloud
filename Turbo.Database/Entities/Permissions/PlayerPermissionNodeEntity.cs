using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Database.Entities.Permissions;

/// <summary>
/// A node set on one player, beating anything their groups say: a grant their groups do not
/// give, or a denial (a sanction) of one they do.
/// </summary>
[Table("player_permission_nodes")]
// One permanent and one temporary row may coexist: the temporary one wins while it lasts.
[Index(nameof(PlayerEntityId), nameof(Node), nameof(IsTemporary), IsUnique = true)]
public class PlayerPermissionNodeEntity : TurboEntity, IPermissionAssignmentEntity<bool>
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

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

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
