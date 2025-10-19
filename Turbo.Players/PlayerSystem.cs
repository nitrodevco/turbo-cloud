using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins;

namespace Turbo.Players;

public sealed class PlayerSystem : IHostPlugin
{
    public string Key => "turbo-players";

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
