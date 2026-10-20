using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Movements;
using Turbo.Database.Extensions;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Registration = Turbo.Operations.Movements.AssetMovementConsumerRegistry.Registration;

namespace Turbo.Operations.Movements;

/// <summary>
/// What one dispatcher activation does on a poll: prunes the outbox, then hands each registered
/// consumer the settled rows past its cursor, and the rows found late in ids its reading skipped.
/// It keeps each consumer's cursor in memory between polls and asks the consumer again after a
/// failure, so two activations at once only deliver some rows twice, which the consumer drops.
/// With no consumer registered it reads nothing.
/// </summary>
internal sealed class AssetMovementDispatcher(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<MovementsConfig> config,
    AssetMovementConsumerRegistry registry,
    TimeProvider timeProvider,
    ILogger<AssetMovementDispatcher> logger
)
{
    private readonly MovementsConfig _config = config.Value;
    private readonly Dictionary<Registration, ConsumerState> _consumers = [];
    private DateTime _nextPruneUtc = DateTime.MinValue;

    /// <summary>One poll. False when no consumer is registered: nothing was read.</summary>
    public async Task<bool> DispatchAsync(CancellationToken ct)
    {
        var registrations = registry.Current;

        // A registration disposed (a plugin stopped or reloaded) starts over when it comes back.
        foreach (var gone in _consumers.Keys.Except(registrations).ToList())
            _consumers.Remove(gone);

        if (registrations.IsEmpty)
            return false;

        await PruneAsync(ct);

        foreach (var registration in registrations)
            await DeliverAsync(registration, ct);

        return true;
    }

    private async Task DeliverAsync(Registration registration, CancellationToken ct)
    {
        if (!_consumers.TryGetValue(registration, out var state))
        {
            state = new ConsumerState(new SkippedOutboxIds(_config.BatchSize));
            _consumers[registration] = state;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            ct,
            registration.Stopping
        );

        try
        {
            state.Cursor ??= await registration.Consumer.GetCursorAsync(linked.Token);

            await DeliverLateAsync(registration.Consumer, state, linked.Token);

            while (await DeliverNextAsync(registration.Consumer, state, linked.Token)) { }
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            // Unregistered or deactivating: the consumer's own cursor says where to resume.
            state.Cursor = null;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Asset movement consumer {Consumer} failed after outbox id {Cursor}; its rows are delivered again",
                registration.Consumer.Name,
                state.Cursor
            );

            state.Cursor = null;
        }
    }

    /// <summary>
    /// Hands over the next settled rows past the cursor. True when a full batch went and more may
    /// be waiting.
    /// </summary>
    private async Task<bool> DeliverNextAsync(
        IAssetMovementConsumer consumer,
        ConsumerState state,
        CancellationToken ct
    )
    {
        var cursor = state.Cursor!.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var settledBefore = now.AddMilliseconds(-_config.SettleMs);

        List<AssetMovementOutboxEntity> rows;

        await using (var db = await dbCtxFactory.CreateDbContextAsync(ct))
            rows = await db
                .AssetMovementOutbox.AsNoTracking()
                .Where(x => x.Id > cursor)
                .OrderBy(x => x.Id)
                .Take(_config.BatchSize)
                .ToListAsync(ct);

        var movements = ImmutableArray.CreateBuilder<OutboxMovement>(rows.Count);
        var skipped = new List<(long First, long Last)>();
        OutboxGap? gap = null;
        var next = cursor;
        var settled = 0;

        foreach (var row in rows)
        {
            // In id order up to the first unsettled row: a lower id may still be committing.
            if (row.CreatedAt > settledBefore)
                break;

            settled++;

            if (row.Cause == MovementCauses.OUTBOX_PRUNED)
            {
                // Everything up to the marker is gone.
                gap = new OutboxGap(next + 1, row.Id);
                next = row.Id;

                continue;
            }

            if (row.Id > next + 1)
                skipped.Add((next + 1, row.Id - 1));

            movements.Add(row.ToOutboxMovement());
            next = row.Id;
        }

        if (next == cursor)
            return false;

        await consumer.HandleAsync(
            new AssetMovementBatch
            {
                Movements = movements.DrainToImmutable(),
                Gap = gap,
                Cursor = next,
            },
            ct
        );

        state.Cursor = next;

        if (_config.ReReadWindowMs > 0)
            foreach (var (first, last) in skipped)
                state.Skipped.Add(first, last, now);

        return settled == _config.BatchSize;
    }

    /// <summary>Hands over rows that turned up in ids the reading skipped, below the cursor.</summary>
    private async Task DeliverLateAsync(
        IAssetMovementConsumer consumer,
        ConsumerState state,
        CancellationToken ct
    )
    {
        if (state.Skipped.IsEmpty)
            return;

        state.Skipped.Expire(
            timeProvider.GetUtcNow().UtcDateTime.AddMilliseconds(-_config.ReReadWindowMs)
        );

        if (state.Skipped.IsEmpty)
            return;

        List<AssetMovementOutboxEntity> rows;

        await using (var db = await dbCtxFactory.CreateDbContextAsync(ct))
            rows = await db
                .AssetMovementOutbox.AsNoTracking()
                .Where(state.Skipped.Predicate())
                .Where(x => x.Cause != MovementCauses.OUTBOX_PRUNED)
                .OrderBy(x => x.Id)
                .Take(_config.BatchSize)
                .ToListAsync(ct);

        if (rows.Count == 0)
            return;

        await consumer.HandleAsync(
            new AssetMovementBatch
            {
                Movements = [.. rows.Select(x => x.ToOutboxMovement())],
                Cursor = state.Cursor!.Value,
                IsLate = true,
            },
            ct
        );

        foreach (var row in rows)
            state.Skipped.Found(row.Id);
    }

    /// <summary>
    /// Deletes rows older than the retention, oldest first and a batch at a time, and turns the
    /// newest row it removes into the <see cref="MovementCauses.OUTBOX_PRUNED"/> marker (deleting
    /// the previous one), so a consumer behind it is told which ids it lost. A plugin's cursor
    /// does not hold it back, but the server's own currency ledger does while it is registered:
    /// it is where currency history is kept, so no row goes before it has read it.
    /// </summary>
    private async Task PruneAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (_config.OutboxRetentionHours <= 0 || now < _nextPruneUtc)
            return;

        try
        {
            var cutoff = now.AddHours(-_config.OutboxRetentionHours);
            var ledgerRegistered = registry.Current.Any(x =>
                x.Consumer.Name == CurrencyLedger.NAME
            );

            var pruned = await dbCtxFactory.InTransactionAsync(
                async db =>
                {
                    var keepFrom = long.MaxValue;

                    // Stored in the same save as the ledger's rows, so it is what the ledger has.
                    if (ledgerRegistered)
                        keepFrom = await db
                            .MovementConsumerCursors.AsNoTracking()
                            .Where(x => x.Name == CurrencyLedger.NAME)
                            .Select(x => x.Cursor)
                            .FirstOrDefaultAsync(ct);

                    var ids = await db
                        .AssetMovementOutbox.AsNoTracking()
                        .Where(x =>
                            x.CreatedAt < cutoff
                            && x.Id <= keepFrom
                            && x.Cause != MovementCauses.OUTBOX_PRUNED
                        )
                        .OrderBy(x => x.Id)
                        .Select(x => x.Id)
                        .Take(_config.BatchSize)
                        .ToListAsync(ct);

                    if (ids.Count == 0)
                        return UnitOfWork.Commit(0);

                    var last = ids[^1];

                    await db.AssetMovementOutbox.Where(x => x.Id < last).ExecuteDeleteAsync(ct);
                    await db
                        .AssetMovementOutbox.Where(x => x.Id == last)
                        .ExecuteUpdateAsync(
                            up =>
                                up.SetProperty(
                                        x => x.OperationId,
                                        Guid.CreateVersion7(now).ToString("N")
                                    )
                                    .SetProperty(x => x.Seq, (short)0)
                                    .SetProperty(x => x.OccurredAt, now)
                                    .SetProperty(x => x.AssetKind, AssetKind.Furni)
                                    .SetProperty(x => x.AssetId, 0L)
                                    .SetProperty(x => x.DefinitionId, 0)
                                    .SetProperty(x => x.CurrencyType, (CurrencyType?)null)
                                    .SetProperty(x => x.ActivityPointType, (int?)null)
                                    .SetProperty(x => x.FromOwner, 0)
                                    .SetProperty(x => x.FromKind, PlaceKind.None)
                                    .SetProperty(x => x.FromPlace, 0L)
                                    .SetProperty(x => x.ToOwner, 0)
                                    .SetProperty(x => x.ToKind, PlaceKind.None)
                                    .SetProperty(x => x.ToPlace, 0L)
                                    .SetProperty(x => x.X, (int?)null)
                                    .SetProperty(x => x.Y, (int?)null)
                                    .SetProperty(x => x.Z, (double?)null)
                                    .SetProperty(x => x.Rotation, (Rotation?)null)
                                    .SetProperty(x => x.WallOffset, (int?)null)
                                    .SetProperty(x => x.Amount, 0L)
                                    .SetProperty(x => x.Actor, 0)
                                    .SetProperty(x => x.Cause, MovementCauses.OUTBOX_PRUNED)
                                    .SetProperty(x => x.CauseRef, (string?)null)
                                    .SetProperty(x => x.SnapshotExtraData, (string?)null),
                            ct
                        );

                    return UnitOfWork.Commit(ids.Count);
                },
                ct
            );

            // A full batch means more are due now; otherwise the next row ages out within the
            // hour the retention is counted in.
            _nextPruneUtc = pruned < _config.BatchSize ? now.AddHours(1) : now;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to prune the asset movement outbox");
        }
    }

    private sealed class ConsumerState(SkippedOutboxIds skipped)
    {
        /// <summary>Where the consumer is; null until it is asked, and again after a failure.</summary>
        public long? Cursor { get; set; }

        public SkippedOutboxIds Skipped { get; } = skipped;
    }
}
