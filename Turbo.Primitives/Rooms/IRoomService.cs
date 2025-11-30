using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms;

public interface IRoomService
{
    public IRoomDirectoryGrain GetRoomDirectory();
    public IRoomGrain GetRoomGrain(long roomId);

    public Task OpenRoomForPlayerIdAsync(
        ActionContext ctx,
        long playerId,
        RoomId roomId,
        CancellationToken ct
    );
    public Task EnterPendingRoomForPlayerIdAsync(
        ActionContext ctx,
        long playerId,
        CancellationToken ct
    );
    public Task CloseRoomForPlayerAsync(long playerId, CancellationToken ct);
    public Task MoveFloorItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    );
    public Task UseFloorItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct,
        int param = -1
    );
    public Task ClickFloorItemInRoomAsync(
        ActionContext ctx,
        RoomObjectId objectId,
        CancellationToken ct,
        int param = -1
    );
    public Task WalkAvatarToAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    );
}
