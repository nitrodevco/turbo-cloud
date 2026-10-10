using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata.Habbo;

/// <summary>
/// Asks Habbo for a new release every <see cref="GamedataConfig.ReleaseCheckMinutes"/>, from
/// startup on. It only finds releases: the panel shows one waiting, and staff take it in after
/// looking at what it changes. Each silo checks; a release found twice is kept once. A check that
/// answered goes on to sync the asset bundles (<see cref="IAssetSyncService.StartAfterCheck"/>).
/// </summary>
internal sealed class HabboReleaseWatcher(
    IHabboReleaseService releases,
    IAssetSyncService assets,
    IOptions<GamedataConfig> config,
    ILogger<HabboReleaseWatcher> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = config.Value.ReleaseCheckMinutes;

        if (minutes <= 0)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));

        do
        {
            await CheckAsync(stoppingToken).ConfigureAwait(false);
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    // One failed check (Habbo down, its filter refusing) must not stop the next.
    private async Task CheckAsync(CancellationToken ct)
    {
        try
        {
            var result = await releases.CheckAsync(ct).ConfigureAwait(false);

            if (result.IsNew)
                logger.LogInformation(
                    "Habbo release {Revision} found; its furniture waits to be imported in the admin panel",
                    result.Release.Revision
                );

            if (result.ProductsAreNew)
                logger.LogInformation(
                    "New Habbo product data found ({Count}); it waits to be imported in the admin panel",
                    result.Products.ProductCount
                );

            if (result.TextsAreNew)
                logger.LogInformation(
                    "New Habbo external texts found ({Count}); they wait to be imported in the admin panel",
                    result.Texts.TextCount
                );

            // The timer is no player's: a sync it starts is the server's own (player 0).
            assets.StartAfterCheck(PlayerId.Parse(0));
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "Habbo could not be checked for a new release: {Message}",
                ex.Message
            );
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Checking Habbo for a new release failed");
        }
    }
}
