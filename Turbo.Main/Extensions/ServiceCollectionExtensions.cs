using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Core.Configuration;
using Turbo.Core.Networking;
using Turbo.Core.Networking.Dispatcher;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Revisions;
using Turbo.Networking;
using Turbo.Networking.Dispatcher;
using Turbo.Networking.Session;
using Turbo.Packets;
using Turbo.Packets.Revisions;

namespace Turbo.Main.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddNetworking(this IServiceCollection services)
    {
        services.AddSingleton<INetworkEventLoopGroup, NetworkEventLoopGroup>();
        services.AddSingleton<INetworkManager, NetworkManager>();

        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<IPacketMessageHub, PacketMessageHub>();
        services.AddSingleton<IRevisionManager, RevisionManager>();
        services.AddSingleton<IPacketDispatcher, PacketDispatcher>();
        services.AddSingleton<IRateLimiter, TokenBucketRateLimiter>();
        services.AddSingleton<IPacketQueue, BoundedPacketQueue>();
        services.AddSingleton<IBackpressureManager, BackpressureManager>();
        services.AddHostedService(sp => sp.GetRequiredService<IPacketDispatcher>());
    }
}
