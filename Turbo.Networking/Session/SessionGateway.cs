using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Orleans;
using Turbo.Networking.Abstractions.Session;
using Turbo.Primitives.Players;

namespace Turbo.Networking.Session;

public sealed class SessionGateway(IGrainFactory grainFactory) : ISessionGateway
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly ConcurrentDictionary<string, ISessionContext> _sessions = new();

    public void Register(string connectionId, ISessionContext session) =>
        _sessions[connectionId] = session;

    public void Unregister(string connectionId) => _sessions.TryRemove(connectionId, out _);

    public async Task SetPlayerIdForSessionAsync(long playerId, string connectionId)
    {
        if (!_sessions.TryGetValue(connectionId, out var session))
            return;

        if (session.PlayerId > 0)
        {
            var existingGrain = _grainFactory.GetGrain<IPlayerEndpointGrain>(session.PlayerId);

            await existingGrain.UnbindConnectionAsync(session.SessionID).ConfigureAwait(false);
        }

        session.SetPlayerId(playerId);

        if (session.PlayerId <= 0)
            return;

        var endpointGrain = _grainFactory.GetGrain<IPlayerEndpointGrain>(session.PlayerId);

        await endpointGrain.BindConnectionAsync(session.SessionID).ConfigureAwait(false);
    }

    public Task<bool> TryWriteAsync(string connectionId, ReadOnlyMemory<byte> payload)
    {
        if (_sessions.TryGetValue(connectionId, out var session))
        {
            return session.SendAsync(payload).AsTask().ContinueWith(t => t.IsCompletedSuccessfully);
        }

        return Task.FromResult(false);
    }
}
