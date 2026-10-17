using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Plugins.Configuration;

namespace Turbo.Plugins.Sources;

/// <summary>
/// Asks GitHub for a new release of every linked plugin every
/// <see cref="PluginConfig.UpdateCheckMinutes"/>, from startup on, as Habbo is asked for a new
/// release: the panel shows an update waiting, and staff take it in, unless the link updates on
/// its own. One failed check (GitHub down, a token expired) must not stop the next.
/// </summary>
internal sealed class PluginUpdateWatcher(
    PluginSources sources,
    IOptions<PluginConfig> config,
    ILogger<PluginUpdateWatcher> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = config.Value.UpdateCheckMinutes;

        if (minutes <= 0)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));

        do
        {
            try
            {
                await sources.CheckAllAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Checking the linked plugins for updates failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
