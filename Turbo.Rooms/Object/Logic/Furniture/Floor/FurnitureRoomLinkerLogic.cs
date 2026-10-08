using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// "WIRED Room Linker" (<c>wf_room_linker</c>): sold in linked pairs like a teleporter, but not
/// stepped into. "WIRED Effect: Teleport to Room" with a linker as its furni sends the users to
/// the room the linker's other half stands in, where they arrive on that half (Wired Faculty
/// tutorial "New WIRED Room Linker Tutorial", 30/04/2025). <c>~teleport.target_id</c> reads and
/// relinks it as it does a teleporter.
/// </summary>
[RoomObjectLogic(TeleportFurniture.ROOM_LINKER_CLASSNAME)]
public class FurnitureRoomLinkerLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureTeleportLogic(stuffDataFactory, ctx)
{
    // Using it does nothing of its own: clicks are for the wired that reads it.
    public override Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct) =>
        Task.CompletedTask;

    // The player arrives standing on it, with nothing to step out of.
    public override Task ReceiveArrivalAsync(IRoomAvatar avatar, CancellationToken ct) =>
        Task.CompletedTask;
}
