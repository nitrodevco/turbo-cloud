using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.LoadBots.Client;
using Turbo.LoadBots.Metrics;

namespace Turbo.LoadBots.Behaviour;

/// <summary>
/// A bot living its life: logs in, then keeps choosing something to do from its persona's
/// plan until the run ends, logging back in if the server drops it.
/// </summary>
public sealed class BotAgent(BotContext ctx)
{
    private static readonly TimeSpan MAX_BACKOFF = TimeSpan.FromSeconds(30);

    public BotContext Context => ctx;

    public async Task RunAsync(CancellationToken ct)
    {
        var plan = PersonaPlans.For(ctx.Persona);
        var backoff = TimeSpan.FromSeconds(1);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (!ctx.Client.IsConnected)
                {
                    if (!await LogInAsync(ct))
                    {
                        await Task.Delay(backoff, ct);
                        backoff = TimeSpan.FromTicks(
                            Math.Min(MAX_BACKOFF.Ticks, backoff.Ticks * 2)
                        );

                        continue;
                    }

                    backoff = TimeSpan.FromSeconds(1);
                }

                var activity = PersonaPlans.Choose(plan, ctx.Random);

                await RunActivityAsync(activity, ct);
                await ctx.ThinkAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The run is over.
        }
        finally
        {
            await ctx.Client.DisconnectAsync();
        }
    }

    private async Task<bool> LogInAsync(CancellationToken ct)
    {
        await ctx.Client.DisconnectAsync();

        try
        {
            if (!await ctx.Client.ConnectAsync(ct))
            {
                await ctx.Client.DisconnectAsync();

                return false;
            }

            ctx.Metrics.Count("bots.logged_in");

            await ctx.Client.LoadInventoryAsync(ct);
            await ctx.Client.LoadCatalogIndexAsync(ct);

            return true;
        }
        catch (Exception ex) when (IsDisconnect(ex) && !ct.IsCancellationRequested)
        {
            // A server going down or restarting closes sockets mid-login; the bot retries with
            // the same backoff as a refused login instead of ending the whole run.
            ctx.Metrics.Count("session.login_interrupted_by_disconnect");
            ctx.Logger.LogDebug(ex, "{Bot} lost its connection while logging in", ctx.Client.Name);
            await ctx.Client.DisconnectAsync();

            return false;
        }
    }

    private async Task RunActivityAsync(Activity activity, CancellationToken ct)
    {
        try
        {
            await activity.RunAsync(ctx, ct);
            ctx.Metrics.Count($"activity.{activity.Name}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsDisconnect(ex))
        {
            ctx.Metrics.Count("activity.interrupted_by_disconnect");
            ctx.Logger.LogDebug(
                ex,
                "{Bot} lost its connection during {Activity}",
                ctx.Client.Name,
                activity.Name
            );
            await ctx.Client.DisconnectAsync();
        }
        catch (Exception ex)
        {
            ctx.Metrics.Check(
                "activity.completes",
                false,
                CheckSeverity.Hard,
                $"{ctx.Client.Name} {activity.Name}: {ex.GetType().Name}: {ex.Message}"
            );
            ctx.Logger.LogWarning(
                ex,
                "{Bot} failed during {Activity}",
                ctx.Client.Name,
                activity.Name
            );
        }
    }

    private bool IsDisconnect(Exception ex) =>
        ex is BotDisconnectedException or IOException or ObjectDisposedException
        || !ctx.Client.IsConnected;
}
