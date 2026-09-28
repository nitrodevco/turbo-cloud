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
[Index(nameof(PlayerEntityId), nameof(Node), IsUnique = true)]
public class PlayerPermissionNodeEntity : TurboEntity
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

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
