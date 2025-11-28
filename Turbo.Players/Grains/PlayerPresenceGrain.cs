using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Orleans;
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Session;
using Turbo.Primitives.Orleans.States.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Players.Grains;

public class PlayerPresenceGrain(
    [PersistentState(OrleansStateNames.PLAYER_PRESENCE, OrleansStorageNames.PLAYER_STORE)]
        IPersistentState<PlayerPresenceState> state,
    IRoomService roomService
) : Grain, IPlayerPresenceGrain
{
    private readonly IRoomService _roomService = roomService;
    private ISessionContextObserver? _sessionObserver = null;

    public Task<SessionKey> GetSessionKeyAsync() =>
        Task.FromResult(SessionKey.From(state.State.Session.Value));

    public Task<RoomPointerSnapshot> GetActiveRoomAsync() =>
        Task.FromResult(
            new RoomPointerSnapshot
            {
                RoomId = RoomId.From(state.State.ActiveRoomId.Value),
                ActiveSinceUtc = state.State.ActiveRoomSinceUtc,
            }
        );

    public Task<RoomPendingSnapshot> GetPendingRoomAsync() =>
        Task.FromResult(
            new RoomPendingSnapshot
            {
                RoomId = RoomId.From(state.State.PendingRoomId.Value),
                Approved = state.State.PendingRoomApproved,
            }
        );

    public async Task RegisterSessionAsync(SessionKey key, ISessionContextObserver observer)
    {
        _sessionObserver = observer;

        state.State.Session = SessionKey.From(key.Value);

        await state.WriteStateAsync();
    }

    public async Task UnregisterSessionAsync(SessionKey key)
    {
        if (!state.State.Session.CompareTo(key))
            return;

        _sessionObserver = null;

        state.State.Session = SessionKey.Empty;

        await state.WriteStateAsync();
        await ClearActiveRoomAsync();
    }

    public async Task SetActiveRoomAsync(RoomId roomId)
    {
        var prev = state.State.ActiveRoomId;
        var next = roomId;
        var changed = !prev.CompareTo(next);

        state.State.ActiveRoomId = RoomId.From(next.Value);
        state.State.PendingRoomId = RoomId.Empty;
        state.State.PendingRoomApproved = false;
        state.State.ActiveRoomSinceUtc = DateTime.UtcNow;

        await state.WriteStateAsync();

        if (changed)
        {
            if (!prev.IsEmpty())
                await _roomService
                    .GetRoomDirectory()
                    .RemovePlayerFromRoomAsync(this.GetPrimaryKeyLong(), prev);

            if (!next.IsEmpty())
                await _roomService
                    .GetRoomDirectory()
                    .AddPlayerToRoomAsync(this.GetPrimaryKeyLong(), next);
        }
    }

    public async Task ClearActiveRoomAsync() => await SetActiveRoomAsync(RoomId.Empty);

    public async Task LeaveRoomAsync(RoomId roomId)
    {
        if (!state.State.ActiveRoomId.CompareTo(roomId))
            return;

        await SetActiveRoomAsync(RoomId.Empty);
    }

    public async Task SetPendingRoomAsync(RoomId roomId, bool approved)
    {
        state.State.PendingRoomId = RoomId.From(roomId.Value);
        state.State.PendingRoomApproved = approved;

        await state.WriteStateAsync();
    }

    public Task SendComposerAsync(IComposer composer, CancellationToken ct = default)
    {
        if (_sessionObserver is not null)
        {
            _ = _sessionObserver.SendComposerAsync(composer, ct);
        }

        return Task.CompletedTask;
    }
}
