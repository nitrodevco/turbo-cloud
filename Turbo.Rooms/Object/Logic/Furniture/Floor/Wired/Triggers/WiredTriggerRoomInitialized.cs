using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// Fires once when the room initializes, and first: the room's first wired tick after it loaded,
/// before anything queued by then (<c>wiredfurni.params.room_initialized.usage_info</c>: "triggers
/// once when the room initializes, and it is the first to trigger"; a room initializes when
/// someone enters it after it was unloaded, or after <c>:reload</c>). No params.
/// </summary>
[RoomObjectLogic("wf_trg_room_initializes")]
public class WiredTriggerRoomInitialized(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.INITIALIZATION;
    public override List<Type> SupportedEventTypes { get; } = [typeof(WiredRoomInitializedEvent)];

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(ctx.Event is WiredRoomInitializedEvent);
}
