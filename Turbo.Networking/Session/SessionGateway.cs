using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Turbo.Networking.Abstractions.Session;

namespace Turbo.Networking.Session;

public sealed class SessionGateway : ISessionGateway
{
    private readonly ConcurrentDictionary<string, ISessionContext> _sessions = new();

    public void Register(string connectionId, ISessionContext session) =>
        _sessions[connectionId] = session;

    public void Unregister(string connectionId) => _sessions.TryRemove(connectionId, out _);

    public Task<bool> TryWriteAsync(string connectionId, ReadOnlyMemory<byte> payload)
    {
        if (_sessions.TryGetValue(connectionId, out var session))
        {
            return session.SendAsync(payload).AsTask().ContinueWith(t => t.IsCompletedSuccessfully);
        }

        return Task.FromResult(false);
    }
}
