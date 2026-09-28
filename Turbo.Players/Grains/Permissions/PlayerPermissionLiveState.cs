using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Players.Grains.Permissions;

internal sealed class PlayerPermissionLiveState
{
    public required PlayerId PlayerId { get; init; }

    /// <summary>
    /// The player's direct memberships, by group and whether temporary: a permanent and a
    /// temporary membership of one group can both exist. Expired ones stay until swept.
    /// </summary>
    public Dictionary<
        (int GroupId, bool Temporary),
        PermissionGroupMembershipSnapshot
    > MembershipsByGroupId { get; } = [];

    /// <summary>The player's own nodes, by node and whether temporary. Expired ones stay until swept.</summary>
    public Dictionary<
        (string Node, bool Temporary),
        PermissionNodeAssignmentSnapshot
    > NodesByNode { get; } = [];

    /// <summary>The player's own meta, by key and whether temporary. Expired ones stay until swept.</summary>
    public Dictionary<
        (string Key, bool Temporary),
        PermissionMetaAssignmentSnapshot
    > MetaByKey { get; } = [];

    /// <summary>The directory's groups as last pushed or read.</summary>
    public PermissionGroupDirectorySnapshot Groups { get; set; } =
        PermissionGroupDirectorySnapshot.EMPTY;

    /// <summary>The registry <see cref="Resolved"/> was worked out against; compared by reference.</summary>
    public PermissionRegistry? Registry { get; set; }

    /// <summary>What the player holds. Null only before the first resolve.</summary>
    public ResolvedPermissionsSnapshot? Resolved { get; set; }
}
