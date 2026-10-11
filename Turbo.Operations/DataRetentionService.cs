using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Operations;

/// <summary>
/// Removes records past their keeping period (<see cref="RetentionConfig"/>), oldest first, a
/// batch at a time. A pending respect is never removed, being work the recovery service has yet
/// to finish.
/// </summary>
public sealed class DataRetentionService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<RetentionConfig> config,
    TimeProvider timeProvider,
    ILogger<DataRetentionService> logger
) : BackgroundService
{
    private readonly RetentionConfig _config = config.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(Math.Max(1, _config.IntervalMinutes))
        );

        do
        {
            try
            {
                await PurgeAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Removing records past their keeping period failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    /// <summary>One pass over every kind of record; returns how many rows each lost.</summary>
    public async Task<IReadOnlyDictionary<string, int>> PurgeAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var removed = new Dictionary<string, int>();

        async Task RuleAsync(
            string name,
            int days,
            Func<TurboDbContext, DateTime, int, CancellationToken, Task<int>> deleteBatch
        )
        {
            if (days <= 0)
                return;

            var cutoff = now.AddDays(-days);
            var total = 0;

            while (true)
            {
                var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
                await using var scope = db.ConfigureAwait(false);
                var deleted = await deleteBatch(db, cutoff, _config.BatchSize, ct)
                    .ConfigureAwait(false);

                total += deleted;

                if (deleted < _config.BatchSize)
                    break;
            }

            if (total > 0)
            {
                removed[name] = total;
                logger.LogInformation(
                    "Removed {Count} {Records} older than {Days} days",
                    total,
                    name,
                    days
                );
            }
        }

        // Each takes the oldest first, by an id that grows with time, so a batch is the front
        // of the table's own index rather than a scan of it.
        await RuleAsync(
                "room chat lines",
                _config.ChatDays,
                (db, cutoff, n, c) =>
                    db
                        .Chatlogs.Where(x => x.CreatedAt < cutoff)
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "private messages",
                _config.ChatDays,
                (db, cutoff, n, c) =>
                    db
                        .MessengerMessages.Where(x => x.CreatedAt < cutoff)
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "room entries",
                _config.RoomEntryDays,
                (db, cutoff, n, c) =>
                    db
                        .RoomEntryLogs.Where(x => x.CreatedAt < cutoff)
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "logins",
                _config.LoginDays,
                (db, cutoff, n, c) =>
                    db
                        .PlayerLogins.Where(x => x.CreatedAt < cutoff)
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "currency ledger rows",
                _config.CurrencyLedgerDays,
                (db, cutoff, n, c) =>
                    db
                        .PlayerCurrencyLedger.Where(x => x.CreatedAt < cutoff)
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "furni transfers",
                _config.FurnitureTransferDays,
                (db, cutoff, n, c) =>
                    db
                        .FurnitureTransfers.Where(x => x.CreatedAt < cutoff)
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "command uses",
                _config.CommandLogDays,
                (db, cutoff, n, c) =>
                    db
                        .CommandLogs.Where(x => x.CreatedAt < cutoff)
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "respects",
                _config.RespectDays,
                (db, cutoff, n, c) =>
                    db
                        .PlayerRespects.Where(x =>
                            x.CreatedAt < cutoff && x.Status != RespectStatus.Pending
                        )
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "pet respects",
                _config.ReceiptDays,
                (db, cutoff, n, c) =>
                    db
                        .PetRespects.Where(x =>
                            x.CreatedAt < cutoff && x.Status != RespectStatus.Pending
                        )
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "respect counter changes",
                _config.ReceiptDays,
                (db, cutoff, n, c) =>
                    db
                        .PlayerRespectChanges.Where(x => x.CreatedAt < cutoff)
                        .OrderBy(x => x.RespectId)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "pet feedings",
                _config.ReceiptDays,
                (db, cutoff, n, c) =>
                    db
                        .PetFeedings.Where(x => x.CreatedAt < cutoff)
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "referenced credit receipts",
                _config.ReceiptDays,
                (db, cutoff, n, c) =>
                    db
                        .WalletCreditReceipts.Where(x => x.CreatedAt < cutoff)
                        .OrderBy(x => x.CreatedAt)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "past daily tasks",
                _config.DailyTaskDays,
                (db, cutoff, n, c) =>
                    db
                        .PlayerDailyTasks.Where(x => x.DayStartsAt < cutoff)
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "wired chest transactions",
                _config.WiredChestDays,
                (db, cutoff, n, c) =>
                    db
                        .WiredChestTransactions.Where(x => x.CreatedAt < cutoff)
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "closed calls for help",
                _config.ClosedReportDays,
                (db, cutoff, n, c) =>
                    db
                        .CfhReports.Where(x => x.ClosedAt != null && x.ClosedAt < cutoff)
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "expired room bans",
                _config.ExpiredRoomSanctionDays,
                (db, cutoff, n, c) =>
                    db
                        .RoomBans.Where(x => x.DateExpires < cutoff)
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "expired room mutes",
                _config.ExpiredRoomSanctionDays,
                (db, cutoff, n, c) =>
                    db
                        .RoomMutes.Where(x => x.DateExpires < cutoff)
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "ended room events",
                _config.EndedRoomEventDays,
                (db, cutoff, n, c) =>
                    db
                        .RoomEvents.Where(x => x.ExpiresAt < cutoff)
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);
        await RuleAsync(
                "asset publish runs",
                _config.AssetPublishDays,
                (db, cutoff, n, c) =>
                    db
                        .AssetPublishes.Where(x => x.CreatedAt < cutoff)
                        .OrderBy(x => x.Id)
                        .Take(n)
                        .ExecuteDeleteAsync(c)
            )
            .ConfigureAwait(false);

        if (_config.AutomaticCatalogBackupsKept > 0)
        {
            var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var scope = db.ConfigureAwait(false);
            var deleted = await db
                .CatalogBackups.Where(x => x.Automatic)
                .OrderByDescending(x => x.Id)
                .Skip(_config.AutomaticCatalogBackupsKept)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);

            if (deleted > 0)
                removed["automatic catalogue backups"] = deleted;
        }

        return removed;
    }
}
