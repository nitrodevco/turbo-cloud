using System;
using Orleans;

namespace Turbo.Primitives.Players.Snapshots.Permissions;

/// <summary>
/// A group a staff member is previewing: until <see cref="EndsAt"/>, or until they stop, they
/// hold what a plain member of it holds, in the hotel and the admin panel alike
/// (<c>docs/permissions.md</c>, §9, <i>Previewing a group</i>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PermissionPreviewSnapshot
{
    [Id(0)]
    public required string GroupName { get; init; }

    [Id(1)]
    public required string DisplayName { get; init; }

    /// <summary>UTC. When the player is themselves again, whether or not they stopped it.</summary>
    [Id(2)]
    public required DateTime EndsAt { get; init; }
}
