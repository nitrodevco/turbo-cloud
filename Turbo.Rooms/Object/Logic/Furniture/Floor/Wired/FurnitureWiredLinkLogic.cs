using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired.Link;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

/// <summary>
/// The WIRED Link (<c>wf_link</c>): a room's end of its owner's WIRED Links networks. Its owner
/// double-clicks it to open its window (the server opens it: AS3 <c>WiredLinkController.onOpen</c>
/// asks for the configuration only for a Link the player owns); the send and receive boxes pick
/// it to say which Links they use. Picking it up disconnects it ("To disconnect a Link, pick it up
/// or clear its connection in that room", <c>wiredlink.connections.hint</c>).
/// </summary>
[RoomObjectLogic(CLASSNAME)]
public class FurnitureWiredLinkLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public const string CLASSNAME = "wf_link";

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Controller;

    // Only its owner configures it (wiredlink.error.1, "Only the furniture owner can configure this Link").
    public override Task<bool> CanUseAsync(ActionContext ctx) => Task.FromResult(IsItemOwner(ctx));

    public override Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct) =>
        GrainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new WiredLinkOpenMessageComposer { FurniId = _ctx.ObjectId.Value },
            ct
        );

    /// <summary>Flips the Link between its two states, as a send through it does; the state is kept across reloads.</summary>
    public Task FlipAsync() => SetStateAsync(GetState() == 0 ? 1 : 0);

    public override async Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        await base.OnPickupAsync(ctx, ct);

        await _roomGrain
            ._grainFactory.GetWiredLinkOwnerGrain(_ctx.RoomObject.OwnerId)
            .DisconnectAsync(new WiredLinkEndpoint(_ctx.RoomId, _ctx.ObjectId.Value), ct);
    }
}
