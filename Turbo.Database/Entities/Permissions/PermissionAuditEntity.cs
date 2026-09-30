using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Database.Entities.Permissions;

/// <summary>
/// One change to a group's or a player's permissions, written in the same save as the change.
/// Append-only: nothing updates or deletes a row. It keeps no foreign keys, so the history of a
/// deleted group or player survives them.
/// </summary>
[Table("permission_audit")]
// "What happened to this player / group", newest first.
[Index(nameof(TargetType), nameof(TargetId), nameof(CreatedAt))]
public class PermissionAuditEntity : TurboEntity
{
    /// <summary>Who made the change: a player id, or null for the console or the server itself (an expiry).</summary>
    [Column("actor_player_id")]
    public int? ActorPlayerId { get; set; }

    [Column("target_type")]
    public required PermissionAuditTargetType TargetType { get; set; }

    /// <summary>The player id or the group id, by <see cref="TargetType"/>.</summary>
    [Column("target_id")]
    public required int TargetId { get; set; }

    [Column("action")]
    public required PermissionAuditActionType Action { get; set; }

    /// <summary>What was changed: a node, a meta key, or a group name.</summary>
    [Column("subject")]
    [MaxLength(PermissionNodeFormat.MAX_LENGTH)]
    public required string Subject { get; set; }

    /// <summary>The new value, where the action has one: <c>true</c>/<c>false</c>, a meta value, a weight.</summary>
    [Column("value")]
    public string? Value { get; set; }

    /// <summary>UTC. The expiry the change was made with, if any.</summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }
}
