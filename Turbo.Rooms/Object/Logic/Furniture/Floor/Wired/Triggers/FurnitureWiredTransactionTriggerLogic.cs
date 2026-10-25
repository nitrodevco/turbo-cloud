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
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// The two transaction triggers: they fire for the user whose transaction it was, when it was
/// for one of the picked contracts (custom contract addons too), or, for those that do, for any
/// transaction when nothing is picked (<see cref="FiresWithoutPicks"/>).
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

    // Contracts, custom ones too; anything else, the Initiate Transaction box included, is
    // refused with wiredfurni.error.require_contracts_or_trade_wired (official client capture
    // 2026-10-22, Transaction Completed: custom contract and wf_contract_trade/_payment saved,
    // a lamp and an Initiate Transaction box refused).
    protected override string? RefusePickInSlot(int slot, IRoomItem item) =>
        item.Logic is FurnitureWiredContractLogic or IWiredContractSource
            ? null
            : WiredSaveErrors.REQUIRE_CONTRACTS_OR_TRADE_WIRED;

    protected abstract RoomObjectId SourceOf(TEvent evt);

    /// <summary>Whether the trigger fires for any transaction while it picks no contract.</summary>
    protected virtual bool FiresWithoutPicks => true;

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        var picked = GetStuffIds();

        return Task.FromResult(
            evt is TEvent transaction
                && (
                    picked.Count == 0
                        ? FiresWithoutPicks
                        : picked.Contains(SourceOf(transaction).Value)
                )
        );
    }

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(ctx.Event is TEvent);
}
