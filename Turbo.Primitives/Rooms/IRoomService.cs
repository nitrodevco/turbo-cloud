using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Grains;

namespace Turbo.Primitives.Rooms;

public partial interface IRoomService
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
    public Task WalkAvatarToAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    );
}
