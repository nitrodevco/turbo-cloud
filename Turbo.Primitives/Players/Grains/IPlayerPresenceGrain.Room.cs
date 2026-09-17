using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    public Task<RoomPointerSnapshot> GetActiveRoomAsync(CancellationToken ct);
    public Task<RoomPendingSnapshot> GetPendingRoomAsync(CancellationToken ct);
    public Task SetActiveRoomAsync(RoomId roomId, CancellationToken ct);
    public Task ClearActiveRoomAsync(CancellationToken ct);
    public Task SetPendingRoomAsync(RoomId roomId, RoomEntryState state, CancellationToken ct);
    public Task ClearPendingRoomAsync(CancellationToken ct);
    public Task OnControllerLevelUpdatedAsync(
        RoomId roomId,
        RoomControllerType controllerType,
        bool canModifyWired,
        bool canReadWired,
        CancellationToken ct
    );
    public Task OnWiredPermissionsUpdatedAsync(
        RoomId roomId,
        bool canModifyWired,
        bool canReadWired,
        CancellationToken ct
    );
}
