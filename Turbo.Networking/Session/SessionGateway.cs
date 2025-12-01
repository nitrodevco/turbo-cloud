using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Networking.Session;

public sealed class SessionGateway(IGrainFactory grainFactory) : ISessionGateway
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    private readonly ConcurrentDictionary<string, ISessionContext> _sessions = new();
    private readonly ConcurrentDictionary<string, ObserverEntry> _sessionObservers = new();
    private readonly ConcurrentDictionary<string, long> _sessionToPlayer = new();
    private readonly ConcurrentDictionary<long, string> _playerToSession = new();

    private sealed record ObserverEntry(SessionContextObserver Impl, ISessionContextObserver Ref);

    public ISessionContext? GetSession(SessionKey key) =>
        _sessions.TryGetValue(key.Value, out var ctx) ? ctx : null;

    public ISessionContextObserver? GetSessionObserver(SessionKey key) =>
        _sessionObservers.TryGetValue(key.Value, out var observer) ? observer.Ref : null;

    public long GetPlayerId(SessionKey key) =>
        _sessionToPlayer.TryGetValue(key.Value, out var playerId) ? playerId : -1;

    public Task AddSessionAsync(SessionKey key, ISessionContext ctx)
    {
        _sessions[key.Value] = ctx;

        _sessionObservers.AddOrUpdate(
            key.Value,
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

        if (_sessionObservers.TryRemove(key.Value, out var observer))
        {
            try
            {
                _grainFactory.DeleteObjectReference<ISessionContextObserver>(observer.Ref);
            }
            catch (Exception) { }
        }

        if (_sessions.TryRemove(key.Value, out _)) { }
    }

    public async Task AddSessionToPlayerAsync(SessionKey key, long playerId)
    {
        var observer = GetSessionObserver(key);

        if (observer is null)
            return;

        var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);

        _sessionToPlayer[key.Value] = playerId;
        _playerToSession[playerId] = key.Value;

        await playerPresence.RegisterSessionAsync(key, observer).ConfigureAwait(false);
    }

    public async Task RemoveSessionFromPlayerAsync(long playerId, CancellationToken ct)
    {
        if (!_playerToSession.TryRemove(playerId, out var sessionKeyValue))
            return;

        _sessionToPlayer.TryRemove(sessionKeyValue, out _);

        var playerPresence = _grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);

        await playerPresence
            .UnregisterSessionAsync(SessionKey.From(sessionKeyValue), ct)
            .ConfigureAwait(false);
    }
}
