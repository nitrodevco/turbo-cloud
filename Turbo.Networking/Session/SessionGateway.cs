using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Players;

namespace Turbo.Networking.Session;

public sealed class SessionGateway(IGrainFactory grainFactory) : ISessionGateway
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    private readonly ConcurrentDictionary<SessionKey, ISessionContext> _sessions = new();
    private readonly ConcurrentDictionary<SessionKey, ObserverEntry> _sessionObservers = new();
    private readonly ConcurrentDictionary<SessionKey, PlayerId> _sessionToPlayer = new();
    private readonly ConcurrentDictionary<PlayerId, SessionKey> _playerToSession = new();

    private sealed record ObserverEntry(SessionContextObserver Impl, ISessionContextObserver Ref);

    public ISessionContext? GetSession(SessionKey key) =>
        _sessions.TryGetValue(key, out var ctx) ? ctx : null;

    public ISessionContextObserver? GetSessionObserver(SessionKey key) =>
        _sessionObservers.TryGetValue(key, out var observer) ? observer.Ref : null;

    public PlayerId GetPlayerId(SessionKey key) =>
        _sessionToPlayer.TryGetValue(key, out var playerId) ? playerId : -1;

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
        var playerId = GetPlayerId(key);

        if (playerId > 0)
            await RemoveSessionFromPlayerAsync(playerId, ct).ConfigureAwait(false);

        if (_sessionObservers.TryRemove(key, out var observer))
        {
            try
            {
                _grainFactory.DeleteObjectReference<ISessionContextObserver>(observer.Ref);
            }
            catch (Exception) { }
        }

        if (_sessions.TryRemove(key, out _)) { }
    }

    public async Task AddSessionToPlayerAsync(SessionKey key, PlayerId playerId)
    {
        var observer = GetSessionObserver(key);

        if (observer is null)
            return;

        var playerPresence = _grainFactory.GetPlayerPresenceGrain(playerId);

        _sessionToPlayer[key] = playerId;
        _playerToSession[playerId] = key;

        await playerPresence.RegisterSessionAsync(key, observer).ConfigureAwait(false);
    }

    public async Task RemoveSessionFromPlayerAsync(PlayerId playerId, CancellationToken ct)
    {
        if (!_playerToSession.TryRemove(playerId, out var sessionKey))
            return;

        _sessionToPlayer.TryRemove(sessionKey, out _);

        var playerPresence = _grainFactory.GetPlayerPresenceGrain(playerId);

        await playerPresence.UnregisterSessionAsync(sessionKey, ct).ConfigureAwait(false);
    }
}
