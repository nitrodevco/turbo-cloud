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
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Link;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// WIRED Trigger: Receive Link Message (<c>wf_trg_receive_message</c>, trigger code 29): fires
/// when a Send Link Message reaches one of the WIRED Links it picked. "The received message is
/// captured in the text placeholder $(trigger_text)" (<c>wiredfurni.params.link.receive_message_info</c>);
/// the room's own wired puts it there as the stack starts.
/// </summary>
[RoomObjectLogic("wf_trg_receive_message")]
public class WiredTriggerReceiveMessage(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.RECEIVE_LINK_MESSAGE;

    public override List<Type> SupportedEventTypes { get; } = [typeof(WiredLinkReceivedEvent)];

    // "WIRED Links: Use picked furnis" (wiredfurni.params.link.endpoints), or a selector's
    // (recorded: default 100, allowed [100,200]).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.PickedFurni];

    protected override string? RefusePickInSlot(int slot, IRoomItem item) =>
        WiredLinkBoxes.IsLink(item) ? null : WiredLinkBoxes.REQUIRE_LINK_FURNI;

    public override Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct) =>
        Task.FromResult(
            evt is WiredLinkReceivedEvent { Payload.IsDataframe: false } received
                && WiredLinkBoxes.ListensTo(this, received)
        );

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct) =>
        Task.FromResult(ctx.Event is WiredLinkReceivedEvent);
}
