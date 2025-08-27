using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SuperSocket.Connection;
using SuperSocket.Server;
using Turbo.Core;
using Turbo.Core.Configuration;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Networking.Session;

namespace Turbo.Networking.Session;

public class SessionContext : AppSession, ISessionContext
{
    private IncomingQueue _incomingQueue;

    public SessionContext(IEmulatorConfig config, IPackageBatchProcessor packageBatchProcessor)
        : base()
    {
        _incomingQueue = new(this, config.Network.IncomingQueue, packageBatchProcessor);
    }

    protected override async ValueTask OnSessionConnectedAsync()
    {
        _incomingQueue.Start();

        await base.OnSessionConnectedAsync();

        Console.WriteLine($"Session context created: {SessionID}");
    }

    protected override async ValueTask OnSessionClosedAsync(CloseEventArgs e)
    {
        await _incomingQueue.StopAsync();

        await base.OnSessionClosedAsync(e);

        Console.WriteLine($"Session context closed: {SessionID}");
    }

    public async ValueTask EnqueueAsync(Package package, CancellationToken ct)
    {
        await _incomingQueue.EnqueueAsync(package, ct);
    }
}
