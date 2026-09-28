using System;
using System.Collections.Generic;
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
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Players.Grains.Permissions;

/// <summary>
/// Every permission group, one grain for the hotel. Write-through: each change is saved and
/// audited before the in-memory snapshot moves, so there is nothing to flush on deactivation.
/// After a change the whole snapshot is replaced and pushed to every active
/// <see cref="IPlayerPermissionGrain"/>, which re-resolves from memory. Group edits are rare
/// operator actions, so telling every subscriber rather than working out who is affected costs
/// nothing that matters. Kept alive because every player permission grain reads it on
/// activation.
/// </summary>
[KeepAlive]
internal sealed class PermissionGroupDirectoryGrain : Grain, IPermissionGroupDirectoryGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly PermissionConfig _permissionConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<IPermissionGroupDirectoryGrain> _logger;

    private readonly PermissionGroupDirectoryLiveState _state = new();

    private IGrainTimer? _expiryTimer;

    public PermissionGroupDirectoryGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<PlayerConfig> playerConfig,
        IGrainFactory grainFactory,
        TimeProvider timeProvider,
        ILogger<IPermissionGroupDirectoryGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _permissionConfig = playerConfig.Value.Permissions;
        _grainFactory = grainFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate the permission group directory");

            throw;
        }

        // Idle until ScheduleExpiry sets it; an assignment already past its expiry makes it fire
        // straight away, which is what cleans up after a restart.
        _expiryTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) =>
                await ((PermissionGroupDirectoryGrain)self!).SweepExpiredAsync(ct),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan
        );

        ScheduleExpiry();
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _expiryTimer?.Dispose();
        _expiryTimer = null;

        return Task.CompletedTask;
    }

    public Task<PermissionGroupDirectorySnapshot> GetSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(_state.Snapshot);

    public Task SubscribeAsync(PlayerId playerId, CancellationToken ct)
    {
        _state.Subscribers.Add(playerId);

        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(PlayerId playerId, CancellationToken ct)
    {
        _state.Subscribers.Remove(playerId);

        return Task.CompletedTask;
    }

    public async Task<int> ReloadAsync(CancellationToken ct)
    {
        await HydrateAsync(ct);

        // Not awaited, as with a push: one player failing to read its rows keeps its old ones
        // until its next reload or activation, and must not stop the rest.
        foreach (var playerId in _state.Subscribers)
            _grainFactory
                .GetPlayerPermissionGrain(playerId)
                .ReloadAsync(CancellationToken.None)
                .LogAndForget(_logger, "reload the permissions of player {PlayerId}", playerId);

        _logger.LogInformation(
            "Reloaded {GroupCount} permission groups and {PlayerCount} active players from the database",
            _state.Snapshot.Groups.Count,
            _state.Subscribers.Count
        );

        return _state.Subscribers.Count;
    }

    public async Task<PermissionChangeResultType> CreateGroupAsync(
        string name,
        string displayName,
        int weight,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (
            !PermissionGroupNames.IsValid(name)
            || !PermissionGroupNames.IsValidDisplayName(displayName)
        )
            return PermissionChangeResultType.Invalid;

        if (FindGroup(name) is not null)
            return PermissionChangeResultType.AlreadyExists;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);
        await using var transaction = await dbCtx.Database.BeginTransactionAsync(ct);

        var entity = new PermissionGroupEntity
        {
            Name = name,
            DisplayName = displayName,
            Weight = weight,
        };

        dbCtx.PermissionGroups.Add(entity);

        // The audit row names the group by id, which only exists once the group is saved.
        await dbCtx.SaveChangesAsync(ct);

        dbCtx.PermissionAudit.Add(
            PermissionAuditEntries.Create(
                PermissionAuditTargetType.Group,
                entity.Id,
                PermissionAuditActionType.GroupCreated,
                name,
                actor,
                PermissionAuditEntries.Format(weight)
            )
        );

        await dbCtx.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        await ReloadGroupAsync(dbCtx, entity.Id, ct);

        _logger.LogInformation(
            "Permission group {GroupName} created with weight {Weight} by {Actor}",
            name,
            weight,
            actor
        );

        return PermissionChangeResultType.Changed;
    }

    public async Task<PermissionChangeResultType> DeleteGroupAsync(
        string name,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (name == PermissionGroupNames.DEFAULT)
            return PermissionChangeResultType.ProtectedGroup;

        if (FindGroup(name) is not { } group)
            return PermissionChangeResultType.UnknownGroup;

        return await WriteAsync(
            group,
            async dbCtx =>
            {
                var entity = await dbCtx.PermissionGroups.FirstOrDefaultAsync(
                    x => x.Id == group.Id,
                    ct
                );

                if (entity is null)
                    return PermissionChangeResultType.UnknownGroup;

                // The database cascades the group's nodes, meta, parent links and memberships.
                dbCtx.PermissionGroups.Remove(entity);
                dbCtx.PermissionAudit.Add(
                    PermissionAuditEntries.Create(
                        PermissionAuditTargetType.Group,
                        group.Id,
                        PermissionAuditActionType.GroupDeleted,
                        name,
                        actor
                    )
                );

                return PermissionChangeResultType.Changed;
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> SetWeightAsync(
        string name,
        int weight,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (FindGroup(name) is not { } group)
            return PermissionChangeResultType.UnknownGroup;

        if (group.Weight == weight)
            return PermissionChangeResultType.Unchanged;

        return await UpdateGroupRowAsync(
            group,
            entity => entity.Weight = weight,
            PermissionAuditActionType.GroupReweighted,
            PermissionAuditEntries.Format(weight),
            actor,
            ct
        );
    }

    public async Task<PermissionChangeResultType> SetDisplayNameAsync(
        string name,
        string displayName,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (!PermissionGroupNames.IsValidDisplayName(displayName))
            return PermissionChangeResultType.Invalid;

        if (FindGroup(name) is not { } group)
            return PermissionChangeResultType.UnknownGroup;

        if (group.DisplayName == displayName)
            return PermissionChangeResultType.Unchanged;

        return await UpdateGroupRowAsync(
            group,
            entity => entity.DisplayName = displayName,
            PermissionAuditActionType.GroupRenamed,
            displayName,
            actor,
            ct
        );
    }

    public async Task<PermissionChangeResultType> SetNodeAsync(
        string name,
        string node,
        bool value,
        DateTime? expiresAt,
        PermissionExpiryModeType mode,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (!PermissionNodeFormat.IsValidAssignment(node))
            return PermissionChangeResultType.Invalid;

        if (PermissionGroupNames.IsGroupNode(node))
            return PermissionChangeResultType.ReservedNode;

        var now = UtcNow;

        if (expiresAt <= now)
            return PermissionChangeResultType.Expired;

        if (FindGroup(name) is not { } group)
            return PermissionChangeResultType.UnknownGroup;

        var temporary = expiresAt is not null;

        return await WriteAsync(
            group,
            async dbCtx =>
            {
                var (result, row) = await PermissionRowWrites.SetAsync(
                    dbCtx.PermissionGroupNodes,
                    x =>
                        x.GroupEntityId == group.Id && x.Node == node && x.IsTemporary == temporary,
                    until => new PermissionGroupNodeEntity
                    {
                        GroupEntityId = group.Id,
                        Node = node,
                        Value = value,
                        ExpiresAt = until,
                        IsTemporary = temporary,
                    },
                    value,
                    expiresAt,
                    mode,
                    now,
                    ct
                );

                if (result == PermissionChangeResultType.Changed)
                    dbCtx.PermissionAudit.Add(
                        Audit(
                            group,
                            PermissionAuditActionType.NodeSet,
                            node,
                            actor,
                            PermissionAuditEntries.Format(value),
                            row.ExpiresAt
                        )
                    );

                return result;
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> UnsetNodeAsync(
        string name,
        string node,
        bool temporary,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (!PermissionNodeFormat.IsValidAssignment(node))
            return PermissionChangeResultType.Invalid;

        if (FindGroup(name) is not { } group)
            return PermissionChangeResultType.UnknownGroup;

        return await WriteAsync(
            group,
            async dbCtx =>
            {
                if (
                    await PermissionRowWrites.RemoveAsync(
                        dbCtx.PermissionGroupNodes,
                        x =>
                            x.GroupEntityId == group.Id
                            && x.Node == node
                            && x.IsTemporary == temporary,
                        ct
                    )
                    is null
                )
                    return PermissionChangeResultType.NotFound;

                dbCtx.PermissionAudit.Add(
                    Audit(group, PermissionAuditActionType.NodeUnset, node, actor)
                );

                return PermissionChangeResultType.Changed;
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> SetMetaAsync(
        string name,
        string key,
        string value,
        DateTime? expiresAt,
        PermissionExpiryModeType mode,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (!PermissionNodeFormat.IsValidNode(key) || !PermissionNodeFormat.IsValidMetaValue(value))
            return PermissionChangeResultType.Invalid;

        var now = UtcNow;

        if (expiresAt <= now)
            return PermissionChangeResultType.Expired;

        if (FindGroup(name) is not { } group)
            return PermissionChangeResultType.UnknownGroup;

        var temporary = expiresAt is not null;

        return await WriteAsync(
            group,
            async dbCtx =>
            {
                var (result, row) = await PermissionRowWrites.SetAsync(
                    dbCtx.PermissionGroupMeta,
                    x => x.GroupEntityId == group.Id && x.Key == key && x.IsTemporary == temporary,
                    until => new PermissionGroupMetaEntity
                    {
                        GroupEntityId = group.Id,
                        Key = key,
                        Value = value,
                        ExpiresAt = until,
                        IsTemporary = temporary,
                    },
                    value,
                    expiresAt,
                    mode,
                    now,
                    ct
                );

                if (result == PermissionChangeResultType.Changed)
                    dbCtx.PermissionAudit.Add(
                        Audit(
                            group,
                            PermissionAuditActionType.MetaSet,
                            key,
                            actor,
                            value,
                            row.ExpiresAt
                        )
                    );

                return result;
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> UnsetMetaAsync(
        string name,
        string key,
        bool temporary,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (!PermissionNodeFormat.IsValidNode(key))
            return PermissionChangeResultType.Invalid;

        if (FindGroup(name) is not { } group)
            return PermissionChangeResultType.UnknownGroup;

        return await WriteAsync(
            group,
            async dbCtx =>
            {
                if (
                    await PermissionRowWrites.RemoveAsync(
                        dbCtx.PermissionGroupMeta,
                        x =>
                            x.GroupEntityId == group.Id
                            && x.Key == key
                            && x.IsTemporary == temporary,
                        ct
                    )
                    is null
                )
                    return PermissionChangeResultType.NotFound;

                dbCtx.PermissionAudit.Add(
                    Audit(group, PermissionAuditActionType.MetaUnset, key, actor)
                );

                return PermissionChangeResultType.Changed;
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> AddParentAsync(
        string name,
        string parentName,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (FindGroup(name) is not { } group || FindGroup(parentName) is not { } parent)
            return PermissionChangeResultType.UnknownGroup;

        if (group.ParentIds.Contains(parent.Id))
            return PermissionChangeResultType.Unchanged;

        if (group.Id == parent.Id || Inherits(parent.Id, group.Id))
            return PermissionChangeResultType.WouldCycle;

        return await WriteAsync(
            group,
            dbCtx =>
            {
                dbCtx.PermissionGroupParents.Add(
                    new PermissionGroupParentEntity
                    {
                        GroupEntityId = group.Id,
                        ParentGroupEntityId = parent.Id,
                    }
                );
                dbCtx.PermissionAudit.Add(
                    PermissionAuditEntries.Create(
                        PermissionAuditTargetType.Group,
                        group.Id,
                        PermissionAuditActionType.ParentAdded,
                        parentName,
                        actor
                    )
                );

                return Task.FromResult(PermissionChangeResultType.Changed);
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> RemoveParentAsync(
        string name,
        string parentName,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (FindGroup(name) is not { } group || FindGroup(parentName) is not { } parent)
            return PermissionChangeResultType.UnknownGroup;

        return await WriteAsync(
            group,
            async dbCtx =>
            {
                var row = await dbCtx.PermissionGroupParents.FirstOrDefaultAsync(
                    x => x.GroupEntityId == group.Id && x.ParentGroupEntityId == parent.Id,
                    ct
                );

                if (row is null)
                    return PermissionChangeResultType.NotFound;

                dbCtx.PermissionGroupParents.Remove(row);
                dbCtx.PermissionAudit.Add(
                    PermissionAuditEntries.Create(
                        PermissionAuditTargetType.Group,
                        group.Id,
                        PermissionAuditActionType.ParentRemoved,
                        parentName,
                        actor
                    )
                );

                return PermissionChangeResultType.Changed;
            },
            ct
        );
    }

    public async Task<ImmutableArray<PermissionAuditSnapshot>> GetAuditAsync(
        string name,
        int count,
        CancellationToken ct
    )
    {
        if (FindGroup(name) is not { } group)
            return [];

        var take = Math.Clamp(count, 1, _permissionConfig.AuditPageLimit);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var rows = await dbCtx
            .PermissionAudit.AsNoTracking()
            .Where(x => x.TargetType == PermissionAuditTargetType.Group && x.TargetId == group.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .ToListAsync(ct);

        return [.. rows.Select(x => x.ToSnapshot())];
    }

    public async Task<ImmutableArray<PermissionGroupMemberSnapshot>> GetMembersAsync(
        string name,
        int count,
        CancellationToken ct
    )
    {
        if (FindGroup(name) is not { } group || group.Name == PermissionGroupNames.DEFAULT)
            return [];

        var take = Math.Clamp(count, 1, _permissionConfig.LookupPageLimit);
        var now = UtcNow;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var members = await dbCtx
            .PlayerPermissionGroups.AsNoTracking()
            .Where(x => x.GroupEntityId == group.Id && (x.ExpiresAt == null || x.ExpiresAt > now))
            .OrderBy(x => x.IsTemporary)
            .ThenBy(x => x.PlayerEntityId)
            .Take(take)
            .Select(x => new PermissionGroupMemberSnapshot
            {
                PlayerId = x.PlayerEntityId,
                ExpiresAt = x.ExpiresAt,
            })
            .ToListAsync(ct);

        return [.. members];
    }

    public async Task<ImmutableArray<PermissionNodeHolderSnapshot>> FindNodeHoldersAsync(
        string node,
        int count,
        CancellationToken ct
    )
    {
        if (!PermissionNodeFormat.IsValidNode(node))
            return [];

        var take = Math.Clamp(count, 1, _permissionConfig.LookupPageLimit);
        var now = UtcNow;

        bool Names(PermissionNodeAssignmentSnapshot assignment) =>
            (assignment.ExpiresAt is null || assignment.ExpiresAt > now)
            && PermissionNodeFormat.Specificity(assignment.Node, node)
                != PermissionNodeFormat.NO_MATCH;

        var holders = ImmutableArray.CreateBuilder<PermissionNodeHolderSnapshot>();

        foreach (var group in _state.Snapshot.Groups.Values.OrderByDescending(x => x.Weight))
        {
            foreach (var assignment in group.Nodes.Where(Names))
                holders.Add(
                    new PermissionNodeHolderSnapshot
                    {
                        TargetType = PermissionAuditTargetType.Group,
                        TargetId = group.Id,
                        Assignment = assignment,
                    }
                );
        }

        if (holders.Count >= take)
            return holders.Take(take).ToImmutableArray();

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        // The node itself, or any wildcard, which is filtered to the ones covering it here: SQL
        // cannot say "a prefix of this node". Wildcards on players are rare, so the rows are few.
        var rows = await dbCtx
            .PlayerPermissionNodes.AsNoTracking()
            .Where(x =>
                (x.ExpiresAt == null || x.ExpiresAt > now)
                && (
                    x.Node == node
                    || x.Node == PermissionNodeFormat.WILDCARD
                    || x.Node.EndsWith(".*")
                )
            )
            .OrderBy(x => x.PlayerEntityId)
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            var assignment = row.ToSnapshot();

            if (!Names(assignment))
                continue;

            holders.Add(
                new PermissionNodeHolderSnapshot
                {
                    TargetType = PermissionAuditTargetType.Player,
                    TargetId = row.PlayerEntityId,
                    Assignment = assignment,
                }
            );

            if (holders.Count >= take)
                break;
        }

        return holders.ToImmutable();
    }

    public async Task<ImmutableArray<PermissionAuditSnapshot>> GetRecentAuditAsync(
        string? search,
        int count,
        CancellationToken ct
    )
    {
        var take = Math.Clamp(count, 1, _permissionConfig.AuditPageLimit);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var query = dbCtx.PermissionAudit.AsNoTracking();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(x => x.Subject.Contains(search));

        // Append-only, so the key orders rows as they were written and needs no other index.
        var rows = await query.OrderByDescending(x => x.Id).Take(take).ToListAsync(ct);

        return [.. rows.Select(x => x.ToSnapshot())];
    }

    private static PermissionAuditEntity Audit(
        PermissionGroupSnapshot group,
        PermissionAuditActionType action,
        string subject,
        PlayerId? actor,
        string? value = null,
        DateTime? expiresAt = null
    ) =>
        PermissionAuditEntries.Create(
            PermissionAuditTargetType.Group,
            group.Id,
            action,
            subject,
            actor,
            value,
            expiresAt
        );

    private PermissionGroupSnapshot? FindGroup(string name) =>
        _state.Snapshot.Groups.Values.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.Ordinal)
        );

    /// <summary>Whether <paramref name="fromId"/> inherits, at any depth, from <paramref name="ancestorId"/>.</summary>
    private bool Inherits(int fromId, int ancestorId)
    {
        var groups = _state.Snapshot.Groups;
        var visited = new HashSet<int>();
        var pending = new Stack<int>([fromId]);

        while (pending.Count > 0)
        {
            if (!groups.TryGetValue(pending.Pop(), out var group) || !visited.Add(group.Id))
                continue;

            foreach (var parentId in group.ParentIds)
            {
                if (parentId == ancestorId)
                    return true;

                pending.Push(parentId);
            }
        }

        return false;
    }

    /// <summary>
    /// Runs one change against a fresh context, and if it changed anything saves it — the
    /// change and its audit row in one save — then reloads the group and publishes.
    /// </summary>
    private async Task<PermissionChangeResultType> WriteAsync(
        PermissionGroupSnapshot group,
        Func<TurboDbContext, Task<PermissionChangeResultType>> change,
        CancellationToken ct
    )
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var result = await change(dbCtx);

        if (result != PermissionChangeResultType.Changed)
            return result;

        await dbCtx.SaveChangesAsync(ct);
        await ReloadGroupAsync(dbCtx, group.Id, ct);

        _logger.LogInformation("Permission group {GroupName} changed", group.Name);

        return result;
    }

    private Task<PermissionChangeResultType> UpdateGroupRowAsync(
        PermissionGroupSnapshot group,
        Action<PermissionGroupEntity> update,
        PermissionAuditActionType action,
        string value,
        PlayerId? actor,
        CancellationToken ct
    ) =>
        WriteAsync(
            group,
            async dbCtx =>
            {
                var entity = await dbCtx.PermissionGroups.FirstOrDefaultAsync(
                    x => x.Id == group.Id,
                    ct
                );

                if (entity is null)
                    return PermissionChangeResultType.UnknownGroup;

                update(entity);
                dbCtx.PermissionAudit.Add(
                    PermissionAuditEntries.Create(
                        PermissionAuditTargetType.Group,
                        group.Id,
                        action,
                        group.Name,
                        actor,
                        value
                    )
                );

                return PermissionChangeResultType.Changed;
            },
            ct
        );

    /// <summary>Reads one group back after a change, or drops it if it is gone, and publishes.</summary>
    private async Task ReloadGroupAsync(TurboDbContext dbCtx, int groupId, CancellationToken ct)
    {
        var entity = await GroupsQuery(dbCtx).FirstOrDefaultAsync(x => x.Id == groupId, ct);

        var groups = entity is null
            ? _state.Snapshot.Groups.Remove(groupId)
            : _state.Snapshot.Groups.SetItem(groupId, entity.ToSnapshot());

        Publish(groups);
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await GroupsQuery(dbCtx).ToListAsync(ct);

        Publish(entities.Select(x => x.ToSnapshot()).ToImmutableDictionary(x => x.Id));
    }

    private static IQueryable<PermissionGroupEntity> GroupsQuery(TurboDbContext dbCtx) =>
        dbCtx
            .PermissionGroups.AsNoTracking()
            .Include(x => x.Parents)
            .Include(x => x.Nodes)
            .Include(x => x.Meta)
            .AsSplitQuery();

    /// <summary>Replaces the snapshot and tells every active player permission grain.</summary>
    private void Publish(ImmutableDictionary<int, PermissionGroupSnapshot> groups)
    {
        var snapshot = new PermissionGroupDirectorySnapshot
        {
            Version = _state.Snapshot.Version + 1,
            Groups = groups,
        };

        _state.Snapshot = snapshot;

        ScheduleExpiry();

        // Not awaited: a player grain resolving must not hold up the next group edit, and one
        // that fails keeps its previous groups until the next push or its next activation.
        foreach (var playerId in _state.Subscribers)
            _grainFactory
                .GetPlayerPermissionGrain(playerId)
                .OnGroupsChangedAsync(snapshot, CancellationToken.None)
                .LogAndForget(_logger, "push permission groups to player {PlayerId}", playerId);
    }

    /// <summary>Sets the expiry timer for the earliest group node or meta expiry, or idles it.</summary>
    private void ScheduleExpiry()
    {
        var next = _state
            .Snapshot.Groups.Values.SelectMany(x =>
                x.Nodes.Select(n => n.ExpiresAt).Concat(x.Meta.Select(m => m.ExpiresAt))
            )
            .Where(x => x is not null)
            .Min();

        if (next is null)
        {
            _expiryTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            return;
        }

        ScheduleExpiryIn(next.Value - UtcNow);
    }

    private void ScheduleExpiryIn(TimeSpan due) =>
        _expiryTimer?.Change(
            TimeSpan.FromMilliseconds(
                Math.Clamp(due.TotalMilliseconds, 0, _permissionConfig.ExpiryCheckMaxMs)
            ),
            Timeout.InfiniteTimeSpan
        );

    /// <summary>
    /// The expiry timer: deletes group nodes and meta that have run out, audits each, and
    /// publishes. A failure keeps the rows (resolution ignores them anyway) and tries again later.
    /// </summary>
    private async Task SweepExpiredAsync(CancellationToken ct)
    {
        try
        {
            var now = UtcNow;

            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var nodes = await dbCtx
                .PermissionGroupNodes.Where(x => x.ExpiresAt != null && x.ExpiresAt <= now)
                .ToListAsync(ct);
            var meta = await dbCtx
                .PermissionGroupMeta.Where(x => x.ExpiresAt != null && x.ExpiresAt <= now)
                .ToListAsync(ct);

            if (nodes.Count == 0 && meta.Count == 0)
            {
                ScheduleExpiry();

                return;
            }

            dbCtx.PermissionGroupNodes.RemoveRange(nodes);
            dbCtx.PermissionGroupMeta.RemoveRange(meta);

            foreach (var row in nodes)
                dbCtx.PermissionAudit.Add(
                    PermissionAuditEntries.Create(
                        PermissionAuditTargetType.Group,
                        row.GroupEntityId,
                        PermissionAuditActionType.Expired,
                        row.Node,
                        null,
                        PermissionAuditEntries.Format(row.Value),
                        row.ExpiresAt
                    )
                );

            foreach (var row in meta)
                dbCtx.PermissionAudit.Add(
                    PermissionAuditEntries.Create(
                        PermissionAuditTargetType.Group,
                        row.GroupEntityId,
                        PermissionAuditActionType.Expired,
                        row.Key,
                        null,
                        row.Value,
                        row.ExpiresAt
                    )
                );

            await dbCtx.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Expired {NodeCount} permission group nodes and {MetaCount} meta values",
                nodes.Count,
                meta.Count
            );

            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to expire permission group assignments");

            ScheduleExpiryIn(TimeSpan.FromMilliseconds(_permissionConfig.ExpiryRetryMs));
        }
    }
}
