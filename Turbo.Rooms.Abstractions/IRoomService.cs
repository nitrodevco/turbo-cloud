using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Grains;

namespace Turbo.Rooms.Abstractions;

public interface IRoomService
{
    public Task<IRoomGrain> GetRoomGrainAsync(long roomId);
    public Task OpenRoomForPlayerIdAsync(
        long playerId,
        long roomId,
        CancellationToken ct = default
    );
    public Task EnterPendingRoomForPlayerIdAsync(long playerId, CancellationToken ct = default);
}
