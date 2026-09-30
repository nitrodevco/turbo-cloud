using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Players.Grains.Permissions;

internal sealed class PermissionGroupDirectoryLiveState
{
    /// <summary>Every group, as last published. Replaced whole on each change, never edited.</summary>
    public PermissionGroupDirectorySnapshot Snapshot { get; set; } =
        PermissionGroupDirectorySnapshot.EMPTY;

    /// <summary>Players whose permission grain is active and is told of every change.</summary>
    public HashSet<PlayerId> Subscribers { get; } = [];
}
