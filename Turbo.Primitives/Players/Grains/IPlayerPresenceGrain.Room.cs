using System.Threading;
using System.Threading.Tasks;
using Orleans.Concurrency;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    [AlwaysInterleave]
    public Task<RoomPointerSnapshot> GetActiveRoomAsync(CancellationToken ct);

    [AlwaysInterleave]
    public Task<RoomPendingSnapshot> GetPendingRoomAsync(CancellationToken ct);
    public Task SetActiveRoomAsync(RoomId roomId, CancellationToken ct);
    public Task ClearActiveRoomAsync(CancellationToken ct);

    /// <summary>
    /// Closes the room session of a player whose avatar the room has already removed. Never
    /// calls the room grain back, so it is the one eviction call a room grain may make.
    /// </summary>
    public Task OnRemovedFromRoomAsync(RoomId roomId, bool kicked, CancellationToken ct);
    public Task SetPendingRoomAsync(RoomId roomId, RoomEntryState state, CancellationToken ct);
    public Task ClearPendingRoomAsync(CancellationToken ct);

    /// <summary>
    /// Says how the player is about to arrive somewhere, for the furni that is sending them
    /// there. It is kept until they enter <paramref name="roomId"/> and is dropped if they go
    /// anywhere else, so a forward they never followed cannot colour a later entry.
    /// </summary>
    [AlwaysInterleave]
    public Task SetPendingRoomEntryAsync(
        RoomId roomId,
        RoomEntrySnapshot entry,
        CancellationToken ct
    );

    [AlwaysInterleave]
    public Task OnControllerLevelUpdatedAsync(
        RoomId roomId,
        RoomControllerType controllerType,
        CancellationToken ct
    );
}
