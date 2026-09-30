using System;
using Orleans;

namespace Turbo.Primitives.Players.Snapshots.Permissions;

/// <summary>One node set on a group or a player: granted when <see cref="Value"/> is true, denied when false.</summary>
[GenerateSerializer, Immutable]
public sealed record PermissionNodeAssignmentSnapshot
{
    /// <summary>A node, or a wildcard (<c>room.*</c>, <c>*</c>).</summary>
    [Id(0)]
    public required string Node { get; init; }

    [Id(1)]
    public required bool Value { get; init; }

    /// <summary>UTC. <c>null</c> for a permanent assignment.</summary>
    [Id(2)]
    public DateTime? ExpiresAt { get; init; }
}
