using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms;

public partial interface IRoomService
{
    public Task MoveWallItemInRoomAsync(
        ActionContext ctx,
        int itemId,
        int newX,
        int newY,
        double newZ,
        int wallOffset,
        Rotation newRot,
        CancellationToken ct
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
