using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Turbo.Core.Configuration;
using Turbo.Core.Networking;
using Turbo.Networking.Hosts.Tcp;
using Turbo.Networking.Hosts.Websocket;

namespace Turbo.Networking.Factories;

public class NetworkServerFactory(IServiceProvider _provider) : INetworkServerFactory
{
    public IServer CreateServerFromConfig(INetworkHostConfig config)
    {
        return config.Type switch
        {
            NetworkHostType.Tcp => ActivatorUtilities.CreateInstance<TcpServer>(_provider, config),
            NetworkHostType.Websocket => ActivatorUtilities.CreateInstance<WebsocketServer>(_provider, config),
            _ => throw new NotSupportedException($"Network host type '{config.Type}' is not supported."),
        };
    }
}