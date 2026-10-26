using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Plugins;

namespace Turbo.Gamedata.Plugins;

/// <summary>
/// Once the server has started, its plugins loaded, keeps the themes the hotel had before themes had
/// an audience Live (<see cref="ThemeAudienceService.AdoptAsync"/>): the first start after the
/// migration only.
/// </summary>
internal sealed class ThemeAdoption(
    IHostApplicationLifetime lifetime,
    IThemeAudiences themes,
    ILogger<ThemeAdoption> logger
) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        if (themes is ThemeAudienceService service)
            lifetime.ApplicationStarted.Register(() =>
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await service
                            .AdoptAsync(lifetime.ApplicationStopping)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                        when (!lifetime.ApplicationStopping.IsCancellationRequested)
                    {
                        logger.LogError(
                            ex,
                            "Keeping the hotel's themes Live failed; it is tried again at the next start"
                        );
                    }
                })
            );

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
