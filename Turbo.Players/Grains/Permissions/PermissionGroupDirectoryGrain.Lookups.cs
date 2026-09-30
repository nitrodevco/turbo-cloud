using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Extensions;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Players.Grains.Permissions;

internal sealed partial class PermissionGroupDirectoryGrain
{
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
}
