using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Players.Snapshots.Permissions;

/// <summary>A permission group as the directory holds it: its own assignments, not its inherited ones.</summary>
[GenerateSerializer, Immutable]
public sealed record PermissionGroupSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    /// <summary>The stable lowercase key (<c>moderator</c>).</summary>
    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required string DisplayName { get; init; }

    /// <summary>Higher wins when two groups disagree.</summary>
    [Id(3)]
    public required int Weight { get; init; }

    /// <summary>The groups this one inherits from.</summary>
    [Id(4)]
    public required ImmutableArray<int> ParentIds { get; init; }

    [Id(5)]
    public required ImmutableArray<PermissionNodeAssignmentSnapshot> Nodes { get; init; }

    [Id(6)]
    public required ImmutableArray<PermissionMetaAssignmentSnapshot> Meta { get; init; }
}
