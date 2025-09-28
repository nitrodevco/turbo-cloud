using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins;

namespace Turbo.Authentication;

public sealed class AuthenticationSystem : IHostPlugin
{
    public string Key => "turbo-authentication";

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
