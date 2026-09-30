using System;
using Orleans;

namespace Turbo.Primitives.Players.Snapshots.Permissions;

/// <summary>A group a player holds directly.</summary>
[GenerateSerializer, Immutable]
public sealed record PermissionGroupMembershipSnapshot
{
    [Id(0)]
    public required int GroupId { get; init; }

    /// <summary>UTC. <c>null</c> for a permanent membership.</summary>
    [Id(1)]
    public DateTime? ExpiresAt { get; init; }
}
