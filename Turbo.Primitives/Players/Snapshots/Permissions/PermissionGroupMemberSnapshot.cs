using System;
using Orleans;

namespace Turbo.Primitives.Players.Snapshots.Permissions;

/// <summary>A player who holds a group directly: one row of <c>perm group &lt;g&gt; members</c>.</summary>
[GenerateSerializer, Immutable]
public sealed record PermissionGroupMemberSnapshot
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    /// <summary>UTC. <c>null</c> for a permanent membership.</summary>
    [Id(1)]
    public DateTime? ExpiresAt { get; init; }
}
