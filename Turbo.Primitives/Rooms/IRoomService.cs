using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Grains;

namespace Turbo.Primitives.Rooms;

public partial interface IRoomService
{
    public IRoomDirectoryGrain GetRoomDirectory();
    public IRoomGrain GetRoomGrain(RoomId roomId);

    public Task OpenRoomForPlayerIdAsync(
        ActionContext ctx,
        PlayerId playerId,
        RoomId roomId,
        CancellationToken ct
    );
    public Task EnterPendingRoomForPlayerIdAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    );
    public Task CloseRoomForPlayerAsync(PlayerId playerId, CancellationToken ct);
    public Task WalkAvatarToAsync(
        ActionContext ctx,
        int targetX,
        int targetY,
        CancellationToken ct
    );
}
