using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Players;

namespace Turbo.Networking.Session;

public sealed class SessionGateway(IGrainFactory grainFactory, ILogger<ISessionGateway> logger)
    : ISessionGateway
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly ILogger<ISessionGateway> _logger = logger;

    private readonly ConcurrentDictionary<SessionKey, ISessionContext> _sessions = new();
    private readonly ConcurrentDictionary<SessionKey, ObserverEntry> _sessionObservers = new();
    private readonly ConcurrentDictionary<SessionKey, PlayerId> _sessionToPlayer = new();
    private readonly ConcurrentDictionary<PlayerId, SessionKey> _playerToSession = new();

    private sealed record ObserverEntry(SessionContextObserver Impl, ISessionContextObserver Ref);

    public ISessionContext? GetSession(SessionKey key) =>
        _sessions.TryGetValue(key, out var ctx) ? ctx : null;

    public IReadOnlyCollection<ISessionContext> GetSessions() => [.. _sessions.Values];

    public ISessionContextObserver? GetSessionObserver(SessionKey key) =>
        _sessionObservers.TryGetValue(key, out var observer) ? observer.Ref : null;

    public PlayerId GetPlayerId(SessionKey key) =>
        _sessionToPlayer.TryGetValue(key, out var playerId)
        && _playerToSession.TryGetValue(playerId, out var currentKey)
        && currentKey == key
            ? playerId
            : -1;

    public Task AddSessionAsync(SessionKey key, ISessionContext ctx)
    {
        _sessions[key] = ctx;

        _sessionObservers.AddOrUpdate(
            key,
            _ =>
            {
                var impl = new SessionContextObserver(key, this);
                var objRef = _grainFactory.CreateObjectReference<ISessionContextObserver>(impl);

                return new ObserverEntry(impl, objRef);
            },
            (_, existing) => existing
        );

        return Task.CompletedTask;
    }

    public async Task RemoveSessionAsync(SessionKey key, CancellationToken ct)
    {
        if (_sessionToPlayer.TryRemove(key, out var playerId))
        {
            // Remove only this binding: a reconnect may already have replaced it.
            _playerToSession.TryRemove(new KeyValuePair<PlayerId, SessionKey>(playerId, key));
            await _grainFactory
                .GetPlayerPresenceGrain(playerId)
                .UnregisterSessionObserverAsync(key, ct)
                .ConfigureAwait(false);
        }

        if (_sessionObservers.TryRemove(key, out var observer))
        {
            try
            {
                _grainFactory.DeleteObjectReference<ISessionContextObserver>(observer.Ref);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to delete the session observer reference for session {SessionKey}",
                    key
                );
            }
        }

        _sessions.TryRemove(key, out _);
    }

    public async Task AddSessionToPlayerAsync(SessionKey key, PlayerId playerId)
    {
        var observer = GetSessionObserver(key);

        if (observer is null)
            return;

        var playerPresence = _grainFactory.GetPlayerPresenceGrain(playerId);

        _sessionToPlayer[key] = playerId;
        var previousKey = SessionKey.Invalid;
        while (true)
        {
            if (_playerToSession.TryGetValue(playerId, out previousKey))
            {
                if (_playerToSession.TryUpdate(playerId, key, previousKey))
                    break;
            }
            else if (_playerToSession.TryAdd(playerId, key))
            {
                previousKey = SessionKey.Invalid;
                break;
            }
        }

        await playerPresence
            .RegisterSessionObserverAsync(key, observer, CancellationToken.None)
            .ConfigureAwait(false);

        if (previousKey != SessionKey.Invalid && previousKey != key)
        {
            // The old connection is already unauthenticated by GetPlayerId. Address it directly:
            // player presence now routes to the replacement and must not receive this logout.
            var previousSession = GetSession(previousKey);
            if (previousSession is not null)
            {
                try
                {
                    await previousSession
                        .SendComposerAsync(
                            new DisconnectReasonEventMessageComposer
                            {
                                Reason = DisconnectReasonEventMessageComposer.ConcurrentLogin,
                            },
                            CancellationToken.None
                        )
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to notify replaced session {SessionKey}",
                        previousKey
                    );
                }

                await previousSession.CloseSessionAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task RemoveSessionFromPlayerAsync(PlayerId playerId, CancellationToken ct)
    {
        if (!_playerToSession.TryRemove(playerId, out var sessionKey))
            return;

        _sessionToPlayer.TryRemove(sessionKey, out _);

        var playerPresence = _grainFactory.GetPlayerPresenceGrain(playerId);

        await playerPresence.UnregisterSessionObserverAsync(sessionKey, ct).ConfigureAwait(false);
    }
}
