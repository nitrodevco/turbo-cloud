using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins;

namespace Turbo.PacketHandlers;

public sealed class PacketHandlersSystem : IHostPlugin
{
    public string Key => "turbo-packet-handlers";

    public Task StartAsync(CancellationToken ct)
    {
        // Initialization logic here
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        // Cleanup logic here
        return Task.CompletedTask;
    }
}
