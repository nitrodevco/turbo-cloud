using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Players.Permissions;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Players.Grains.Permissions;

internal sealed partial class PermissionGroupDirectoryGrain
{
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
