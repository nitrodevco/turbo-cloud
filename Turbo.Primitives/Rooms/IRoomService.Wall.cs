using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms;

public partial interface IRoomService
{
    public Task PlaceWallItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        int x,
        int y,
        double z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    );
    public Task MoveWallItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        int x,
        int y,
        double z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    );
    public Task PickupWallItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        bool isConfirm = true
    );
    public Task UseWallItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int param = -1
    );
    public Task ClickWallItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        CancellationToken ct,
        int param = -1
    );
}
