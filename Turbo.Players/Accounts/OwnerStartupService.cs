using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Turbo.Players.Accounts;

/// <summary>
/// Once the server is up, makes sure the configured owner, if they have already signed up, holds
/// what an owner holds, and has the admin panel offer their setup link if they have no passkey.
/// </summary>
internal sealed class OwnerStartupService(
    OwnerBootstrap owner,
    IHostApplicationLifetime lifetime,
    ILogger<OwnerStartupService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var started = lifetime.ApplicationStarted.Register(() => ready.TrySetResult());

        try
        {
            await ready.Task.WaitAsync(stoppingToken).ConfigureAwait(false);
            await owner.EnsureExistingAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Checking the hotel's owner at startup failed");
        }
    }
}
