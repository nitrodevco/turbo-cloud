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
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms;
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

    // The active room changed and the session has not been told yet. The next flush carries it,
    // even with no composers queued; see ProcessOutgoingQueueAsync.
    private bool _activeRoomDirty;

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

        await UnregisterSessionObserverAsync(_state.SessionKey, ct);
    }

    public Task RegisterSessionObserverAsync(
        SessionKey sessionKey,
        ISessionContextObserver observer,
        CancellationToken ct
    )
    {
        // A flush awaiting the previous observer must not stall the replacement connection.
        _state.SessionGeneration++;
        _state.SessionKey = sessionKey;
        _state.ClientCapabilities = _state.ClientCapabilities.Clear();
        _state.IsProcessingQueue = false;
        _state.OutgoingQueue.Clear();
        _sessionObserver = observer;

        // A new session starts outside any room; the first flush tells it otherwise. No flush is
        // started here: whatever queued before the session attached must not overtake what the
        // login handler sends it directly.
        _activeRoomDirty = true;

        _grainFactory
            .GetPlayerGrain(_state.PlayerId)
            .SetOnlineStatusAsync(true, CancellationToken.None)
            .LogAndForget(_logger, "set player {PlayerId} online", _state.PlayerId);

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

    public async Task UnregisterSessionObserverAsync(SessionKey sessionKey, CancellationToken ct)
    {
        // A delayed disconnect belongs only to the connection that registered it.
        if (_state.SessionKey != sessionKey)
            return;

        _state.SessionGeneration++;
        _state.SessionKey = SessionKey.Invalid;
        _state.ClientCapabilities = _state.ClientCapabilities.Clear();
        _state.IsProcessingQueue = false;
        _state.OutgoingQueue.Clear();
        _sessionObserver = null;

        _presenceTimer?.Dispose();
        _presenceTimer = null;

        await ClearActiveRoomAsync(ct);

        _grainFactory
            .GetPlayerGrain(_state.PlayerId)
            .SetOnlineStatusAsync(false, CancellationToken.None)
            .LogAndForget(_logger, "set player {PlayerId} offline", _state.PlayerId);

        _sessionObserver = null;
    }

    public Task<bool> HasActiveSessionAsync(CancellationToken ct) =>
        Task.FromResult(_sessionObserver is not null);

    public Task SendComposerAsync(IComposer composer, CancellationToken ct)
    {
        if (composer is not null)
        {
            Enqueue(composer);
            StartOutgoingFlush();
        }

        return Task.CompletedTask;
    }

    public Task SendComposerAsync(IReadOnlyList<IComposer> composers, CancellationToken ct)
    {
        if (composers.Count > 0)
        {
            foreach (var composer in composers)
                Enqueue(composer);

            StartOutgoingFlush();
        }

        return Task.CompletedTask;
    }

    public Task OnNextAsync(RoomOutboundSnapshot item, StreamSequenceToken? token = null)
    {
        if (
            _sessionObserver is null
            || item.Composers.IsDefaultOrEmpty
            || item.ExcludedPlayerIds is not null && item.ExcludedPlayerIds.Contains(PlayerId)
        )
            return Task.CompletedTask;

        RoomTelemetry.RecordStreamDelivery(item.PublishedAtUtcTicks);

        foreach (var composer in item.Composers)
            Enqueue(composer);

        StartOutgoingFlush();

        return Task.CompletedTask;
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
        // An extension packet is for a session that asked for it; any other client never sees one.
        if (composer is ICapabilityComposer extension && !AcceptsExtension(extension))
            return;

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

    /// <summary>
    /// The active room changed: the session caches it for incoming packets (see
    /// <see cref="ISessionContextObserver"/>), so it is pushed with the next flush.
    /// </summary>
    internal void OnActiveRoomChanged()
    {
        _activeRoomDirty = true;
        StartOutgoingFlush();
    }

    // Starts the flush unless one is already running: that one drains what was just queued too.
    // Tells call this for every composer, so the running case costs nothing, not a task and a
    // continuation each.
    private void StartOutgoingFlush()
    {
        if (_state.IsProcessingQueue || _sessionObserver is null)
            return;

        _state.IsProcessingQueue = true;

        // ProcessOutgoingQueueAsync catches and logs everything itself; this only guards the
        // task from being dropped unobserved.
        ProcessOutgoingQueueAsync(_state.SessionGeneration)
            .LogAndForget(
                _logger,
                "flush outgoing composers for player {PlayerId}",
                _state.PlayerId
            );
    }

    /// <summary>
    /// Sends everything queued, a whole queue per observer call, and the active room with it.
    /// One call at a time, awaited, so the session receives the flushes in order; composers
    /// queued while a call is in flight go in the next one. The room is read when each call is
    /// made, so the session never lags the presence by more than the flush in flight, and never
    /// sees a composer from a room before it has been told it is in that room.
    /// </summary>
    private async Task ProcessOutgoingQueueAsync(long sessionGeneration)
    {
        try
        {
            // Lets the tell that started the flush finish first, so a burst of sends in one turn
            // goes out as one batch.
            await Task.Yield();

            while (
                sessionGeneration == _state.SessionGeneration
                && _sessionObserver is { } observer
                && (_state.OutgoingQueue.Count > 0 || _activeRoomDirty)
            )
            {
                var batch = _state.OutgoingQueue.ToArray();

                _state.OutgoingQueue.Clear();
                _activeRoomDirty = false;

                var delivered = false;

                try
                {
                    await observer.SendComposersAsync(batch, _state.ActiveRoomId);

                    delivered = true;
                }
                finally
                {
                    // A failed call may not have set the room, so the next flush repeats it. The
                    // batch itself is lost (and logged below), as a single composer was before.
                    if (!delivered && sessionGeneration == _state.SessionGeneration)
                        _activeRoomDirty = true;
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
            if (sessionGeneration == _state.SessionGeneration)
                _state.IsProcessingQueue = false;
        }
    }
}
