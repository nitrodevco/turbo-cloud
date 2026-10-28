using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Inventory.Configuration;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Inventory;

/// <summary>
/// Retries only delivery for already committed exchanges. The receipt is the queue, so a host
/// restart or a lost reply cannot lose the obligation. Each inventory repairs its own live state.
/// </summary>
internal sealed class FurnitureTradeRecovery(
    IDbContextFactory<TurboDbContext> database,
    IGrainFactory grains,
    IOptions<InventoryConfig> config,
    TimeProvider time,
    ILogger<FurnitureTradeRecovery> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(Math.Max(1, config.Value.TradeRecoveryIntervalSeconds))
        );
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await RecoverAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to read pending furniture trade reconciliation");
            }
        }
    }

    internal async Task RecoverAsync(CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);
        var pending = await db
            .FurnitureTradeReceipts.AsNoTracking()
            .Where(x => !x.InitiatorReconciled || !x.PartnerReconciled)
            .OrderBy(x => x.ReconcileAttemptedAt)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.OperationId)
            .Take(Math.Max(1, config.Value.TradeRecoveryBatchSize))
            .Select(x => new
            {
                x.OperationId,
                x.InitiatorId,
                x.PartnerId,
                x.InitiatorReconciled,
                x.PartnerReconciled,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var operations = pending.Select(x => x.OperationId).ToArray();
        var attemptedAt = time.GetUtcNow().UtcDateTime;
        await db
            .FurnitureTradeReceipts.Where(x => operations.Contains(x.OperationId))
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.ReconcileAttemptedAt, attemptedAt), ct)
            .ConfigureAwait(false);
        var players = pending
            .SelectMany(x =>
                new[]
                {
                    x.InitiatorReconciled ? 0 : x.InitiatorId,
                    x.PartnerReconciled ? 0 : x.PartnerId,
                }
            )
            .Where(x => x > 0)
            .Distinct();
        await Task.WhenAll(players.Select(x => ReconcileAsync(x, ct))).ConfigureAwait(false);
    }

    private async Task ReconcileAsync(PlayerId player, CancellationToken ct)
    {
        try
        {
            await grains
                .GetInventoryGrain(player)
                .ReconcileFurnitureTradesAsync(ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to reconcile committed furniture trades for player {PlayerId}; will retry",
                player
            );
        }
    }
}
