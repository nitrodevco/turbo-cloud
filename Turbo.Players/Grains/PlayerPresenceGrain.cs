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
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Players.Grains;

/// <summary>
/// Where a player is connected and which room they are in, and the one way composers reach
/// them. Nothing is persisted: presence only means something while the session lives, so
/// deactivation drops the queue and lets go of the session and the room stream.
/// </summary>
internal sealed partial class PlayerPresenceGrain
    : Grain,
        IPlayerPresenceGrain,
        IAsyncObserver<RoomOutboundSnapshot>
{
    internal readonly PlayerConfig _playerConfig;
    internal readonly IGrainFactory _grainFactory;
    internal readonly ILogger<IPlayerPresenceGrain> _logger;
    internal readonly PlayerPresenceLiveState _state;

    private ISessionContextObserver? _sessionObserver;
    private StreamSubscriptionHandle<RoomOutboundSnapshot>? _roomOutboundSub;
    private IGrainTimer? _presenceTimer;

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

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _presenceTimer?.Dispose();
        _presenceTimer = null;

        _state.OutgoingQueue.Clear();

        await UnregisterSessionObserverAsync(ct);
    }

    public Task RegisterSessionObserverAsync(ISessionContextObserver observer, CancellationToken ct)
    {
        _sessionObserver = observer;

        _grainFactory
            .GetPlayerGrain(_state.PlayerId)
            .SetOnlineStatusAsync(true, CancellationToken.None)
            .LogAndForget(_logger, $"set player {_state.PlayerId} online");

        _presenceTimer?.Dispose();

        // KeepAlive: a presence grain with a live session must not be collected on idle. Losing
        // the activation drops the session observer and strands the room stream subscription.
        _presenceTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) =>
                await ((PlayerPresenceGrain)self!).FlushPendingMessengerUpdatesAsync(ct),
            this,
            new GrainTimerCreationOptions
            {
                DueTime = TimeSpan.FromMilliseconds(_playerConfig.PlayerPresenceTickMs),
                Period = TimeSpan.FromMilliseconds(_playerConfig.PlayerPresenceTickMs),
                KeepAlive = true,
            }
        );

        return Task.CompletedTask;
    }

    private async Task FlushPendingMessengerUpdatesAsync(CancellationToken ct)
    {
        var messengerGrain = _grainFactory.GetPlayerMessengerGrain(_state.PlayerId);
        var messengerUpdates = await messengerGrain.GetPendingUpdatesAsync(ct);

        if (messengerUpdates.Count == 0)
            return;

        var categories = await messengerGrain.GetCategoriesAsync(ct);

        await FlushMessengerUpdatesAsync(categories, messengerUpdates, ct);
    }

    public async Task UnregisterSessionObserverAsync(CancellationToken ct)
    {
        _presenceTimer?.Dispose();
        _presenceTimer = null;

        await ClearActiveRoomAsync(ct);

        _grainFactory
            .GetPlayerGrain(_state.PlayerId)
            .SetOnlineStatusAsync(false, CancellationToken.None)
            .LogAndForget(_logger, $"set player {_state.PlayerId} offline");

        _sessionObserver = null;
    }

    public Task<bool> HasActiveSessionAsync(CancellationToken ct) =>
        Task.FromResult(_sessionObserver is not null);

    public Task SendComposerAsync(IComposer composer, CancellationToken ct)
    {
        if (composer is not null)
        {
            Enqueue(composer);

            _ = ProcessOutgoingQueueAsync();
        }

        return Task.CompletedTask;
    }

    public Task SendComposerAsync(IReadOnlyList<IComposer> composers, CancellationToken ct)
    {
        if (composers.Count > 0)
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

        return SendComposerAsync(item.Composer, CancellationToken.None);
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
        if (_state.OutgoingQueue.Count >= _playerConfig.MaxPendingComposers)
        {
            _logger.LogWarning(
                "Outgoing queue for player {PlayerId} is full ({Max}); dropping oldest composer",
                _state.PlayerId,
                _playerConfig.MaxPendingComposers
            );

            _state.OutgoingQueue.Dequeue();
        }

        _state.OutgoingQueue.Enqueue(composer);
    }

    private async Task ProcessOutgoingQueueAsync()
    {
        if (_state.IsProcessingQueue)
            return;

        _state.IsProcessingQueue = true;

        try
        {
            await Task.Yield();

            if (_sessionObserver is not null)
            {
                while (_state.OutgoingQueue.Count > 0)
                {
                    var payload = _state.OutgoingQueue.Dequeue();

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
            _state.IsProcessingQueue = false;
        }
    }
}
