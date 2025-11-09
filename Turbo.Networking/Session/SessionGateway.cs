using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Abstractions;
using Turbo.Networking.Abstractions.Session;

namespace Turbo.Networking.Session;

public sealed class SessionGateway : ISessionGateway
{
    private readonly ConcurrentDictionary<string, ISessionContext> _sessions = new();
    private readonly ConcurrentDictionary<long, string> _playerSessions = new();

    public void RegisterSession(ISessionContext session) =>
        _sessions.TryAdd(session.SessionID, session);

    public void UnregisterSession(string connectionId) => _sessions.TryRemove(connectionId, out _);

    public string? GetConnectionIdByPlayerId(long playerId) =>
        _playerSessions.TryGetValue(playerId, out var connectionId) ? connectionId : null;

    public ISessionContext? GetSessionContextByConnectionId(string connectionId) =>
        _sessions.TryGetValue(connectionId, out var ctx) ? ctx : null;

    public ISessionContext? GetSessionContextByPlayerId(long playerId)
    {
        var connectionId = GetConnectionIdByPlayerId(playerId);

        if (connectionId is null)
            return null;

        var ctx = GetSessionContextByConnectionId(connectionId);

        if (ctx is null)
            return null;

        return ctx;
    }

    public Task SetPlayerIdForSessionAsync(long playerId, string connectionId)
    {
        if (!_sessions.TryGetValue(connectionId, out var session))
            return Task.CompletedTask;
        if (session.PlayerId > 0)
        {
            _playerSessions.TryRemove(session.PlayerId, out _);
        }

        session.SetPlayerId(playerId);

        if (session.PlayerId <= 0)
            return Task.CompletedTask;
        _playerSessions.TryAdd(session.PlayerId, connectionId);
        return Task.CompletedTask;
    }

    public async Task SendComposerToPlayerAsync(
        IComposer composer,
        long playerId,
        CancellationToken ct = default
    )
    {
        if (!_playerSessions.TryGetValue(playerId, out var connectionId))
            return;

        if (!_sessions.TryGetValue(connectionId, out var ctx))
            return;

        await ctx.SendComposerAsync(composer, ct).ConfigureAwait(false);
    }
}
