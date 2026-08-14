using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    public Task<RoomPointerSnapshot> GetActiveRoomAsync();
    public Task<RoomPendingSnapshot> GetPendingRoomAsync();
    public Task SetActiveRoomAsync(RoomId roomId, CancellationToken ct);
    public Task ClearActiveRoomAsync(CancellationToken ct);
    public Task SetPendingRoomAsync(RoomId roomId, bool approved);
    public Task OnControllerLevelUpdatedAsync(
        RoomId roomId,
        RoomControllerType controllerType,
        CancellationToken ct
    );
}
