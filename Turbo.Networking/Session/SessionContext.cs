using System;
using System.Threading.Tasks;
using SuperSocket.Connection;
using SuperSocket.Server;
using Turbo.Core.Networking.Session;

namespace Turbo.Networking.Session;

public class SessionContext : AppSession, ISessionContext
{
    protected override async ValueTask OnSessionConnectedAsync()
    {
        Console.WriteLine($"Session context created: {SessionID}");
        await base.OnSessionConnectedAsync();
    }

    protected override async ValueTask OnSessionClosedAsync(CloseEventArgs e)
    {
        Console.WriteLine($"Session context closed: {SessionID}");
        await base.OnSessionClosedAsync(e);
    }
}
