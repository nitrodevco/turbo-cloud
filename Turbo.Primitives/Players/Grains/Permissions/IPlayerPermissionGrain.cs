using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Primitives.Players.Grains.Permissions;

/// <summary>
/// One player's permissions: their own groups, nodes and meta, and the set those resolve to
/// against the directory's groups. Every permission check about the player asks this grain, and
/// it answers from memory. The only writer of the player's rows. See <c>docs/permissions.md</c>
/// §9.
/// </summary>
/// <remarks>
/// <c>actor</c> on every write is who made it, for the audit: a player, or <c>null</c> for the
/// console. Nothing here checks that the actor may make the change; the caller does.
/// <para>
/// A permanent and a temporary assignment of the same node, meta key or group are separate rows:
/// a write with an expiry touches the temporary one, a write without touches the permanent one,
/// and a removal says which it means with <c>temporary</c>. While both exist the temporary one
/// wins. <c>mode</c> says what setting a temporary one that is already running does to its expiry.
/// </para>
/// </remarks>
public interface IPlayerPermissionGrain : IGrainWithIntegerKey
{
    /// <summary>Whether the player holds a registered node. An unregistered node is denied and logged.</summary>
    public Task<bool> HasAsync(string node, CancellationToken ct);

    /// <summary>
    /// <see cref="ExplainAsync"/> for a grain this one itself waits on: whether the node is held
    /// and, in <see cref="PermissionAssignmentSourceSnapshot.GrantedUntil"/>, until when. The
    /// subscription grain asks it whether a membership is held by permission, while this grain
    /// reads the club level back from the subscription grain; answered between this grain's
    /// turns, neither can wait on the other for ever. It awaits nothing, so it never sees a turn
    /// half done.
    /// </summary>
    [AlwaysInterleave]
    public Task<PermissionCheckSnapshot> ExplainInterleavedAsync(string node, CancellationToken ct);

    /// <summary>The resolved value of a registered meta key, or <c>null</c> when nothing sets it.</summary>
    public Task<string?> GetMetaAsync(string key, CancellationToken ct);

    public Task<ResolvedPermissionsSnapshot> GetResolvedAsync(CancellationToken ct);

    /// <summary>What the client is told about the player: security level, ambassador, moderator, perks.</summary>
    public Task<PermissionClientSnapshot> GetClientStateAsync(CancellationToken ct);

    /// <summary>
    /// Sends the player's sessions <c>UserRights</c> and <c>PerkAllowances</c>. Called at login;
    /// after that the grain sends them itself whenever what they say changes, and when the club
    /// level they carry changes (the subscription grain asks).
    /// </summary>
    public Task SendClientStateAsync(CancellationToken ct);

    /// <summary>
    /// Sends the player's client-facing nodes (<c>permission.nodes</c>), for a session that has
    /// just accepted the extension; after that they go with every change.
    /// </summary>
    public Task SendPermissionNodesAsync(CancellationToken ct);

    /// <summary>Sends current speaking/trading restrictions after a successful login or reconnect.
    /// Capability resends must not call this method.</summary>
    public Task NotifyActiveRestrictionsAsync(CancellationToken ct);

    /// <summary>Why the player does or does not hold <paramref name="node"/>.</summary>
    public Task<PermissionCheckSnapshot> ExplainAsync(string node, CancellationToken ct);

    /// <summary>What is set on the player directly, expired rows excluded.</summary>
    public Task<PlayerPermissionAssignmentsSnapshot> GetAssignmentsAsync(CancellationToken ct);

    /// <summary>Puts the player in a group, or changes the expiry of a membership they have.</summary>
    public Task<PermissionChangeResultType> AddGroupAsync(
        string groupName,
        DateTime? expiresAt,
        PermissionExpiryModeType mode,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> RemoveGroupAsync(
        string groupName,
        bool temporary,
        PlayerId? actor,
        CancellationToken ct
    );

    /// <summary>Sets a node, or a wildcard, on the player, replacing any value and expiry it had.</summary>
    public Task<PermissionChangeResultType> SetNodeAsync(
        string node,
        bool value,
        DateTime? expiresAt,
        PermissionExpiryModeType mode,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> UnsetNodeAsync(
        string node,
        bool temporary,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> SetMetaAsync(
        string key,
        string value,
        DateTime? expiresAt,
        PermissionExpiryModeType mode,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> UnsetMetaAsync(
        string key,
        bool temporary,
        PlayerId? actor,
        CancellationToken ct
    );

    /// <summary>The most recent audit rows about the player, newest first.</summary>
    public Task<ImmutableArray<PermissionAuditSnapshot>> GetAuditAsync(
        int count,
        CancellationToken ct
    );

    /// <summary>
    /// Reads the player's own groups, nodes and meta from the database again, for rows something
    /// else wrote, then resolves and tells the client and room of any difference.
    /// </summary>
    public Task ReloadAsync(CancellationToken ct);

    /// <summary>
    /// <c>perm verbose</c>: logs every check of a node starting with <paramref name="filter"/>
    /// (empty for every node) made about this player, by this grain or by the room they are in,
    /// with its answer. <c>null</c> stops it. Lasts while the grain is active.
    /// </summary>
    public Task SetVerboseAsync(string? filter, CancellationToken ct);

    /// <summary>The directory's push after a group changed: resolve against the new groups.</summary>
    public Task OnGroupsChangedAsync(PermissionGroupDirectorySnapshot groups, CancellationToken ct);

    /// <summary>
    /// The directory's push after the registry changed (a plugin loaded or unloaded): resolve
    /// against the new registry, unless it is already the one resolved against.
    /// </summary>
    public Task OnRegistryChangedAsync(CancellationToken ct);
}
