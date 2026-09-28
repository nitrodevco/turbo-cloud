using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Permissions;
using Turbo.Database.Extensions;
using Turbo.Players.Permissions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Players.Grains.Permissions;

internal sealed partial class PermissionGroupDirectoryGrain
{
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
}
