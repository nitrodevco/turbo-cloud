using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
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
/// </remarks>
public interface IPlayerPermissionGrain : IGrainWithIntegerKey
{
    /// <summary>Whether the player holds a registered node. An unregistered node is denied and logged.</summary>
    public Task<bool> HasAsync(string node, CancellationToken ct);

    /// <summary>The resolved value of a registered meta key, or <c>null</c> when nothing sets it.</summary>
    public Task<string?> GetMetaAsync(string key, CancellationToken ct);

    public Task<ResolvedPermissionsSnapshot> GetResolvedAsync(CancellationToken ct);

    /// <summary>Why the player does or does not hold <paramref name="node"/>.</summary>
    public Task<PermissionCheckSnapshot> ExplainAsync(string node, CancellationToken ct);

    /// <summary>What is set on the player directly, expired rows excluded.</summary>
    public Task<PlayerPermissionAssignmentsSnapshot> GetAssignmentsAsync(CancellationToken ct);

    /// <summary>Puts the player in a group, or changes the expiry of a membership they have.</summary>
    public Task<PermissionChangeResultType> AddGroupAsync(
        string groupName,
        DateTime? expiresAt,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> RemoveGroupAsync(
        string groupName,
        PlayerId? actor,
        CancellationToken ct
    );

    /// <summary>Sets a node, or a wildcard, on the player, replacing any value and expiry it had.</summary>
    public Task<PermissionChangeResultType> SetNodeAsync(
        string node,
        bool value,
        DateTime? expiresAt,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> UnsetNodeAsync(
        string node,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> SetMetaAsync(
        string key,
        string value,
        DateTime? expiresAt,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> UnsetMetaAsync(
        string key,
        PlayerId? actor,
        CancellationToken ct
    );

    /// <summary>The most recent audit rows about the player, newest first.</summary>
    public Task<ImmutableArray<PermissionAuditSnapshot>> GetAuditAsync(
        int count,
        CancellationToken ct
    );

    /// <summary>The directory's push after a group changed: resolve against the new groups.</summary>
    public Task OnGroupsChangedAsync(PermissionGroupDirectorySnapshot groups, CancellationToken ct);
}
