using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// The two transaction triggers: they fire for the user whose transaction it was, when it was
/// for one of the picked contracts (or Initiate Transaction boxes, for a custom contract), or
/// for any transaction when nothing is picked.
/// </summary>
public abstract class FurnitureWiredTransactionTriggerLogic<TEvent>(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
    where TEvent : RoomEvent
{
    public override List<Type> SupportedEventTypes { get; } = [typeof(TEvent)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.PickedFurni];

    // Contracts, or the Initiate Transaction boxes of custom contracts
    // (wiredfurni.error.require_contracts_or_trade_wired; inferred from its wording).
    protected override string? RefusePickInSlot(int slot, IRoomItem item) =>
        item.Logic is FurnitureWiredContractLogic or WiredActionInitiateTransaction
            ? null
            : WiredSaveErrors.REQUIRE_CONTRACTS_OR_TRADE_WIRED;

    protected abstract RoomObjectId SourceOf(TEvent evt);

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        var picked = GetStuffIds();

        return Task.FromResult(
            evt is TEvent transaction
                && (picked.Count == 0 || picked.Contains(SourceOf(transaction).Value))
        );
    }

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(ctx.Event is TEvent);
}
