using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Turbo.Main.Settings;

/// <summary>
/// Asks every <see cref="ServerSettingsConfig.CheckSeconds"/> whether the panel's overrides
/// changed in the database (<see cref="ServerSettingsService.CheckAsync"/>): a setting saved on
/// another silo reloads only that silo's configuration, so this one reads it here.
/// </summary>
internal sealed class ServerSettingsWatcher(
    ServerSettingsService settings,
    IOptions<ServerSettingsConfig> config,
    ILogger<ServerSettingsWatcher> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = config.Value.CheckSeconds;

        if (seconds <= 0)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await settings.CheckAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // The database unreachable for a moment: the overrides read last stay, and the
                // next tick asks again.
                logger.LogError(ex, "Checking the panel's server settings failed");
            }
        }
    }
}
