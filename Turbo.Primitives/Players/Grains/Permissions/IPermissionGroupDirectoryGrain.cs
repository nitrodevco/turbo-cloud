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
/// <para>
/// A permanent and a temporary assignment of the same node, meta key or group are separate rows:
/// a write with an expiry touches the temporary one, a write without touches the permanent one,
/// and a removal says which it means with <c>temporary</c>. While both exist the temporary one
/// wins. <c>mode</c> says what setting a temporary one that is already running does to its expiry.
/// </para>
/// </remarks>
public interface IPermissionGroupDirectoryGrain : IGrainWithStringKey
{
    public Task<PermissionGroupDirectorySnapshot> GetSnapshotAsync(CancellationToken ct);

    /// <summary>A player permission grain asks to be told of every change, while it is active.</summary>
    public Task SubscribeAsync(PlayerId playerId, CancellationToken ct);

    public Task UnsubscribeAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>
    /// Reads every group from the database again, for tables something else wrote (a CMS, a
    /// housekeeping panel), publishes them, and has every active player permission grain read its
    /// own rows again too. Not audited: it changes nothing, it catches up. Returns how many player
    /// grains were told.
    /// </summary>
    public Task<int> ReloadAsync(CancellationToken ct);

    /// <summary>
    /// A plugin's nodes were registered or went away: every active player permission grain
    /// resolves against the new registry and tells the client and room of any difference. The
    /// directory calls this on itself when <c>IPermissionRegistryProvider.Changed</c> is raised.
    /// </summary>
    public Task OnRegistryChangedAsync(CancellationToken ct);

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
        PermissionExpiryModeType mode,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> UnsetNodeAsync(
        string name,
        string node,
        bool temporary,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> SetMetaAsync(
        string name,
        string key,
        string value,
        DateTime? expiresAt,
        PermissionExpiryModeType mode,
        PlayerId? actor,
        CancellationToken ct
    );

    public Task<PermissionChangeResultType> UnsetMetaAsync(
        string name,
        string key,
        bool temporary,
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

    /// <summary>
    /// The players who hold a group directly, unexpired, permanent members first. Empty for an
    /// unknown group, and for default, which everybody holds without a row.
    /// </summary>
    public Task<ImmutableArray<PermissionGroupMemberSnapshot>> GetMembersAsync(
        string name,
        int count,
        CancellationToken ct
    );

    /// <summary>
    /// Every group, then every player, with an unexpired assignment that names
    /// <paramref name="node"/> exactly or by wildcard, granting or denying it. Who is given a
    /// node directly, not who ends up holding it; <c>perm check</c> answers that for one player.
    /// </summary>
    public Task<ImmutableArray<PermissionNodeHolderSnapshot>> FindNodeHoldersAsync(
        string node,
        int count,
        CancellationToken ct
    );

    /// <summary>
    /// The most recent audit rows about anyone, newest first; with <paramref name="search"/>, only
    /// those whose node, key or group name contains it.
    /// </summary>
    public Task<ImmutableArray<PermissionAuditSnapshot>> GetRecentAuditAsync(
        string? search,
        int count,
        CancellationToken ct
    );

    /// <summary>The most recent audit rows about a group, newest first.</summary>
    public Task<ImmutableArray<PermissionAuditSnapshot>> GetAuditAsync(
        string name,
        int count,
        CancellationToken ct
    );
}
