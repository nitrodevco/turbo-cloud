using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Turbo.Database.Context;
using Turbo.Database.Entities.Permissions;
using Turbo.Database.Extensions;
using Turbo.Players.Configuration;
using Turbo.Players.Permissions;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Players.Grains.Permissions;

/// <summary>
/// One player's permissions. Write-through: each change is saved and audited before the
/// in-memory rows move, so there is nothing to flush on deactivation. The resolved set is worked
/// out again whenever the player's rows change, the directory pushes new groups, the registry
/// changes (a plugin loaded or unloaded; noticed on the next read), or an assignment runs out.
/// While active it is subscribed to the directory.
/// </summary>
internal sealed class PlayerPermissionGrain : Grain, IPlayerPermissionGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly PermissionConfig _permissionConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly IPermissionRegistryProvider _permissionRegistryProvider;
    private readonly ILogger<IPlayerPermissionGrain> _logger;

    private readonly PlayerPermissionLiveState _state;

    private IGrainTimer? _expiryTimer;

    private PlayerId PlayerId => _state.PlayerId;

    public PlayerPermissionGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<PlayerConfig> playerConfig,
        IGrainFactory grainFactory,
        IPermissionRegistryProvider permissionRegistryProvider,
        ILogger<IPlayerPermissionGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _permissionConfig = playerConfig.Value.Permissions;
        _grainFactory = grainFactory;
        _permissionRegistryProvider = permissionRegistryProvider;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);

            var directory = _grainFactory.GetPermissionGroupDirectoryGrain();

            _state.Groups = await directory.GetSnapshotAsync(ct);

            await directory.SubscribeAsync(PlayerId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to hydrate the permissions of player {PlayerId}",
                PlayerId
            );

            throw;
        }

        // Idle until Resolve sets it; an assignment already past its expiry makes it fire
        // straight away, which is what sweeps rows that ran out while the player was away.
        _expiryTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((PlayerPermissionGrain)self!).SweepExpiredAsync(ct),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan
        );

        Resolve();
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _expiryTimer?.Dispose();
        _expiryTimer = null;

        try
        {
            await _grainFactory.GetPermissionGroupDirectoryGrain().UnsubscribeAsync(PlayerId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to unsubscribe the permissions of player {PlayerId} from the group directory",
                PlayerId
            );
        }
    }

    public Task<bool> HasAsync(string node, CancellationToken ct)
    {
        var resolved = EnsureResolved();

        if (!_state.Registry!.IsRegistered(node))
        {
            // A gate asking for a node nobody registered is a bug in the gate, not a denial.
            _logger.LogWarning(
                "Permission check for unregistered node {Node} on player {PlayerId}; denied",
                node,
                PlayerId
            );

            return Task.FromResult(false);
        }

        return Task.FromResult(resolved.Has(node));
    }

    public Task<string?> GetMetaAsync(string key, CancellationToken ct) =>
        Task.FromResult(EnsureResolved().Meta.GetValueOrDefault(key));

    public Task<ResolvedPermissionsSnapshot> GetResolvedAsync(CancellationToken ct) =>
        Task.FromResult(EnsureResolved());

    public Task<PermissionCheckSnapshot> ExplainAsync(string node, CancellationToken ct)
    {
        EnsureResolved();

        return Task.FromResult(
            PermissionResolver.Explain(
                _state.Registry!,
                _state.Groups.Groups,
                BuildAssignments(),
                node,
                DateTime.UtcNow
            )
        );
    }

    public Task<PlayerPermissionAssignmentsSnapshot> GetAssignmentsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var all = BuildAssignments();

        return Task.FromResult(
            new PlayerPermissionAssignmentsSnapshot
            {
                Groups = [.. all.Groups.Where(x => IsLive(x.ExpiresAt, now))],
                Nodes = [.. all.Nodes.Where(x => IsLive(x.ExpiresAt, now))],
                Meta = [.. all.Meta.Where(x => IsLive(x.ExpiresAt, now))],
            }
        );
    }

    public async Task<PermissionChangeResultType> AddGroupAsync(
        string groupName,
        DateTime? expiresAt,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (groupName == PermissionGroupNames.DEFAULT)
            return PermissionChangeResultType.ProtectedGroup;

        if (expiresAt <= DateTime.UtcNow)
            return PermissionChangeResultType.Expired;

        if (FindGroup(groupName) is not { } group)
            return PermissionChangeResultType.UnknownGroup;

        return await WriteAsync(
            async dbCtx =>
            {
                var row = await dbCtx.PlayerPermissionGroups.FirstOrDefaultAsync(
                    x => x.PlayerEntityId == PlayerId.Value && x.GroupEntityId == group.Id,
                    ct
                );

                if (row is null)
                {
                    row = new PlayerPermissionGroupEntity
                    {
                        PlayerEntityId = PlayerId.Value,
                        GroupEntityId = group.Id,
                        ExpiresAt = expiresAt,
                    };

                    dbCtx.PlayerPermissionGroups.Add(row);
                }
                else if (row.ExpiresAt == expiresAt)
                    return (PermissionChangeResultType.Unchanged, null);
                else
                    row.ExpiresAt = expiresAt;

                dbCtx.PermissionAudit.Add(
                    Audit(PermissionAuditActionType.GroupAdded, groupName, actor, null, expiresAt)
                );

                return (
                    PermissionChangeResultType.Changed,
                    () => _state.MembershipsByGroupId[group.Id] = row.ToSnapshot()
                );
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> RemoveGroupAsync(
        string groupName,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (groupName == PermissionGroupNames.DEFAULT)
            return PermissionChangeResultType.ProtectedGroup;

        if (FindGroup(groupName) is not { } group)
            return PermissionChangeResultType.UnknownGroup;

        return await WriteAsync(
            async dbCtx =>
            {
                var row = await dbCtx.PlayerPermissionGroups.FirstOrDefaultAsync(
                    x => x.PlayerEntityId == PlayerId.Value && x.GroupEntityId == group.Id,
                    ct
                );

                if (row is null)
                    return (PermissionChangeResultType.NotFound, null);

                dbCtx.PlayerPermissionGroups.Remove(row);
                dbCtx.PermissionAudit.Add(
                    Audit(PermissionAuditActionType.GroupRemoved, groupName, actor)
                );

                return (
                    PermissionChangeResultType.Changed,
                    () => _state.MembershipsByGroupId.Remove(group.Id)
                );
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> SetNodeAsync(
        string node,
        bool value,
        DateTime? expiresAt,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (!PermissionNodeFormat.IsValidAssignment(node))
            return PermissionChangeResultType.Invalid;

        if (expiresAt <= DateTime.UtcNow)
            return PermissionChangeResultType.Expired;

        return await WriteAsync(
            async dbCtx =>
            {
                var row = await dbCtx.PlayerPermissionNodes.FirstOrDefaultAsync(
                    x => x.PlayerEntityId == PlayerId.Value && x.Node == node,
                    ct
                );

                if (row is null)
                {
                    row = new PlayerPermissionNodeEntity
                    {
                        PlayerEntityId = PlayerId.Value,
                        Node = node,
                        Value = value,
                        ExpiresAt = expiresAt,
                    };

                    dbCtx.PlayerPermissionNodes.Add(row);
                }
                else if (row.Value == value && row.ExpiresAt == expiresAt)
                    return (PermissionChangeResultType.Unchanged, null);
                else
                {
                    row.Value = value;
                    row.ExpiresAt = expiresAt;
                }

                dbCtx.PermissionAudit.Add(
                    Audit(
                        PermissionAuditActionType.NodeSet,
                        node,
                        actor,
                        PermissionAuditEntries.Format(value),
                        expiresAt
                    )
                );

                return (
                    PermissionChangeResultType.Changed,
                    () => _state.NodesByNode[node] = row.ToSnapshot()
                );
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> UnsetNodeAsync(
        string node,
        PlayerId? actor,
        CancellationToken ct
    ) =>
        !PermissionNodeFormat.IsValidAssignment(node)
            ? PermissionChangeResultType.Invalid
            : await WriteAsync(
                async dbCtx =>
                {
                    var row = await dbCtx.PlayerPermissionNodes.FirstOrDefaultAsync(
                        x => x.PlayerEntityId == PlayerId.Value && x.Node == node,
                        ct
                    );

                    if (row is null)
                        return (PermissionChangeResultType.NotFound, null);

                    dbCtx.PlayerPermissionNodes.Remove(row);
                    dbCtx.PermissionAudit.Add(
                        Audit(PermissionAuditActionType.NodeUnset, node, actor)
                    );

                    return (
                        PermissionChangeResultType.Changed,
                        () => _state.NodesByNode.Remove(node)
                    );
                },
                ct
            );

    public async Task<PermissionChangeResultType> SetMetaAsync(
        string key,
        string value,
        DateTime? expiresAt,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (!PermissionNodeFormat.IsValidNode(key) || !PermissionNodeFormat.IsValidMetaValue(value))
            return PermissionChangeResultType.Invalid;

        if (expiresAt <= DateTime.UtcNow)
            return PermissionChangeResultType.Expired;

        return await WriteAsync(
            async dbCtx =>
            {
                var row = await dbCtx.PlayerPermissionMeta.FirstOrDefaultAsync(
                    x => x.PlayerEntityId == PlayerId.Value && x.Key == key,
                    ct
                );

                if (row is null)
                {
                    row = new PlayerPermissionMetaEntity
                    {
                        PlayerEntityId = PlayerId.Value,
                        Key = key,
                        Value = value,
                        ExpiresAt = expiresAt,
                    };

                    dbCtx.PlayerPermissionMeta.Add(row);
                }
                else if (row.Value == value && row.ExpiresAt == expiresAt)
                    return (PermissionChangeResultType.Unchanged, null);
                else
                {
                    row.Value = value;
                    row.ExpiresAt = expiresAt;
                }

                dbCtx.PermissionAudit.Add(
                    Audit(PermissionAuditActionType.MetaSet, key, actor, value, expiresAt)
                );

                return (
                    PermissionChangeResultType.Changed,
                    () => _state.MetaByKey[key] = row.ToSnapshot()
                );
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> UnsetMetaAsync(
        string key,
        PlayerId? actor,
        CancellationToken ct
    ) =>
        !PermissionNodeFormat.IsValidNode(key)
            ? PermissionChangeResultType.Invalid
            : await WriteAsync(
                async dbCtx =>
                {
                    var row = await dbCtx.PlayerPermissionMeta.FirstOrDefaultAsync(
                        x => x.PlayerEntityId == PlayerId.Value && x.Key == key,
                        ct
                    );

                    if (row is null)
                        return (PermissionChangeResultType.NotFound, null);

                    dbCtx.PlayerPermissionMeta.Remove(row);
                    dbCtx.PermissionAudit.Add(
                        Audit(PermissionAuditActionType.MetaUnset, key, actor)
                    );

                    return (PermissionChangeResultType.Changed, () => _state.MetaByKey.Remove(key));
                },
                ct
            );

    public async Task<ImmutableArray<PermissionAuditSnapshot>> GetAuditAsync(
        int count,
        CancellationToken ct
    )
    {
        var take = Math.Clamp(count, 1, _permissionConfig.AuditPageLimit);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var rows = await dbCtx
            .PermissionAudit.AsNoTracking()
            .Where(x =>
                x.TargetType == PermissionAuditTargetType.Player && x.TargetId == PlayerId.Value
            )
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .ToListAsync(ct);

        return [.. rows.Select(x => x.ToSnapshot())];
    }

    public Task OnGroupsChangedAsync(PermissionGroupDirectorySnapshot groups, CancellationToken ct)
    {
        // Pushes are not awaited by the directory, so an older one can arrive after a newer one.
        if (groups.Version <= _state.Groups.Version)
            return Task.CompletedTask;

        _state.Groups = groups;

        // A deleted group's memberships were cascaded away in the database; forget them here too.
        foreach (var groupId in _state.MembershipsByGroupId.Keys.ToList())
        {
            if (!groups.Groups.ContainsKey(groupId))
                _state.MembershipsByGroupId.Remove(groupId);
        }

        Resolve();

        return Task.CompletedTask;
    }

    private PermissionGroupSnapshot? FindGroup(string name) =>
        _state.Groups.Groups.Values.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.Ordinal)
        );

    private PermissionAuditEntity Audit(
        PermissionAuditActionType action,
        string subject,
        PlayerId? actor,
        string? value = null,
        DateTime? expiresAt = null
    ) =>
        PermissionAuditEntries.Create(
            PermissionAuditTargetType.Player,
            PlayerId.Value,
            action,
            subject,
            actor,
            value,
            expiresAt
        );

    /// <summary>
    /// Runs one change against a fresh context. If it changed anything, saves the change and its
    /// audit row in one save, then applies it to memory — only once saved — and resolves.
    /// </summary>
    private async Task<PermissionChangeResultType> WriteAsync(
        Func<TurboDbContext, Task<(PermissionChangeResultType Result, Action? Apply)>> change,
        CancellationToken ct
    )
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var (result, apply) = await change(dbCtx);

        if (result != PermissionChangeResultType.Changed)
            return result;

        await dbCtx.SaveChangesAsync(ct);

        apply?.Invoke();
        Resolve();

        _logger.LogInformation("Permissions of player {PlayerId} changed", PlayerId);

        return result;
    }

    /// <summary>The resolved set, worked out again first if the registry moved or something ran out.</summary>
    private ResolvedPermissionsSnapshot EnsureResolved()
    {
        if (
            _state.Resolved is not { } resolved
            || !ReferenceEquals(_state.Registry, _permissionRegistryProvider.Current)
            || resolved.NextExpiresAt <= DateTime.UtcNow
        )
            return Resolve();

        return resolved;
    }

    private ResolvedPermissionsSnapshot Resolve()
    {
        var registry = _permissionRegistryProvider.Current;
        var resolved = PermissionResolver.Resolve(
            registry,
            _state.Groups.Groups,
            BuildAssignments(),
            DateTime.UtcNow
        );

        _state.Registry = registry;
        _state.Resolved = resolved;

        if (resolved.UnregisteredNodes.Length > 0)
            _logger.LogDebug(
                "Player {PlayerId} is assigned unregistered permission nodes {Nodes}",
                PlayerId,
                resolved.UnregisteredNodes
            );

        ScheduleExpiry(resolved.NextExpiresAt);

        return resolved;
    }

    private PlayerPermissionAssignmentsSnapshot BuildAssignments() =>
        new()
        {
            Groups = [.. _state.MembershipsByGroupId.Values],
            Nodes = [.. _state.NodesByNode.Values],
            Meta = [.. _state.MetaByKey.Values],
        };

    private static bool IsLive(DateTime? expiresAt, DateTime now) =>
        expiresAt is null || expiresAt > now;

    private void ScheduleExpiry(DateTime? next)
    {
        // An expired row the resolver skipped still has to be swept, so the earliest of those
        // counts too.
        var earliestOwn = _state
            .MembershipsByGroupId.Values.Select(x => x.ExpiresAt)
            .Concat(_state.NodesByNode.Values.Select(x => x.ExpiresAt))
            .Concat(_state.MetaByKey.Values.Select(x => x.ExpiresAt))
            .Where(x => x is not null)
            .Min();

        var due = (earliestOwn, next) switch
        {
            (null, null) => (DateTime?)null,
            (null, _) => next,
            (_, null) => earliestOwn,
            _ => earliestOwn < next ? earliestOwn : next,
        };

        if (due is null)
        {
            _expiryTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            return;
        }

        ScheduleExpiryIn(due.Value - DateTime.UtcNow);
    }

    private void ScheduleExpiryIn(TimeSpan due) =>
        _expiryTimer?.Change(
            TimeSpan.FromMilliseconds(
                Math.Clamp(due.TotalMilliseconds, 0, _permissionConfig.ExpiryCheckMaxMs)
            ),
            Timeout.InfiniteTimeSpan
        );

    /// <summary>
    /// The expiry timer: deletes the player's own rows that have run out, audits each, and
    /// resolves (which also drops group assignments that ran out; the directory sweeps those).
    /// A failure keeps the rows, which resolution ignores anyway, and tries again later.
    /// </summary>
    private async Task SweepExpiredAsync(CancellationToken ct)
    {
        try
        {
            var now = DateTime.UtcNow;

            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var groups = await dbCtx
                .PlayerPermissionGroups.Where(x =>
                    x.PlayerEntityId == PlayerId.Value && x.ExpiresAt != null && x.ExpiresAt <= now
                )
                .ToListAsync(ct);
            var nodes = await dbCtx
                .PlayerPermissionNodes.Where(x =>
                    x.PlayerEntityId == PlayerId.Value && x.ExpiresAt != null && x.ExpiresAt <= now
                )
                .ToListAsync(ct);
            var meta = await dbCtx
                .PlayerPermissionMeta.Where(x =>
                    x.PlayerEntityId == PlayerId.Value && x.ExpiresAt != null && x.ExpiresAt <= now
                )
                .ToListAsync(ct);

            if (groups.Count + nodes.Count + meta.Count > 0)
            {
                dbCtx.PlayerPermissionGroups.RemoveRange(groups);
                dbCtx.PlayerPermissionNodes.RemoveRange(nodes);
                dbCtx.PlayerPermissionMeta.RemoveRange(meta);

                foreach (var row in groups)
                    dbCtx.PermissionAudit.Add(
                        Audit(
                            PermissionAuditActionType.Expired,
                            _state.Groups.Groups.GetValueOrDefault(row.GroupEntityId)?.Name
                                ?? $"group:{row.GroupEntityId}",
                            null,
                            null,
                            row.ExpiresAt
                        )
                    );

                foreach (var row in nodes)
                    dbCtx.PermissionAudit.Add(
                        Audit(
                            PermissionAuditActionType.Expired,
                            row.Node,
                            null,
                            PermissionAuditEntries.Format(row.Value),
                            row.ExpiresAt
                        )
                    );

                foreach (var row in meta)
                    dbCtx.PermissionAudit.Add(
                        Audit(
                            PermissionAuditActionType.Expired,
                            row.Key,
                            null,
                            row.Value,
                            row.ExpiresAt
                        )
                    );

                await dbCtx.SaveChangesAsync(ct);

                foreach (var row in groups)
                    _state.MembershipsByGroupId.Remove(row.GroupEntityId);

                foreach (var row in nodes)
                    _state.NodesByNode.Remove(row.Node);

                foreach (var row in meta)
                    _state.MetaByKey.Remove(row.Key);

                _logger.LogInformation(
                    "Expired {Count} permission assignments of player {PlayerId}",
                    groups.Count + nodes.Count + meta.Count,
                    PlayerId
                );
            }

            Resolve();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to expire the permission assignments of player {PlayerId}",
                PlayerId
            );

            ScheduleExpiryIn(TimeSpan.FromMilliseconds(_permissionConfig.ExpiryRetryMs));
        }
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var groups = await dbCtx
            .PlayerPermissionGroups.AsNoTracking()
            .Where(x => x.PlayerEntityId == PlayerId.Value)
            .ToListAsync(ct);
        var nodes = await dbCtx
            .PlayerPermissionNodes.AsNoTracking()
            .Where(x => x.PlayerEntityId == PlayerId.Value)
            .ToListAsync(ct);
        var meta = await dbCtx
            .PlayerPermissionMeta.AsNoTracking()
            .Where(x => x.PlayerEntityId == PlayerId.Value)
            .ToListAsync(ct);

        _state.MembershipsByGroupId.Clear();
        _state.NodesByNode.Clear();
        _state.MetaByKey.Clear();

        foreach (var row in groups)
            _state.MembershipsByGroupId[row.GroupEntityId] = row.ToSnapshot();

        foreach (var row in nodes)
            _state.NodesByNode[row.Node] = row.ToSnapshot();

        foreach (var row in meta)
            _state.MetaByKey[row.Key] = row.ToSnapshot();
    }
}
