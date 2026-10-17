using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata.Habbo.Client;

/// <summary>
/// Builds the client's libraries from a release of Habbo's HTML client as soon as it is downloaded
/// (<see cref="GamedataConfig.ClientBuildAfterDownload"/>), rather than at the next asset sync: when
/// the release is the one the hotel follows, a client build of what changed in it starts once no
/// other asset job runs. The build is the server's own (player 0).
/// </summary>
internal sealed class ClientBuildAfterDownload(
    HabboClientService client,
    IAssetSyncService assets,
    IAssetJobs jobs,
    IOptions<GamedataConfig> config,
    TimeProvider time,
    ILogger<ClientBuildAfterDownload> logger
) : BackgroundService
{
    /// <summary>How often a running asset job is looked at again while a build waits for it.</summary>
    internal static readonly TimeSpan WAIT = TimeSpan.FromSeconds(15);

    private readonly Channel<string> _downloaded = Channel.CreateUnbounded<string>();

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        client.ReleaseDownloaded += OnDownloaded;

        return base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        client.ReleaseDownloaded -= OnDownloaded;
        _downloaded.Writer.TryComplete();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private void OnDownloaded(string version)
    {
        if (config.Value.ClientBuildAfterDownload)
            _downloaded.Writer.TryWrite(version);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (
                var version in _downloaded.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false)
            )
                await BuildAsync(version, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping: a release not built yet is built at the next sync.
        }
    }

    private async Task BuildAsync(string version, CancellationToken ct)
    {
        var status = await client.GetStatusAsync(ct).ConfigureAwait(false);

        if (status.Followed != version)
        {
            logger.LogInformation(
                "Habbo client release {Version} downloaded; the hotel follows {Followed}, so nothing is built",
                version,
                status.Followed
            );

            return;
        }

        while (true)
        {
            if (!jobs.Running)
            {
                try
                {
                    var job = assets.StartClientBuild(PlayerId.Parse(0), force: false);

                    logger.LogInformation(
                        "Started client build {JobId} after Habbo client release {Version} was downloaded",
                        job.Id,
                        version
                    );

                    return;
                }
                catch (InvalidOperationException)
                {
                    // Another job started between the look and the start: wait for it too.
                }
            }

            await Task.Delay(WAIT, time, ct).ConfigureAwait(false);
        }
    }
}
