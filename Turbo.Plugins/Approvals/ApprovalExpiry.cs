using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Turbo.Plugins.Approvals;

/// <summary>
/// Expires pending approvals past their time once a minute, so what waits on one (an agent's run,
/// a plugin's listener) hears of the expiry from <c>ApprovalDecidedEvent</c> without anyone
/// opening the inbox. Every server sweeps; the update that expires a request matches it once.
/// </summary>
internal sealed class ApprovalExpiry(
    HotelApprovals approvals,
    TimeProvider clock,
    ILogger<ApprovalExpiry> logger
) : BackgroundService
{
    private static readonly TimeSpan EVERY = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(EVERY, clock);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await approvals.ExpireDueAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(
                        ex,
                        "Expiring overdue approvals failed; trying again in a minute"
                    );
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }
}
