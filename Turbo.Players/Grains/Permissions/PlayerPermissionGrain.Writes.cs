using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Permissions;
using Turbo.Database.Extensions;
using Turbo.Players.Permissions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Players.Grains.Permissions;

internal sealed partial class PlayerPermissionGrain
{
    public async Task<PermissionChangeResultType> AddGroupAsync(
        string groupName,
        DateTime? expiresAt,
        PermissionExpiryModeType mode,
        PlayerId? actor,
        CancellationToken ct
    )
    {
        if (groupName == PermissionGroupNames.DEFAULT)
            return PermissionChangeResultType.ProtectedGroup;

        var now = UtcNow;

        if (expiresAt <= now)
            return PermissionChangeResultType.Expired;

        if (FindGroup(groupName) is not { } group)
            return PermissionChangeResultType.UnknownGroup;

        var temporary = expiresAt is not null;

        return await WriteAsync(
            async dbCtx =>
            {
                var (result, row) = await PermissionRowWrites.SetExpiryAsync(
                    dbCtx.PlayerPermissionGroups,
                    x =>
                        x.PlayerEntityId == PlayerId.Value
                        && x.GroupEntityId == group.Id
                        && x.IsTemporary == temporary,
                    until => new PlayerPermissionGroupEntity
                    {
                        PlayerEntityId = PlayerId.Value,
                        GroupEntityId = group.Id,
                        ExpiresAt = until,
                        IsTemporary = temporary,
                    },
                    expiresAt,
                    mode,
                    now,
                    ct
                );

                if (result != PermissionChangeResultType.Changed)
                    return (result, null);

                dbCtx.PermissionAudit.Add(
                    Audit(
                        PermissionAuditActionType.GroupAdded,
                        groupName,
                        actor,
                        null,
                        row.ExpiresAt
                    )
                );

                return (
                    result,
                    () => _state.MembershipsByGroupId[(group.Id, temporary)] = row.ToSnapshot()
                );
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> RemoveGroupAsync(
        string groupName,
        bool temporary,
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
                if (
                    await PermissionRowWrites.RemoveAsync(
                        dbCtx.PlayerPermissionGroups,
                        x =>
                            x.PlayerEntityId == PlayerId.Value
                            && x.GroupEntityId == group.Id
                            && x.IsTemporary == temporary,
                        ct
                    )
                    is not { } row
                )
                    return (PermissionChangeResultType.NotFound, null);

                dbCtx.PermissionAudit.Add(
                    Audit(
                        PermissionAuditActionType.GroupRemoved,
                        groupName,
                        actor,
                        null,
                        row.ExpiresAt
                    )
                );

                return (
                    PermissionChangeResultType.Changed,
                    () => _state.MembershipsByGroupId.Remove((group.Id, temporary))
                );
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> SetNodeAsync(
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

        var temporary = expiresAt is not null;

        return await WriteAsync(
            async dbCtx =>
            {
                var (result, row) = await PermissionRowWrites.SetAsync(
                    dbCtx.PlayerPermissionNodes,
                    x =>
                        x.PlayerEntityId == PlayerId.Value
                        && x.Node == node
                        && x.IsTemporary == temporary,
                    until => new PlayerPermissionNodeEntity
                    {
                        PlayerEntityId = PlayerId.Value,
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

                if (result != PermissionChangeResultType.Changed)
                    return (result, null);

                dbCtx.PermissionAudit.Add(
                    Audit(
                        PermissionAuditActionType.NodeSet,
                        node,
                        actor,
                        PermissionAuditEntries.Format(value),
                        row.ExpiresAt
                    )
                );

                return (result, () => _state.NodesByNode[(node, temporary)] = row.ToSnapshot());
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> UnsetNodeAsync(
        string node,
        bool temporary,
        PlayerId? actor,
        CancellationToken ct
    ) =>
        !PermissionNodeFormat.IsValidAssignment(node)
            ? PermissionChangeResultType.Invalid
            : await WriteAsync(
                async dbCtx =>
                {
                    if (
                        await PermissionRowWrites.RemoveAsync(
                            dbCtx.PlayerPermissionNodes,
                            x =>
                                x.PlayerEntityId == PlayerId.Value
                                && x.Node == node
                                && x.IsTemporary == temporary,
                            ct
                        )
                        is not { } row
                    )
                        return (PermissionChangeResultType.NotFound, null);

                    dbCtx.PermissionAudit.Add(
                        Audit(PermissionAuditActionType.NodeUnset, node, actor, null, row.ExpiresAt)
                    );

                    return (
                        PermissionChangeResultType.Changed,
                        () => _state.NodesByNode.Remove((node, temporary))
                    );
                },
                ct
            );

    public async Task<PermissionChangeResultType> SetMetaAsync(
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

        var temporary = expiresAt is not null;

        return await WriteAsync(
            async dbCtx =>
            {
                var (result, row) = await PermissionRowWrites.SetAsync(
                    dbCtx.PlayerPermissionMeta,
                    x =>
                        x.PlayerEntityId == PlayerId.Value
                        && x.Key == key
                        && x.IsTemporary == temporary,
                    until => new PlayerPermissionMetaEntity
                    {
                        PlayerEntityId = PlayerId.Value,
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

                if (result != PermissionChangeResultType.Changed)
                    return (result, null);

                dbCtx.PermissionAudit.Add(
                    Audit(PermissionAuditActionType.MetaSet, key, actor, value, row.ExpiresAt)
                );

                return (result, () => _state.MetaByKey[(key, temporary)] = row.ToSnapshot());
            },
            ct
        );
    }

    public async Task<PermissionChangeResultType> UnsetMetaAsync(
        string key,
        bool temporary,
        PlayerId? actor,
        CancellationToken ct
    ) =>
        !PermissionNodeFormat.IsValidNode(key)
            ? PermissionChangeResultType.Invalid
            : await WriteAsync(
                async dbCtx =>
                {
                    if (
                        await PermissionRowWrites.RemoveAsync(
                            dbCtx.PlayerPermissionMeta,
                            x =>
                                x.PlayerEntityId == PlayerId.Value
                                && x.Key == key
                                && x.IsTemporary == temporary,
                            ct
                        )
                        is not { } row
                    )
                        return (PermissionChangeResultType.NotFound, null);

                    dbCtx.PermissionAudit.Add(
                        Audit(PermissionAuditActionType.MetaUnset, key, actor, null, row.ExpiresAt)
                    );

                    return (
                        PermissionChangeResultType.Changed,
                        () => _state.MetaByKey.Remove((key, temporary))
                    );
                },
                ct
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

        await PublishChangesAsync(force: false, ct);

        return result;
    }
}
