using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions.Host;
using Turbo.Networking.Configuration;
using Turbo.Primitives.Networking;

namespace Turbo.Networking.Extensions;

public static class SuperSocketHostBuilderExtensions
{
    public static ISuperSocketHostBuilder<TPackage> UseSessionGateway<TPackage>(
        this ISuperSocketHostBuilder<TPackage> builder
    )
    {
        builder.ConfigureServices(
            delegate(HostBuilderContext hostCtx, IServiceCollection services)
            {
                services.AddSingleton(sp =>
                {
                    var gateway = sp.GetRequiredService<ISessionGateway>();

                    return new SessionHandlers
                    {
                        Connected = async session =>
                        {
                            if (session is ISessionContext ctx)
                                await gateway
                                    .AddSessionAsync(ctx.SessionKey, ctx)
                                    .ConfigureAwait(false);
                        },
                        Closed = async (session, e) =>
                        {
                            if (session is ISessionContext ctx)
                                await gateway
                                    .RemoveSessionAsync(ctx.SessionKey)
                                    .ConfigureAwait(false);
                        },
                    };
                });
            }
        );

        return builder;
    }

    public static ISuperSocketHostBuilder<TPackage> UsePingPong<TPackage>(
        this ISuperSocketHostBuilder<TPackage> builder
    )
    {
        builder.ConfigureServices(
            delegate(HostBuilderContext hostCtx, IServiceCollection services)
            {
                services.AddSingleton(sp =>
                {
                    var config = sp.GetRequiredService<NetworkingConfig>();

                    return new SessionHandlers
                    {
                        Connected = session =>
                        {
                            if (session is not ISessionContext ctx)
                                return new ValueTask();
                            ctx.Touch();

                            _ = RunHeartbeatAsync(ctx, config, ctx.HeartbeatCts.Token);
                            return new ValueTask();
                        },
                        Closed = async (session, e) =>
                        {
                            if (session is not ISessionContext ctx)
                                return;

                            await ctx.HeartbeatCts.CancelAsync().ConfigureAwait(false);

                            ctx.HeartbeatCts.Dispose();
                        },
                    };
                });
            }
        );

        return builder;
    }

    private static Task RunHeartbeatAsync(
        ISessionContext session,
        NetworkingConfig cfg,
        CancellationToken ct
    )
    {
        return Task.CompletedTask;
        /*  var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(cfg.PingIntervalMilliseconds));

try
{
while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
{
var last = session.LastActivityUtc;

if (DateTime.UtcNow - last <= cfg.IdleOkActivityWindow)
continue;

await session.SendComposerAsync(new PingMessage(), ct).ConfigureAwait(false);

// Wait for PONG
if (
session.Items.TryGetValue(HeartbeatSessionKeys.PongWaiter, out var waiterObj)
&& waiterObj is AsyncSignal waiter
)
{
var completed =
await Task.WhenAny(waiter.WaitAsync(cfg.PongTimeout, ct)) is { };
if (!completed)
{
// No PONG — close session
await session.CloseAsync(CloseReason.TimeOut);
return;
}

// Mark activity on pong
session.Items[HeartbeatSessionKeys.LastActivityUtc] = DateTime.UtcNow;

// Reset waiter for next cycle
session.Items[HeartbeatSessionKeys.PongWaiter] = new AsyncSignal();
}
}
}
catch (OperationCanceledException)
{
}
finally
{
timer.Dispose();
} */
    }
}
