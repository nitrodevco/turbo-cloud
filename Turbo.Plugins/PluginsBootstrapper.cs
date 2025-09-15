using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Turbo.Plugins;

public class PluginsBootstrapper(PluginManager pluginManager) : IHostedService
{
    private readonly int _maxDop = Math.Clamp(Environment.ProcessorCount - 1, 1, 8);
    private readonly PluginManager _pluginManager = pluginManager;

    public async Task StartAsync(CancellationToken ct)
    {
        await _pluginManager.LoadOrReloadAllPlugins(_maxDop, ct);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _pluginManager.DisposeAsync();
    }
}
