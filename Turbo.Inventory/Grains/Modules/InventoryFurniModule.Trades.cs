using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Trading;

namespace Turbo.Inventory.Grains.Modules;

internal sealed partial class InventoryFurniModule
{
    public async Task<bool> CommitTradeAsync(
        FurnitureTradeRequest request,
        MovementCause cause,
        CancellationToken ct
    )
    {
        if (request.InitiatorId != PlayerId)
            throw new InvalidOperationException(
                "Only the initiating inventory may commit the trade."
            );

        // A replay must reach the receipt before checking the old inventory: committed items
        // are no longer held here. Custody validates the frozen offers against the locked rows.
        var committed = await _custody.ExchangeTradeAsync(request, cause, ct);
        if (committed)
        {
            _state.FurnitureReceiveCount++;
            _state.IsFurnitureReady = false;
        }
        return committed;
    }

    /// <summary>
    /// Every furniture access checks durable obligations before trusting a cached list. Only
    /// the receipts captured before a stable reload are acknowledged; a newer trade stays pending.
    /// </summary>
    internal async Task ReconcileTradesAsync(CancellationToken ct)
    {
        var db = await _inventoryGrain._dbContextFactory.CreateDbContextAsync(ct);
        await using var dbScope = db.ConfigureAwait(true);
        var player = PlayerId.Value;
        var pending = await db
            .FurnitureTradeReceipts.AsNoTracking()
            .Where(x =>
                (x.InitiatorId == player && !x.InitiatorReconciled)
                || (x.PartnerId == player && !x.PartnerReconciled)
            )
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.OperationId)
            .Take(Math.Max(1, Config.TradeRecoveryBatchSize))
            .ToListAsync(ct);
        if (pending.Count == 0)
            return;

        _state.FurnitureReceiveCount++;
        _state.IsFurnitureReady = false;
        await LoadFurnitureAsync(ct);
        if (!_state.IsFurnitureReady)
            return;

        // Mark only received items still held now, rather than resurrecting historical offers.
        var receivedIds = pending
            .SelectMany(x =>
                JsonSerializer.Deserialize<int[]>(
                    x.InitiatorId == player ? x.PartnerItemIds : x.InitiatorItemIds
                ) ?? []
            )
            .ToHashSet();
        await MarkUnseenAsync(
            _state
                .FurnitureById.Values.Where(x => receivedIds.Contains(x.ItemId.Value))
                .Select(x => x.GetSnapshot()),
            ct
        );
        await InvalidateClientAsync(ct);
        // Receive is interleaved and may have invalidated the reload while the presence was told.
        if (!_state.IsFurnitureReady)
            return;

        var operations = pending.Select(x => x.OperationId).ToArray();
        await db
            .FurnitureTradeReceipts.Where(x =>
                operations.Contains(x.OperationId) && x.InitiatorId == player
            )
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.InitiatorReconciled, true), ct);
        await db
            .FurnitureTradeReceipts.Where(x =>
                operations.Contains(x.OperationId) && x.PartnerId == player
            )
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.PartnerReconciled, true), ct);
    }

    private async Task InvalidateClientAsync(CancellationToken ct)
    {
        await Presence.SendComposerAsync(new FurniListInvalidateEventMessageComposer(), ct);
        _state.FurnitureListSessionKey = SessionKey.Invalid;
        _state.IsFurnitureListHolderKnown = true;
    }
}
