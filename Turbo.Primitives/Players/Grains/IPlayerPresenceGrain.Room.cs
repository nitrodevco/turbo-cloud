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
    /// Sends the player to a room: records how they will arrive, then tells the client to go.
    /// One call so the two cannot come apart — the forward is what makes the client ask to
    /// enter, and the room reads the entry as they land. The entry is kept until they enter
    /// <paramref name="roomId"/> and dropped if they go anywhere else; a plain forward records a
    /// plain entry, so an earlier furni's entry cannot colour this one. Reach it through
    /// <c>IGrainFactory.ForwardPlayerToRoomAsync</c>.
    /// </summary>
    [AlwaysInterleave]
    public Task ForwardToRoomAsync(RoomId roomId, RoomEntrySnapshot entry, CancellationToken ct);

    /// <summary>
    /// How the last forward said the player would arrive in <paramref name="roomId"/>, while it
    /// is fresh (<c>PlayerConfig.PendingRoomEntryTtlMs</c>); a plain entry otherwise. Read by
    /// the entry handlers: a teleporter's entry lets the player past the room's door.
    /// </summary>
    [AlwaysInterleave]
    public Task<RoomEntrySnapshot> GetPendingRoomEntryAsync(RoomId roomId, CancellationToken ct);

    [AlwaysInterleave]
    public Task OnControllerLevelUpdatedAsync(
        RoomId roomId,
        RoomControllerType controllerType,
        CancellationToken ct
    );
}
