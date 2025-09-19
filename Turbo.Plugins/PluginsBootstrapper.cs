using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Turbo.Plugins;

public class PluginsBootstrapper(PluginManager pluginManager) : IHostedService
{
    private readonly PluginManager _pluginManager = pluginManager;

    public async Task StartAsync(CancellationToken ct)
    {
        await _pluginManager.LoadAll(ct);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await Task.CompletedTask;
    }
}
