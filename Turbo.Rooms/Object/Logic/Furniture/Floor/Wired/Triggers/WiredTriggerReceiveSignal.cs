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
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// Fires when a "send signal" action addresses one of the antenna furni this box picked. The
/// forwarded furni and users of the signal are available to the stack as signal sources.
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
            [WiredFurniSourceType.SelectedItems],
        ];

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is not WiredSignalEvent signal)
            return Task.FromResult(false);

        if (signal.Depth > _roomGrain._wiredConfig.MaxDepth)
            return Task.FromResult(false);

        return Task.FromResult(GetStuffIds().Any(signal.AntennaIds.Contains));
    }

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(ctx.Event is WiredSignalEvent);
}
