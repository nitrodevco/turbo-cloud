using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using Turbo.Players.Configuration;
using Turbo.Players.Extensions;
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
        IAsyncObserver<RoomOutboundSnapshot>
{
    internal readonly PlayerConfig _playerConfig;
    internal readonly IGrainFactory _grainFactory;
    internal readonly ILogger<IPlayerPresenceGrain> _logger;
    internal readonly PlayerPresenceLiveState _state;

    private ISessionContextObserver? _sessionObserver = null;
    private StreamSubscriptionHandle<RoomOutboundSnapshot>? _roomOutboundSub = null;

    private readonly Queue<IComposer> _outgoingQueue = new();

    private IGrainTimer? _timer;
    private bool _isProcessingQueue = false;

    public PlayerId PlayerId => _state.PlayerId;

    public PlayerPresenceGrain(
        IOptions<PlayerConfig> playerConfig,
        IGrainFactory grainFactory,
        ILogger<IPlayerPresenceGrain> logger
    )
    {
        _playerConfig = playerConfig.Value;
        _grainFactory = grainFactory;
        _logger = logger;

        _state = new() { PlayerId = PlayerId.Parse((int)this.GetPrimaryKeyLong()) };
    }

    public override Task OnActivateAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _outgoingQueue.Clear();

        await UnregisterSessionObserverAsync(ct);
    }

    public Task RegisterSessionObserverAsync(ISessionContextObserver observer)
    {
        _sessionObserver = observer;

        _grainFactory
            .GetPlayerGrain(_state.PlayerId)
            .SetOnlineStatusAsync(true, CancellationToken.None)
            .LogAndForget(_logger, $"set player {_state.PlayerId} online");

        _timer?.Dispose();

        // KeepAlive: a presence grain with a live session must not be collected on idle. Losing
        // the activation drops the session observer and strands the room stream subscription.
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
            new GrainTimerCreationOptions
            {
                DueTime = TimeSpan.FromMilliseconds(_playerConfig.PlayerPresenceTickMs),
                Period = TimeSpan.FromMilliseconds(_playerConfig.PlayerPresenceTickMs),
                KeepAlive = true,
            }
        );

        return Task.CompletedTask;
    }

    public async Task UnregisterSessionObserverAsync(CancellationToken ct)
    {
        _timer?.Dispose();
        _timer = null;

        await ClearActiveRoomAsync(ct);

        _grainFactory
            .GetPlayerGrain(_state.PlayerId)
            .SetOnlineStatusAsync(false, CancellationToken.None)
            .LogAndForget(_logger, $"set player {_state.PlayerId} offline");

        _sessionObserver = null;
    }

    public Task<bool> HasActiveSessionAsync() => Task.FromResult(_sessionObserver is not null);

    public Task SendComposerAsync(IComposer composer)
    {
        if (composer is not null)
        {
            Enqueue(composer);

            _ = ProcessOutgoingQueueAsync();
        }

        return Task.CompletedTask;
    }

    public Task SendComposerAsync(params IComposer[] composers)
    {
        if (composers.Length > 0)
        {
            foreach (var composer in composers)
                Enqueue(composer);

            _ = ProcessOutgoingQueueAsync();
        }

        return Task.CompletedTask;
    }

    public Task OnNextAsync(RoomOutboundSnapshot item, StreamSequenceToken? token = null)
    {
        if (
            _sessionObserver is null
            || item.ExcludedPlayerIds is not null && item.ExcludedPlayerIds.Contains(PlayerId)
        )
            return Task.CompletedTask;

        return SendComposerAsync(item.Composer);
    }

    public Task OnCompletedAsync() => Task.CompletedTask;

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(
            ex,
            "Room stream faulted for player {PlayerId} in room {RoomId}",
            _state.PlayerId,
            _state.ActiveRoomId
        );

        return Task.CompletedTask;
    }

    private void Enqueue(IComposer composer)
    {
        // Without a session nothing can drain the queue; keep it bounded so an offline or
        // half-attached presence cannot grow without limit.
        if (_outgoingQueue.Count >= _playerConfig.MaxPendingComposers)
        {
            _logger.LogWarning(
                "Outgoing queue for player {PlayerId} is full ({Max}); dropping oldest composer",
                _state.PlayerId,
                _playerConfig.MaxPendingComposers
            );

            _outgoingQueue.Dequeue();
        }

        _outgoingQueue.Enqueue(composer);
    }

    private async Task ProcessOutgoingQueueAsync()
    {
        if (_isProcessingQueue)
            return;

        _isProcessingQueue = true;

        try
        {
            await Task.Yield();

            if (_sessionObserver is not null)
            {
                while (_outgoingQueue.Count > 0)
                {
                    var payload = _outgoingQueue.Dequeue();

                    await _sessionObserver.SendComposerAsync(payload);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to flush outgoing composers for player {PlayerId}",
                _state.PlayerId
            );
        }
        finally
        {
            _isProcessingQueue = false;
        }
    }
}
