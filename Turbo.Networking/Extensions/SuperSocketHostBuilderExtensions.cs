using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
                            if (session is ISessionContext ctx)
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
