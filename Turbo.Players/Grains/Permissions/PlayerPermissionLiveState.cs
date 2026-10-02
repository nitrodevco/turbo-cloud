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

    /// <summary>The <c>perm verbose</c> filter, copied onto every resolved set; <c>null</c> when off.</summary>
    public string? VerboseFilter { get; set; }

    /// <summary>The directory's groups as last pushed or read.</summary>
    public PermissionGroupDirectorySnapshot Groups { get; set; } =
        PermissionGroupDirectorySnapshot.EMPTY;

    /// <summary>The registry <see cref="Resolved"/> was worked out against; compared by reference.</summary>
    public PermissionRegistry? Registry { get; set; }

    /// <summary>What the client should be told, from <see cref="Resolved"/>. Null only before the first resolve.</summary>
    public PermissionClientSnapshot? Client { get; set; }

    /// <summary>What the client was last told, or is taken to know; a change is sent when they differ.</summary>
    public PermissionClientSnapshot? SentClient { get; set; }

    /// <summary>The resolved set the player's room was last told of, or is taken to hold.</summary>
    public ResolvedPermissionsSnapshot? SentRoom { get; set; }

    /// <summary>
    /// The resolved set plugins were last told of, or are taken to know; a
    /// <c>PlayerPermissionsChangedEvent</c> is raised when what it holds differs.
    /// </summary>
    public ResolvedPermissionsSnapshot? Announced { get; set; }

    /// <summary>What the player holds. Null only before the first resolve.</summary>
    public ResolvedPermissionsSnapshot? Resolved { get; set; }

    /// <summary>The prior immutable resolution inputs used to identify expiry-only restorations.</summary>
    public RestrictionResolutionInputs? RestrictionInputs { get; set; }
}
