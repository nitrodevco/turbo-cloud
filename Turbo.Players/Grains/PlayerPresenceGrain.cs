using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using Turbo.Players.Grains.Modules;
using Turbo.Primitives.Action;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
    : Grain,
        IPlayerPresenceGrain,
        IAsyncObserver<RoomOutbound>
{
    internal readonly IGrainFactory _grainFactory;

    private ISessionContextObserver? _sessionObserver = null;
    private StreamSubscriptionHandle<RoomOutbound>? _roomOutboundSub = null;

    private IPersistentState<PlayerPresenceState> _state;

    private readonly PlayerInventoryModule _inventoryModule;
    private readonly Queue<IComposer> _outgoingQueue = new();
    private bool _isProcessingQueue = false;

    public PlayerPresenceGrain(
        [PersistentState(OrleansStateNames.PLAYER_PRESENCE, OrleansStorageNames.PLAYER_STORE)]
            IPersistentState<PlayerPresenceState> state,
        IGrainFactory grainFactory
    )
    {
        _grainFactory = grainFactory;

        _state = state;
        _inventoryModule = new(this);
    }

    public override Task OnActivateAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task<SessionKey> GetSessionKeyAsync() => Task.FromResult(_state.State.SessionKey);

    public Task<RoomPointerSnapshot> GetActiveRoomAsync() =>
        Task.FromResult(
            new RoomPointerSnapshot
            {
                RoomId = _state.State.ActiveRoomId,
                ActiveSinceUtc = _state.State.ActiveRoomSinceUtc,
            }
        );

    public Task<RoomPendingSnapshot> GetPendingRoomAsync() =>
        Task.FromResult(
            new RoomPendingSnapshot
            {
                RoomId = _state.State.PendingRoomId,
                Approved = _state.State.PendingRoomApproved,
            }
        );

    public async Task RegisterSessionAsync(SessionKey key, ISessionContextObserver observer)
    {
        _sessionObserver = observer;

        _state.State.SessionKey = key;

        await _inventoryModule.OnSessionAttachedAsync(CancellationToken.None);

        await _state.WriteStateAsync();
    }

    public async Task UnregisterSessionAsync(SessionKey key, CancellationToken ct)
    {
        if (!_state.State.SessionKey.Equals(key))
            return;

        await ClearActiveRoomAsync(ct);

        await _inventoryModule.OnSessionDetachedAsync(ct);

        _sessionObserver = null;

        _state.State.SessionKey = string.Empty;

        await _state.WriteStateAsync(ct);
    }

    public async Task SetActiveRoomAsync(RoomId roomId, CancellationToken ct)
    {
        if (roomId <= 0)
            return;

        await ClearActiveRoomAsync(ct);

        var next = roomId;

        _state.State.ActiveRoomId = next;
        _state.State.PendingRoomId = -1;
        _state.State.PendingRoomApproved = false;
        _state.State.ActiveRoomSinceUtc = DateTime.UtcNow;

        await _state.WriteStateAsync(ct);

        await _grainFactory
            .GetRoomDirectoryGrain()
            .AddPlayerToRoomAsync((PlayerId)this.GetPrimaryKeyLong(), next, ct);

        var provider = this.GetStreamProvider(OrleansStreamProviders.ROOM_STREAM_PROVIDER);
        var streamId = StreamId.Create(OrleansStreamNames.ROOM_STREAM, (long)roomId.Value);
        var stream = provider.GetStream<RoomOutbound>(streamId);

        _roomOutboundSub = await stream.SubscribeAsync(this);

        var room = _grainFactory.GetRoomGrain(roomId);

        var playerSnapshot = await _grainFactory
            .GetPlayerGrain((PlayerId)this.GetPrimaryKeyLong())
            .GetSummaryAsync(ct);

        var ctx = new ActionContext
        {
            Origin = ActionOrigin.Player,
            SessionKey = SessionKey.Invalid,
            PlayerId = (PlayerId)this.GetPrimaryKeyLong(),
            RoomId = roomId,
        };

        await room.CreateAvatarFromPlayerAsync(ctx, playerSnapshot, ct);
    }

    public async Task ClearActiveRoomAsync(CancellationToken ct)
    {
        if (_state.State.ActiveRoomId <= 0)
            return;

        var prev = _state.State.ActiveRoomId;

        _state.State.ActiveRoomId = -1;
        _state.State.ActiveRoomSinceUtc = DateTime.UtcNow;

        await _state.WriteStateAsync(ct);

        var ctx = new ActionContext
        {
            Origin = ActionOrigin.Player,
            SessionKey = SessionKey.Invalid,
            PlayerId = (PlayerId)this.GetPrimaryKeyLong(),
            RoomId = prev,
        };

        await _grainFactory
            .GetRoomGrain(prev)
            .RemoveAvatarFromPlayerAsync(ctx, (PlayerId)this.GetPrimaryKeyLong(), ct);

        await _grainFactory
            .GetRoomDirectoryGrain()
            .RemovePlayerFromRoomAsync((PlayerId)this.GetPrimaryKeyLong(), prev, ct);

        if (_roomOutboundSub is not null)
        {
            await _roomOutboundSub.UnsubscribeAsync();

            _roomOutboundSub = null;
        }
    }

    public async Task SetPendingRoomAsync(RoomId roomId, bool approved)
    {
        _state.State.PendingRoomId = roomId;
        _state.State.PendingRoomApproved = approved;

        await _state.WriteStateAsync();
    }

    public Task SendComposerAsync(IComposer composer)
    {
        if (composer is not null)
        {
            _outgoingQueue.Enqueue(composer);

            _ = ProcessOutgoingQueueAsync();
        }

        return Task.CompletedTask;
    }

    public Task SendComposerAsync(params IComposer[] composers)
    {
        if (composers.Length > 0)
        {
            foreach (var composer in composers)
                _outgoingQueue.Enqueue(composer);

            _ = ProcessOutgoingQueueAsync();
        }

        return Task.CompletedTask;
    }

    public Task OnNextAsync(RoomOutbound item, StreamSequenceToken? token = null)
    {
        if (
            _sessionObserver is null
            || item.ExcludedPlayerIds is not null
                && item.ExcludedPlayerIds.Contains((PlayerId)this.GetPrimaryKeyLong())
        )
            return Task.CompletedTask;

        return SendComposerAsync(item.Composer);
    }

    public Task OnCompletedAsync() => Task.CompletedTask;

    public Task OnErrorAsync(Exception ex) => Task.CompletedTask;

    private async Task ProcessOutgoingQueueAsync()
    {
        if (_isProcessingQueue)
            return;

        _isProcessingQueue = true;

        await Task.Yield();

        if (_sessionObserver is not null)
        {
            while (_outgoingQueue.Count > 0)
            {
                var payload = _outgoingQueue.Dequeue();

                await _sessionObserver.SendComposerAsync(payload);
            }
        }

        _isProcessingQueue = false;
    }
}
