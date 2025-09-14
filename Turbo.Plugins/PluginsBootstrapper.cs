using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Plugins.Configuration;

namespace Turbo.Plugins;

public class PluginsBootstrapper(
    PluginManager pluginManager,
    ILoggerFactory logger,
    PluginConfig config
) : IHostedService
{
    private readonly int _maxDop = Math.Clamp(Environment.ProcessorCount - 1, 1, 8);
    private readonly PluginManager _pluginManager = pluginManager;
    private readonly ILogger _logger = logger.CreateLogger(nameof(PluginsBootstrapper));
    private readonly PluginConfig _config = config;

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("Loading plugins from {Path} ...", _config.PluginFolderPath);

        await _pluginManager.LoadOrReloadAllPlugins(_maxDop, ct);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _pluginManager.DisposeAsync();
    }
}
