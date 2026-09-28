using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Players.Grains.Permissions;

internal sealed class PlayerPermissionLiveState
{
    public required PlayerId PlayerId { get; init; }

    /// <summary>The player's direct memberships, by group id. Expired ones stay until swept.</summary>
    public Dictionary<int, PermissionGroupMembershipSnapshot> MembershipsByGroupId { get; } = [];

    /// <summary>The player's own nodes, by node. Expired ones stay until swept.</summary>
    public Dictionary<string, PermissionNodeAssignmentSnapshot> NodesByNode { get; } = [];

    /// <summary>The player's own meta, by key. Expired ones stay until swept.</summary>
    public Dictionary<string, PermissionMetaAssignmentSnapshot> MetaByKey { get; } = [];

    /// <summary>The directory's groups as last pushed or read.</summary>
    public PermissionGroupDirectorySnapshot Groups { get; set; } =
        PermissionGroupDirectorySnapshot.EMPTY;

    /// <summary>The registry <see cref="Resolved"/> was worked out against; compared by reference.</summary>
    public PermissionRegistry? Registry { get; set; }

    /// <summary>What the player holds. Null only before the first resolve.</summary>
    public ResolvedPermissionsSnapshot? Resolved { get; set; }
}
