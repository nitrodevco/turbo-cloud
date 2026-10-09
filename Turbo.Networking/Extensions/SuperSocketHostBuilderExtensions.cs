using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions.Host;
using SuperSocket.Server.Host;
using SuperSocket.WebSocket;
using Turbo.Networking.Package;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Revisions;

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
                    var revisionManager = sp.GetRequiredService<IRevisionManager>();
                    var logger =
                        sp.GetService<ILoggerFactory>()?.CreateLogger("Turbo.Networking.Session")
                        ?? NullLogger.Instance;

                    return new SessionHandlers
                    {
                        Connected = async session =>
                        {
                            if (session is ISessionContext ctx)
                            {
                                ctx.SetRevisionId(revisionManager.DefaultRevisionId);

                                await gateway
                                    .AddSessionAsync(ctx.SessionKey, ctx)
                                    .ConfigureAwait(false);
                            }
                        },
                        Closed = async (session, e) =>
                        {
                            if (session is not ISessionContext ctx)
                                return;

                            // Every close, whoever started it, so a disconnect a player reports
                            // can be explained from the log: SuperSocket's reason says who closed
                            // (RemoteClosing is the client, ServerShutdown a stop or deploy,
                            // SocketError a dropped network), the server's says why it did.
                            logger.LogInformation(
                                "Session {SessionKey} of player {PlayerId} closed: {CloseReason}, server reason: {ServerCloseReason}",
                                ctx.SessionKey,
                                gateway.GetPlayerId(ctx.SessionKey),
                                e.Reason,
                                ctx.ServerCloseReason ?? "none"
                            );

                            await gateway
                                .RemoveSessionAsync(ctx.SessionKey, CancellationToken.None)
                                .ConfigureAwait(false);
                        },
                    };
                });
            }
        );

        return builder;
    }

    /// <summary>
    /// Wraps the filter WebSocketHostBuilder registers so a message counts as heard from when it
    /// arrives, not when its handler runs (see <see cref="ReceiveMarkingFilter{TPackage}"/>).
    /// </summary>
    public static ISuperSocketHostBuilder<WebSocketPackage> UseReceiveMarking(
        this ISuperSocketHostBuilder<WebSocketPackage> builder
    ) =>
        builder.UsePipelineFilterFactory(() =>
            new ReceiveMarkingFilter<WebSocketPackage>(new WebSocketPipelineFilter())
        );
}
