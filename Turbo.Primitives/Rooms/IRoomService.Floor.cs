using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms;

public partial interface IRoomService
{
    public Task PlaceFloorItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    );
    public Task MoveFloorItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    );
    public Task PickupFloorItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        bool isConfirm = true
    );
    public Task UseFloorItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int param = -1
    );
    public Task ClickFloorItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int param = -1
    );
}
