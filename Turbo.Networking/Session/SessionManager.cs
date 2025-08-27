using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SuperSocket.Connection;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Abstractions.Session;
using Turbo.Core.Configuration;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Networking.Session;
using Turbo.Networking.Protocol;

namespace Turbo.Networking.Session;

public class SessionManager(IServiceProvider serviceProvider, IOptions<ServerOptions> serverOptions)
    : SuperSocketService<Package>(serviceProvider, serverOptions),
        ISessionManager
{
    private readonly ConcurrentDictionary<string, ISessionContext> _sessions = new();

    protected override async ValueTask OnSessionConnectedAsync(IAppSession session)
    {
        await base.OnSessionConnectedAsync(session);
    }

    protected override async ValueTask OnSessionClosedAsync(
        IAppSession session,
        CloseEventArgs reason
    )
    {
        await base.OnSessionClosedAsync(session, reason);
    }
}
