using System;
using Orleans;

namespace Turbo.Primitives.Players.Snapshots.Permissions;

/// <summary>One meta value set on a group or a player.</summary>
[GenerateSerializer, Immutable]
public sealed record PermissionMetaAssignmentSnapshot
{
    [Id(0)]
    public required string Key { get; init; }

    /// <summary>Stored as text; the reader of the key parses it.</summary>
    [Id(1)]
    public required string Value { get; init; }

    /// <summary>UTC. <c>null</c> for a permanent assignment.</summary>
    [Id(2)]
    public DateTime? ExpiresAt { get; init; }
}
