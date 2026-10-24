using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// Fires when a "send signal" action addresses one of the antenna furni this box listens on: its
/// picks, the stack's selectors' furni or the furni of the signal being handled (official client
/// capture: a fresh box offers furni [[100,200,201]], default [100]). The forwarded furni and users
/// of the signal are available to the stack as signal sources.
/// </summary>
[RoomObjectLogic("wf_trg_recv_signal")]
public class WiredTriggerReceiveSignal(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.RECEIVE_SIGNAL;
    public override List<Type> SupportedEventTypes { get; } = [typeof(WiredSignalEvent)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
            ],
        ];

    // Listens on antennas only (wiredfurni.error.require_antenna_furni; inferred from its wording).
    protected override string? RefusePickInSlot(int slot, IRoomItem item) =>
        IsAntenna(item) ? null : WiredSaveErrors.REQUIRE_ANTENNA_FURNI;

    /// <summary>
    /// A box listening on its own picks turns other antennas' signals away here, before the
    /// stack's selectors run; one listening on the selectors' or the signal's furni is left to
    /// <see cref="CanTriggerAsync"/>, because those are only known once the selectors have run.
    /// </summary>
    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is not WiredSignalEvent signal)
            return Task.FromResult(false);

        if (!ListensOnPicksOnly())
            return Task.FromResult(true);

        return Task.FromResult(GetStuffIds().Any(signal.AntennaIds.Contains));
    }

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(
            ctx.Event is WiredSignalEvent signal
                && ctx.GetSelection(this).SelectedFurniIds.Any(signal.AntennaIds.Contains)
        );

    private bool ListensOnPicksOnly() =>
        GetFurniSources()
            .All(slot => slot.All(source => source == WiredFurniSourceType.SelectedItems));
}
