using System;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SuperSocket.Connection;
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
        services.AddSingleton<IPackageBatchProcessor, PackageBatchProcessor>();
        services.AddSingleton<SessionContextFactory>();
        services.AddSingleton<INetworkIncomingQueueConfig>(sp =>
        {
            return sp.GetRequiredService<IEmulatorConfig>().Network.IncomingQueue;
        });

        services.AddSingleton<TcpSocketHostFactory>(sp =>
            () =>
            {
                var config = sp.GetRequiredService<IEmulatorConfig>();

                var host = SuperSocketHostBuilder
                    .Create<Package, PipelineProcessor>()
                    .UseSessionFactory<SessionContextFactory>()
                    .UseHostedService<SessionManager>()
                    .UsePackageHandler(
                        async (session, package) =>
                        {
                            var ctx = (ISessionContext)session;

                            switch (package.Type)
                            {
                                case PackageType.Policy:
                                    const string Policy =
                                        "<?xml version=\"1.0\"?>\r\n"
                                        + "<!DOCTYPE cross-domain-policy SYSTEM \"/xml/dtds/cross-domain-policy.dtd\">\r\n"
                                        + "<cross-domain-policy>\r\n"
                                        + "<allow-access-from domain=\"*\" to-ports=\"*\" />\r\n"
                                        + "</cross-domain-policy>\0"; // note the NUL

                                    await ctx.SendAsync(Encoding.Default.GetBytes(Policy));
                                    await ctx.CloseAsync(CloseReason.ServerShutdown);
                                    break;
                                case PackageType.Client:
                                    await ctx.EnqueueAsync(package, default);
                                    break;
                            }
                        }
                    )
                    .ConfigureSuperSocket(o =>
                    {
                        o.Name = "TcpServer";
                        o.Listeners =
                        [
                            new ListenOptions
                            {
                                Ip = IPAddress.Parse(config.Network.TcpServer.Host).ToString(),
                                Port = config.Network.TcpServer.Port,
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
