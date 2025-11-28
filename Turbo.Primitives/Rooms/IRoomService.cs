using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Grains;

namespace Turbo.Primitives.Rooms;

public interface IRoomService
{
    public IRoomDirectoryGrain GetRoomDirectory();
    public IRoomGrain GetRoomGrain(long roomId);

    public Task OpenRoomForPlayerIdAsync(
        long playerId,
        RoomId roomId,
        CancellationToken ct = default
    );
    public Task EnterPendingRoomForPlayerIdAsync(long playerId, CancellationToken ct = default);
    public Task CloseRoomForPlayerAsync(long playerId);
    public Task MoveFloorItemInRoomAsync(
        ActionContext ctx,
        long itemId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct = default
    );
    public Task UseFloorItemInRoomAsync(
        ActionContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    );
    public Task ClickFloorItemInRoomAsync(
        ActionContext ctx,
        long itemId,
        int param = -1,
        CancellationToken ct = default
    );
}
