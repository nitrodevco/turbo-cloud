using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Movements;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;

namespace Turbo.Operations.Movements;

/// <summary>
/// The server's own consumer of the asset movement outbox: every currency movement that leaves
/// or reaches a player's wallet becomes a row of <c>currency_ledger</c>, kept for good, which the
/// outbox is not (<c>OutboxRetentionHours</c>). A movement between two wallets is a row for each
/// player. Its cursor is written with the rows, and a movement it already kept is dropped, so
/// delivering a batch again keeps each once (<see cref="IAssetMovementConsumer"/>). Ids pruned
/// before it read them are kept as a <see cref="GAP_CAUSE"/> row, so a hole in the history shows.
/// </summary>
public sealed class CurrencyLedger(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    TimeProvider timeProvider
) : IAssetMovementConsumer
{
    public const string NAME = "core.currency-ledger";

    /// <summary>
    /// The cause of a row that marks outbox ids pruned before the ledger read them: no player, no
    /// amount, the ids as its cause ref (<c>first-last</c>).
    /// </summary>
    public const string GAP_CAUSE = "ledger.gap";

    public string Name => NAME;

    public async Task<long> GetCursorAsync(CancellationToken ct)
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        return await dbCtx
            .MovementConsumerCursors.AsNoTracking()
            .Where(x => x.Name == NAME)
            .Select(x => x.Cursor)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task HandleAsync(AssetMovementBatch batch, CancellationToken ct)
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var entries = batch.Movements.SelectMany(Entries).ToList();

        // Ids pruned before the ledger read them (it was not registered while a plugin's
        // dispatcher pruned): the history has a hole, and the ledger says where.
        if (batch.Gap is { } gap)
            entries.Add(GapEntry(gap, timeProvider.GetUtcNow().UtcDateTime));

        if (entries.Count > 0)
        {
            var operations = entries.Select(x => x.OperationId).Distinct().ToList();
            var kept = await dbCtx
                .CurrencyLedger.AsNoTracking()
                .Where(x => operations.Contains(x.OperationId))
                .Select(x => new
                {
                    x.OperationId,
                    x.Seq,
                    x.PlayerId,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var seen = kept.Select(x => (x.OperationId, x.Seq, x.PlayerId)).ToHashSet();

            foreach (var entry in entries)
                if (seen.Add((entry.OperationId, entry.Seq, entry.PlayerId)))
                    dbCtx.CurrencyLedger.Add(entry);
        }

        var cursor = await dbCtx
            .MovementConsumerCursors.FirstOrDefaultAsync(x => x.Name == NAME, ct)
            .ConfigureAwait(false);

        if (cursor is null)
            dbCtx.MovementConsumerCursors.Add(
                new MovementConsumerCursorEntity { Name = NAME, Cursor = batch.Cursor }
            );
        else
            cursor.Cursor = Math.Max(cursor.Cursor, batch.Cursor);

        // One transaction: the rows and the cursor are kept together or not at all.
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>The row that marks a gap; its operation is the range, so a gap delivered twice is kept once.</summary>
    private static CurrencyLedgerEntryEntity GapEntry(OutboxGap gap, DateTime now) =>
        new()
        {
            OutboxId = gap.LastId,
            OperationId = $"{GAP_CAUSE}:{gap.FirstId}-{gap.LastId}",
            Seq = 0,
            OccurredAt = now,
            PlayerId = 0,
            Amount = 0,
            Cause = GAP_CAUSE,
            CauseRef = $"{gap.FirstId}-{gap.LastId}",
            OtherKind = PlaceKind.None,
        };

    /// <summary>What a movement did to wallets: a row for the one it left, and the one it reached.</summary>
    private static IEnumerable<CurrencyLedgerEntryEntity> Entries(OutboxMovement row)
    {
        var movement = row.Movement;

        if (movement.Asset.Kind != AssetKind.Currency || movement.Asset.Currency is not { } kind)
            yield break;

        var from = movement.From;
        var to = movement.To;

        // A wallet to the same wallet moves nothing.
        if (
            from.Kind == PlaceKind.Wallet
            && to.Kind == PlaceKind.Wallet
            && from.OwnerPlayerId == to.OwnerPlayerId
        )
            yield break;

        if (from.Kind == PlaceKind.Wallet && from.OwnerPlayerId > 0)
            yield return Entry(from.OwnerPlayerId, -movement.Amount, to);

        if (to.Kind == PlaceKind.Wallet && to.OwnerPlayerId > 0)
            yield return Entry(to.OwnerPlayerId, movement.Amount, from);

        CurrencyLedgerEntryEntity Entry(int player, long amount, AssetPlace other) =>
            new()
            {
                OutboxId = row.Id,
                OperationId = movement.OperationId,
                Seq = movement.Seq,
                OccurredAt = movement.OccurredAtUtc,
                PlayerId = player,
                CurrencyType = kind.CurrencyType,
                ActivityPointType = kind.ActivityPointType,
                Amount = amount,
                Cause = movement.Cause,
                CauseRef = movement.CauseRef,
                ActorPlayerId = movement.ActorPlayerId,
                OtherKind = other.Kind,
                OtherOwnerId = other.OwnerPlayerId,
                OtherPlaceId = other.PlaceId,
            };
    }
}
