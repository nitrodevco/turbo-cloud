using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Orleans;
using Turbo.Primitives.Grains;
using Turbo.Primitives.Orleans.Events.Rooms;
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Session;
using Turbo.Primitives.Orleans.States.Rooms;

namespace Turbo.Players.Grains;

public class PlayerPresenceGrain(
    [PersistentState(OrleansStateNames.PLAYER_PRESENCE, OrleansStorageNames.PRESENCE_STORE)]
        IPersistentState<PlayerPresenceState> state,
    IGrainFactory grainFactory
) : Grain, IPlayerPresenceGrain
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    private ISessionContextObserver? _sessionObserver = null;

    private StreamSubscriptionHandle<RoomEvent>? _subscriptionHandle = null;

    public Task<SessionKey> GetSessionKeyAsync() => Task.FromResult(state.State.Session);

    public async Task RegisterSessionAsync(SessionKey key, ISessionContextObserver observer)
    {
        _sessionObserver = observer;

        state.State.Session = SessionKey.From(key.Value);

        await state.WriteStateAsync();
    }

    public async Task UnregisterSessionAsync(SessionKey key)
    {
        if (!state.State.Session.Value.Equals(key.Value))
            return;

        _sessionObserver = null;

        state.State.Session = SessionKey.Empty;

        await state.WriteStateAsync();

        Console.WriteLine(
            $"[PlayerPresenceGrain] Unregistered session for player {this.GetPrimaryKeyLong()}"
        );
    }

    public Task<RoomPointerSnapshot> GetActiveRoomAsync() =>
        Task.FromResult(
            new RoomPointerSnapshot { RoomId = state.State.ActiveRoomId, Since = state.State.Since }
        );

    public async Task<RoomChangedSnapshot> SetActiveRoomAsync(long roomId)
    {
        var prev = state.State.ActiveRoomId;
        var changed = prev != roomId;

        if (prev > 0 && changed)
            await _grainFactory
                .GetGrain<IRoomPresenceGrain>(prev)
                .RemovePlayerIdAsync(this.GetPrimaryKeyLong());

        state.State.ActiveRoomId = roomId;
        state.State.Since = DateTimeOffset.UtcNow;
        state.State.PendingRoomId = -1;
        state.State.PendingRoomApproved = false;

        await state.WriteStateAsync();

        if (roomId > 0 && changed)
        {
            await _grainFactory
                .GetGrain<IRoomPresenceGrain>(roomId)
                .AddPlayerIdAsync(this.GetPrimaryKeyLong());

            //await SubscribeToActiveRoomAsync();
        }

        return new RoomChangedSnapshot
        {
            PreviousRoomId = prev,
            CurrentRoomId = roomId,
            Changed = changed,
        };
    }

    public async Task<RoomChangedSnapshot> ClearActiveRoomAsync()
    {
        return await SetActiveRoomAsync(-1);
    }

    public Task<PendingRoomInfoSnapshot> GetPendingRoomAsync() =>
        Task.FromResult(
            new PendingRoomInfoSnapshot
            {
                RoomId = state.State.PendingRoomId,
                Approved = state.State.PendingRoomApproved,
            }
        );

    public async Task SetPendingRoomAsync(long roomId, bool approved)
    {
        state.State.PendingRoomId = roomId;
        state.State.PendingRoomApproved = approved;

        await state.WriteStateAsync();
    }

    public async Task SubscribeToActiveRoomAsync()
    {
        await UnsubscribeFromActiveRoomAsync();

        var roomId = state.State.ActiveRoomId;

        if (roomId <= 0)
            return;

        var streamProvider = this.GetStreamProvider(OrleansStreamProviders.DEFAULT_STREAM_PROVIDER);
        var stream = streamProvider.GetStream<RoomEvent>(
            StreamId.Create(OrleansStreamNames.ROOM_EVENTS, roomId)
        );

        _subscriptionHandle = await stream.SubscribeAsync(OnRoomStreamAsync);
    }

    public async Task UnsubscribeFromActiveRoomAsync()
    {
        if (_subscriptionHandle is null)
            return;

        await _subscriptionHandle.UnsubscribeAsync();

        _subscriptionHandle = null;
    }

    private Task OnRoomStreamAsync(RoomEvent evt, StreamSequenceToken? token)
    {
        Console.WriteLine($"[Monitor] Room {evt.RoomId}");

        return Task.CompletedTask;
    }

    public async Task ResetAsync() => await state.ClearStateAsync();

    public Task SendComposerAsync(IComposer composer, CancellationToken ct = default)
    {
        if (_sessionObserver is not null)
        {
            _ = _sessionObserver.SendComposerAsync(composer, ct);
        }

        return Task.CompletedTask;
    }
}
