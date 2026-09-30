using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Players.Permissions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Players.Grains.Permissions;

internal sealed partial class PlayerPermissionGrain
{
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

        ScheduleExpiryIn(due.Value - UtcNow);
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
            var now = UtcNow;

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
                    _state.MembershipsByGroupId.Remove((row.GroupEntityId, row.IsTemporary));

                foreach (var row in nodes)
                    _state.NodesByNode.Remove((row.Node, row.IsTemporary));

                foreach (var row in meta)
                    _state.MetaByKey.Remove((row.Key, row.IsTemporary));

                _logger.LogInformation(
                    "Expired {Count} permission assignments of player {PlayerId}",
                    groups.Count + nodes.Count + meta.Count,
                    PlayerId
                );
            }

            Resolve();

            // Rows swept straight after an activation may have run out while the grain was
            // collected and its player online, so the client is told even if the projection
            // looks the same as the one this activation began with.
            await PublishChangesAsync(force: groups.Count + nodes.Count + meta.Count > 0, ct);
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
}
