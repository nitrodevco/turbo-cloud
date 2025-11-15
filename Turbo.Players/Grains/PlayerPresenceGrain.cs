using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Orleans;
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Orleans.Grains.Room;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Session;
using Turbo.Primitives.Orleans.States.Players;

namespace Turbo.Players.Grains;

public class PlayerPresenceGrain(
    [PersistentState(OrleansStateNames.PLAYER_PRESENCE, OrleansStorageNames.PLAYER_STORE)]
        IPersistentState<PlayerPresenceState> state,
    IGrainFactory grainFactory
) : Grain, IPlayerPresenceGrain
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    private ISessionContextObserver? _sessionObserver = null;

    public Task<SessionKey> GetSessionKeyAsync() =>
        Task.FromResult(SessionKey.From(state.State.Session.Value));

    public Task<RoomPointerSnapshot> GetActiveRoomAsync() =>
        Task.FromResult(
            new RoomPointerSnapshot
            {
                RoomId = state.State.ActiveRoomId,
                ActiveSinceUtc = state.State.ActiveRoomSinceUtc,
            }
        );

    public Task<RoomPendingSnapshot> GetPendingRoomAsync() =>
        Task.FromResult(
            new RoomPendingSnapshot
            {
                RoomId = state.State.PendingRoomId,
                Approved = state.State.PendingRoomApproved,
            }
        );

    public async Task RegisterSessionAsync(SessionKey key, ISessionContextObserver observer)
    {
        _sessionObserver = observer;

        state.State.Session = SessionKey.From(key.Value);

        await state.WriteStateAsync();

        Console.WriteLine($"Player {this.GetPrimaryKeyLong()} - Registered session {key.Value}");
    }

    public async Task UnregisterSessionAsync(SessionKey key)
    {
        if (!state.State.Session.Value.Equals(key.Value))
            return;

        _sessionObserver = null;

        state.State.Session = SessionKey.Empty;

        await state.WriteStateAsync();
        await ClearActiveRoomAsync();

        Console.WriteLine($"Player {this.GetPrimaryKeyLong()} - Unregistered session {key.Value}");
    }

    public async Task SetActiveRoomAsync(long roomId)
    {
        var prev = state.State.ActiveRoomId;
        var next = roomId;
        var changed = prev != next;

        state.State.ActiveRoomId = next;
        state.State.PendingRoomId = -1;
        state.State.PendingRoomApproved = false;
        state.State.ActiveRoomSinceUtc = DateTime.UtcNow;

        await state.WriteStateAsync();

        if (changed)
        {
            if (prev > 0)
                await _grainFactory
                    .GetGrain<IRoomPresenceGrain>(prev)
                    .RemovePlayerIdAsync(this.GetPrimaryKeyLong());

            if (next > 0)
                await _grainFactory
                    .GetGrain<IRoomPresenceGrain>(next)
                    .AddPlayerIdAsync(this.GetPrimaryKeyLong());
        }

        Console.WriteLine($"Player {this.GetPrimaryKeyLong()} - Set active room to {roomId}");
    }

    public async Task ClearActiveRoomAsync() => await SetActiveRoomAsync(-1);

    public async Task LeaveRoomAsync(long roomId)
    {
        if (state.State.ActiveRoomId != roomId)
            return;

        await SetActiveRoomAsync(-1);
    }

    public async Task SetPendingRoomAsync(long roomId, bool approved)
    {
        state.State.PendingRoomId = roomId;
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
