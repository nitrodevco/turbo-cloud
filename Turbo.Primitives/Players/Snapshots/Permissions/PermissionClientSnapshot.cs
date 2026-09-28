using System.Collections.Immutable;
using System.Linq;
using Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Players.Snapshots.Permissions;

/// <summary>
/// What the client is told about one player's permissions: the one number, the two booleans and
/// the perk list of <c>docs/permissions.md</c> §8. Produced by
/// <see cref="Turbo.Primitives.Players.Permissions.PermissionProjection"/>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PermissionClientSnapshot
{
    /// <summary><c>UserRights.securityLevel</c>.</summary>
    [Id(0)]
    public required SecurityLevelType SecurityLevel { get; init; }

    /// <summary><c>UserRights.isAmbassador</c>.</summary>
    [Id(1)]
    public required bool IsAmbassador { get; init; }

    /// <summary>The room avatar's moderator flag.</summary>
    [Id(2)]
    public required bool IsModerator { get; init; }

    /// <summary>Every perk a registered node projects to, ordered by perk.</summary>
    [Id(3)]
    public required ImmutableArray<PerkAllowanceSnapshot> Perks { get; init; }

    /// <summary>
    /// Every client-facing node held (<see cref="Turbo.Primitives.Players.Permissions.PermissionNodeDefinition.IsClientVisible"/>),
    /// ordered: <c>TurboPermissionNodesMessage</c>, for a session that accepted it.
    /// </summary>
    [Id(4)]
    public required ImmutableArray<string> Nodes { get; init; }

    /// <summary>Whether the client would be told the same thing; record equality compares the array by reference.</summary>
    public bool Matches(PermissionClientSnapshot other) =>
        SecurityLevel == other.SecurityLevel
        && IsAmbassador == other.IsAmbassador
        && IsModerator == other.IsModerator
        && Perks.SequenceEqual(other.Perks)
        && Nodes.SequenceEqual(other.Nodes);
}
