using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Actor;
using Turbo.Primitives.Orleans.Snapshots.Session;
using Turbo.Primitives.Rooms.Grains;

namespace Turbo.Primitives.Rooms;

public interface IRoomService
{
    public IRoomDirectoryGrain GetRoomDirectory();
    public IRoomGrain GetRoomGrain(long roomId);

    public Task OpenRoomForSessionAsync(
        SessionKey sessionKey,
        long roomId,
        CancellationToken ct = default
    );
    public Task OpenRoomForPlayerIdAsync(
        long playerId,
        long roomId,
        CancellationToken ct = default
    );
    public Task EnterPendingRoomForSessionAsync(
        SessionKey sessionKey,
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
