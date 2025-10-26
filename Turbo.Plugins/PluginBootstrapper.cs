using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Plugins;

public sealed class PluginBootstrapper(
    IEnumerable<IHostPluginModule> hostPlugins,
    PluginManager pluginManager,
    AssemblyProcessor processor,
    IServiceProvider serviceProvider
) : IHostedService
{
    private readonly IEnumerable<IHostPluginModule> _hostPlugins = hostPlugins;
    private readonly PluginManager _pluginManager = pluginManager;
    private readonly AssemblyProcessor _processor = processor;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public async Task StartAsync(CancellationToken ct)
    {
        var tasks = new List<Func<Task>>();

        foreach (var p in _hostPlugins)
        {
            tasks.Add(() => _processor.ProcessAsync(p.GetType().Assembly, _serviceProvider, ct));
        }

        await Task.WhenAll(tasks.Select(t => t())).ConfigureAwait(false);
        await _pluginManager.LoadAllAsync(true, ct).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _pluginManager.UnloadAllAsync(ct).ConfigureAwait(false);
    }
}
