using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Players.Snapshots.Permissions;

/// <summary>The answer to "why does this player have (or not have) this node".</summary>
[GenerateSerializer, Immutable]
public sealed record PermissionCheckSnapshot
{
    [Id(0)]
    public required string Node { get; init; }

    /// <summary>False for a node nothing registered; the rest is still worked out.</summary>
    [Id(1)]
    public required bool IsRegistered { get; init; }

    [Id(2)]
    public required bool Granted { get; init; }

    /// <summary>The assignment that decided, or <c>null</c> when nothing matched and the node is denied by default.</summary>
    [Id(3)]
    public PermissionAssignmentSourceSnapshot? Decision { get; init; }

    /// <summary>Lower-priority sources that also matched, in priority order: what the decision beat.</summary>
    [Id(4)]
    public required ImmutableArray<PermissionAssignmentSourceSnapshot> Overridden { get; init; }
}
