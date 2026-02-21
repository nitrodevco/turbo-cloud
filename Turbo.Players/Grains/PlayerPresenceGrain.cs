using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using Turbo.Players.Configuration;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
    : Grain,
        IPlayerPresenceGrain,
        IAsyncObserver<RoomOutbound>
{
    internal readonly PlayerConfig _playerConfig;
    internal readonly IGrainFactory _grainFactory;
    internal readonly PlayerPresenceLiveState _state;

    private ISessionContextObserver? _sessionObserver = null;
    private StreamSubscriptionHandle<RoomOutbound>? _roomOutboundSub = null;

    private readonly Queue<IComposer> _outgoingQueue = new();

    private IGrainTimer? _timer;
    private bool _isProcessingQueue = false;

    public PlayerPresenceGrain(IOptions<PlayerConfig> playerConfig, IGrainFactory grainFactory)
    {
        _playerConfig = playerConfig.Value;
        _grainFactory = grainFactory;

        _state = new() { PlayerId = PlayerId.Parse((int)this.GetPrimaryKeyLong()) };
    }

    public override Task OnActivateAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _outgoingQueue.Clear();

        return Task.CompletedTask;
    }

    public Task RegisterSessionObserverAsync(ISessionContextObserver observer)
    {
        _sessionObserver = observer;

        _grainFactory
            .GetPlayerGrain(_state.PlayerId)
            .SetOnlineStatusAsync(true, CancellationToken.None)
            .Ignore();

        _timer = this.RegisterGrainTimer<object?>(
            async (state, ct) =>
            {
                var messengerGrain = _grainFactory.GetPlayerMessengerGrain(_state.PlayerId);
                var messengerUpdates = await messengerGrain.GetPendingUpdatesAsync(ct);

                if (messengerUpdates.Count > 0)
                {
                    var categories = await messengerGrain.GetCategoriesAsync(ct);

                    await FlushMessengerUpdatesAsync(categories, messengerUpdates, ct);
                }
            },
            null,
            TimeSpan.FromMilliseconds(_playerConfig.PlayerPresenceTickMs),
            TimeSpan.FromMilliseconds(_playerConfig.PlayerPresenceTickMs)
        );

        return Task.CompletedTask;
    }

    public async Task UnregisterSessionObserverAsync(CancellationToken ct)
    {
        await ClearActiveRoomAsync(ct);

        _grainFactory
            .GetPlayerGrain(_state.PlayerId)
            .SetOnlineStatusAsync(false, CancellationToken.None)
            .Ignore();

        _timer?.Dispose();
        _timer = null;

        _sessionObserver = null;
    }

    public Task<bool> HasActiveSessionAsync() => Task.FromResult(_sessionObserver is not null);

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
                && item.ExcludedPlayerIds.Contains((int)this.GetPrimaryKeyLong())
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
