using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Turbo.Plugins;

public sealed class PluginBootstrapper(PluginManager pluginManager) : IHostedService
{
    private readonly PluginManager _pluginManager = pluginManager;

    public async Task StartAsync(CancellationToken ct)
    {
        await _pluginManager.LoadAllAsync(true, ct).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _pluginManager.UnloadAllAsync(ct).ConfigureAwait(false);
    }
}
