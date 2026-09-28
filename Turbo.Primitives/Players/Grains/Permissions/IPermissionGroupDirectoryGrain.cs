using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Primitives.Players.Grains.Permissions;

/// <summary>
/// Every permission group in the hotel, one grain. The only writer of group data: each change
/// is saved, audited, and pushed to every <see cref="IPlayerPermissionGrain"/> that is active.
/// Reads answer from memory. See <c>docs/permissions.md</c> §9.
/// </summary>
/// <remarks>
/// <c>actor</c> on every write is who made it, for the audit: a player, or <c>null</c> for the
/// console. Nothing here checks that the actor may make the change; the caller does.
/// </remarks>
public interface IPermissionGroupDirectoryGrain : IGrainWithStringKey
{
    public Task<PermissionGroupDirectorySnapshot> GetSnapshotAsync(CancellationToken ct);

    /// <summary>A player permission grain asks to be told of every change, while it is active.</summary>
    public Task SubscribeAsync(PlayerId playerId, CancellationToken ct);

    public Task UnsubscribeAsync(PlayerId playerId, CancellationToken ct);

    public Task<PermissionChangeResultType> CreateGroupAsync(
        string name,
        string displayName,
        int weight,
        PlayerId? actor,
        CancellationToken ct
    );

    /// <summary>Deletes a group; its memberships, nodes, meta and parent links go with it.</summary>
    public Task<PermissionChangeResultType> DeleteGroupAsync(
        string name,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> SetWeightAsync(
        string name,
        int weight,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> SetDisplayNameAsync(
        string name,
        string displayName,
        PlayerId? actor,
        CancellationToken ct
    );

    /// <summary>Sets a node, or a wildcard, on a group, replacing any value and expiry it had.</summary>
    public Task<PermissionChangeResultType> SetNodeAsync(
        string name,
        string node,
        bool value,
        DateTime? expiresAt,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> UnsetNodeAsync(
        string name,
        string node,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> SetMetaAsync(
        string name,
        string key,
        string value,
        DateTime? expiresAt,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> UnsetMetaAsync(
        string name,
        string key,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> AddParentAsync(
        string name,
        string parentName,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> RemoveParentAsync(
        string name,
        string parentName,
        PlayerId? actor,
        CancellationToken ct
    );

    /// <summary>The most recent audit rows about a group, newest first.</summary>
    public Task<ImmutableArray<PermissionAuditSnapshot>> GetAuditAsync(
        string name,
        int count,
        CancellationToken ct
    );
}
