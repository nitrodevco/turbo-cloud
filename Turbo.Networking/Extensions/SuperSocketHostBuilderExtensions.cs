using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions.Host;
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
}
