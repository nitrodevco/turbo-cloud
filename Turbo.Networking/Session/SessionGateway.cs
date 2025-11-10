using System.Collections.Concurrent;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Networking.Session;

public sealed class SessionGateway : ISessionGateway
{
    private readonly ConcurrentDictionary<string, ISessionContext> _sessions = new();
    private readonly ConcurrentDictionary<string, long> _sessionToPlayer = new();
    private readonly ConcurrentDictionary<long, string> _playerToSession = new();

    public ISessionContext? GetSession(SessionKey key) =>
        _sessions.TryGetValue(key.Value, out var ctx) ? ctx : null;

    public void AddSession(SessionKey key, ISessionContext ctx) => _sessions[key.Value] = ctx;

    public void RemoveSession(SessionKey key) => _sessions.TryRemove(key.Value, out _);

    public long GetPlayerId(SessionKey key) =>
        _sessionToPlayer.TryGetValue(key.Value, out var playerId) ? playerId : -1;

    public void BindSessionToPlayer(SessionKey key, long playerId)
    {
        _sessionToPlayer[key.Value] = playerId;
        _playerToSession[playerId] = key.Value;
    }

    public void UnbindSessionFromPlayer(SessionKey key)
    {
        if (_sessionToPlayer.TryRemove(key.Value, out var playerId))
            _playerToSession.TryRemove(playerId, out _);
    }
}
