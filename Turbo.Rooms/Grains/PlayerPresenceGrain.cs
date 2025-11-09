using System;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using Turbo.Contracts.Orleans;
using Turbo.Primitives.Grains;
using Turbo.Primitives.Orleans.Events.Rooms;
using Turbo.Primitives.Orleans.Snapshots.Rooms;
using Turbo.Primitives.Orleans.States.Rooms;

namespace Turbo.Rooms.Grains;

public class PlayerPresenceGrain(
    [PersistentState(OrleansStateNames.PLAYER_PRESENCE, OrleansStorageNames.PRESENCE_STORE)]
        IPersistentState<PlayerPresenceState> state,
    IGrainFactory grainFactory
) : Grain, IPlayerPresenceGrain
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    private StreamSubscriptionHandle<RoomEvent>? _subscriptionHandle = null;

    private string _sessionId = string.Empty;

    public Task<string> GetSessionIdAsync() => Task.FromResult(_sessionId);

    public Task SetSessionIdAsync(string sessionId)
    {
        _sessionId = sessionId;

        return Task.CompletedTask;
    }

    public Task<RoomPointerSnapshot> GetCurrentRoomAsync() =>
        Task.FromResult(
            new RoomPointerSnapshot { RoomId = state.State.RoomId, Since = state.State.Since }
        );

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

    public async Task ResetAsync() => await state.ClearStateAsync();

    public async Task<RoomChangedSnapshot> EnterRoomAsync(long roomId)
    {
        var prev = state.State.RoomId;

        if (prev == roomId)
            return new RoomChangedSnapshot
            {
                PreviousRoomId = prev,
                CurrentRoomId = prev,
                Changed = false,
            };

        if (prev > 0)
            await _grainFactory
                .GetGrain<IRoomPresenceGrain>(prev)
                .RemovePlayerIdAsync(this.GetPrimaryKeyLong());

        await _grainFactory
            .GetGrain<IRoomPresenceGrain>(roomId)
            .AddPlayerIdAsync(this.GetPrimaryKeyLong());

        state.State.RoomId = roomId;
        state.State.Since = DateTimeOffset.UtcNow;
        state.State.PendingRoomId = -1;
        state.State.PendingRoomApproved = false;

        await state.WriteStateAsync();
        await SubscribeToActiveRoomAsync();

        return new RoomChangedSnapshot
        {
            PreviousRoomId = prev,
            CurrentRoomId = roomId,
            Changed = true,
        };
    }

    public async Task<RoomChangedSnapshot> LeaveRoomAsync()
    {
        var prev = state.State.RoomId;

        if (prev <= 0)
            return new RoomChangedSnapshot
            {
                PreviousRoomId = -1,
                CurrentRoomId = -1,
                Changed = false,
            };

        await _grainFactory
            .GetGrain<IRoomPresenceGrain>(prev)
            .RemovePlayerIdAsync(this.GetPrimaryKeyLong());

        state.State.RoomId = -1;
        state.State.Since = DateTimeOffset.UtcNow;

        await state.WriteStateAsync();
        await UnsubscribeFromActiveRoomAsync();

        return new RoomChangedSnapshot
        {
            PreviousRoomId = prev,
            CurrentRoomId = -1,
            Changed = true,
        };
    }

    public async Task SubscribeToActiveRoomAsync()
    {
        await UnsubscribeFromActiveRoomAsync();

        var roomId = state.State.RoomId;

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
}
