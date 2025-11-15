using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Primitives.Rooms;

public interface IRoomService
{
    public Task<IRoomGrain> GetRoomGrainAsync(long roomId);

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
}
