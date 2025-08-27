using System;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Host;
using Turbo.Core.Configuration;
using Turbo.Core.Networking;
using Turbo.Core.Networking.Encryption;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Revisions;
using Turbo.Main.Delegates;
using Turbo.Networking;
using Turbo.Networking.Encryption;
using Turbo.Networking.Protocol;
using Turbo.Networking.Session;
using Turbo.Packets;
using Turbo.Packets.Revisions;

namespace Turbo.Main.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddNetworking(this IServiceCollection services)
    {
        services.AddSingleton<SessionManager>();
        services.AddSingleton<ISessionManager>(sp => sp.GetRequiredService<SessionManager>());
        services.AddSingleton<IPackageHandler, PackageHandler>();

        services.AddSingleton<TcpSocketHostFactory>(sp =>
            () =>
            {
                var config = sp.GetRequiredService<IEmulatorConfig>().Network.TcpServer;
                var packageHandler = sp.GetRequiredService<IPackageHandler>();

                var host = SuperSocketHostBuilder
                    .Create<Package, PipelineProcessor>()
                    .UseSession<SessionContext>()
                    .UseHostedService<SessionManager>()
                    .UsePackageHandler(
                        async (session, pkg) =>
                        {
                            await packageHandler.HandlePackageAsync((ISessionContext)session, pkg);
                        }
                    )
                    .ConfigureSuperSocket(o =>
                    {
                        o.Name = "TcpServer";
                        o.Listeners =
                        [
                            new ListenOptions
                            {
                                Ip = IPAddress.Parse(config.Host).ToString(),
                                Port = config.Port,
                            },
                        ];
                    });

                return host.Build();
            }
        );

        services.AddSingleton<IRsaService, RsaService>();
        services.AddSingleton<IDiffieService, DiffieService>();

        services.AddSingleton<IPacketMessageHub, PacketMessageHub>();
        services.AddSingleton<IRevisionManager, RevisionManager>();
    }
}
