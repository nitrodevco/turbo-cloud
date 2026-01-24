using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms;

public partial interface IRoomService
{
    public Task PlaceFloorItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    );
    public Task MoveFloorItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    );
}
