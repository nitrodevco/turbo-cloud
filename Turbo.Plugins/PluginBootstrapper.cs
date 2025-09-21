using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Turbo.Plugins;

public sealed class PluginBootstrapper(PluginManager pluginManager) : IHostedService
{
    private readonly PluginManager _pluginManager = pluginManager;

    public async Task StartAsync(CancellationToken ct)
    {
        await _pluginManager.LoadAll(true, ct);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await Task.CompletedTask;
    }
}
