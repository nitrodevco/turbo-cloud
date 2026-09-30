using System;
using Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Players.Snapshots.Permissions;

/// <summary>One row of <c>permission_audit</c>.</summary>
[GenerateSerializer, Immutable]
public sealed record PermissionAuditSnapshot
{
    [Id(0)]
    public required DateTime CreatedAt { get; init; }

    /// <summary>Null for the console or the server itself.</summary>
    [Id(1)]
    public PlayerId? ActorPlayerId { get; init; }

    [Id(2)]
    public required PermissionAuditTargetType TargetType { get; init; }

    [Id(3)]
    public required int TargetId { get; init; }

    [Id(4)]
    public required PermissionAuditActionType Action { get; init; }

    [Id(5)]
    public required string Subject { get; init; }

    [Id(6)]
    public string? Value { get; init; }

    [Id(7)]
    public DateTime? ExpiresAt { get; init; }
}
