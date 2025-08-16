using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Turbo.Core.Configuration;
using Turbo.Core.Networking;
using Turbo.Core.Networking.Servers;
using Turbo.Networking.Servers.Tcp;
using Turbo.Networking.Servers.Websocket;

namespace Turbo.Networking;

public class NetworkManager(
    ILogger<NetworkManager> logger,
    INetworkEventLoopGroup eventLoopGroup,
    IServiceProvider provider
) : INetworkManager
{
    private readonly ILogger<NetworkManager> _logger = logger;
    private readonly INetworkEventLoopGroup _eventLoopGroup = eventLoopGroup;
    private readonly IServiceProvider _provider = provider;

    public List<IServer> Servers { get; } = [];

    public async Task StartServersAsync()
    {
        await Task.WhenAll(Servers.Select(x => x.StartAsync()));
    }

    public async Task StopServersAsync()
    {
        await Task.WhenAll(Servers.Select(x => x.StopAsync()));
        await _eventLoopGroup.Worker.ShutdownGracefullyAsync();
        await _eventLoopGroup.Boss.ShutdownGracefullyAsync();
    }

    public void SetupServers(IList<INetworkServerConfig> hostConfigs)
    {
        if (hostConfigs is null || hostConfigs.Count == 0)
        {
            return;
        }

        foreach (var config in hostConfigs)
        {
            CreateServer(config);
        }
    }

    private IServer CreateServer(INetworkServerConfig config)
    {
        if (config is null)
        {
            _logger.LogError("Network host configuration cannot be null.");

            return null;
        }

        IServer server = null;

        if (config.Type == NetworkServerType.Tcp)
        {
            server = ActivatorUtilities.CreateInstance<TcpServer>(_provider, config);
        }

        if (config.Type == NetworkServerType.Websocket)
        {
            server = ActivatorUtilities.CreateInstance<WebsocketServer>(_provider, config);
        }

        if (server is null)
        {
            _logger.LogError($"Failed to create server for configuration: {config.Type}");

            return null;
        }

        Servers.Add(server);

        return server;
    }
}
