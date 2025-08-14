using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Turbo.Core.Networking.Session;

namespace Turbo.Networking.Session;

public class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<IChannelId, ISession> _clients = new();

    public bool TryGetSession(IChannelId id, out ISession session)
    {
        return _clients.TryGetValue(id, out session);
    }

    public bool TryRegisterSession(IChannelId id, in ISession session)
    {
        return _clients.TryAdd(id, session);
    }

    public async Task DisconnectSession(IChannelId id)
    {
        if (!_clients.TryRemove(id, out var session)) return;

        await session.DisposeAsync();
    }

    private async Task Process()
    {
        var tasks = new ConcurrentBag<Task>();

        foreach (var session in _clients.Values)
        {
            if (session is null) continue;

            try
            {
                tasks.Add(session.HandleDecodedMessages());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating task for session: {ex}");
            }
        }

        await Task.WhenAll(tasks);
    }

    public async Task Cycle()
    {
        try
        {
            await Process();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}