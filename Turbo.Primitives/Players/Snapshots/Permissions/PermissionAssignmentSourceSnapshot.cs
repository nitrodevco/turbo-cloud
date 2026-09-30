using System;
using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Players.Snapshots.Permissions;

/// <summary>One assignment the resolver weighed for a node, and where it came from.</summary>
[GenerateSerializer, Immutable]
public sealed record PermissionAssignmentSourceSnapshot
{
    [Id(0)]
    public required PermissionSourceType SourceType { get; init; }

    /// <summary>The group, for <see cref="PermissionSourceType.Group"/>.</summary>
    [Id(1)]
    public int? GroupId { get; init; }

    [Id(2)]
    public string? GroupName { get; init; }

    [Id(3)]
    public int? GroupWeight { get; init; }

    /// <summary>
    /// Group names from one the player holds directly down to this one, through the parents that
    /// reach it. Empty for <see cref="PermissionSourceType.Player"/>.
    /// </summary>
    [Id(4)]
    public required ImmutableArray<string> Path { get; init; }

    /// <summary>The assignment as stored: the node, or the wildcard that matched it.</summary>
    [Id(5)]
    public required string Node { get; init; }

    [Id(6)]
    public required bool Value { get; init; }

    [Id(7)]
    public DateTime? ExpiresAt { get; init; }
}
