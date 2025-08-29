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
using Turbo.Core.Packets.Messages;
using Turbo.Core.Packets.Revisions;
using Turbo.Main.Delegates;
using Turbo.Networking;
using Turbo.Networking.Encryption;
using Turbo.Networking.Pipeline;
using Turbo.Networking.Session;
using Turbo.Packets;
using Turbo.Packets.Incoming;
using Turbo.Packets.Revisions;

namespace Turbo.Main.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddNetworking(this IServiceCollection services)
    {
        services.AddSingleton<IRevisionManager, RevisionManager>();
        services.AddSingleton<IPacketMessageHub, PacketMessageHub>();
        services.AddSingleton<IPacketProcessor, PacketProcessor>();
        services.AddSingleton<IRsaService, RsaService>();
        services.AddSingleton<IDiffieService, DiffieService>();
        services.AddSingleton<INetworkManager, NetworkManager>();
    }
}
