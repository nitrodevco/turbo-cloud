using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Turbo.Core.Configuration;
using Turbo.Core.Networking;
using Turbo.Networking.Factories;
using Turbo.Networking.Hosts.Websocket;

namespace Turbo.Networking;

public class NetworkManager(
    ILogger<NetworkManager> logger,
    INetworkServerFactory networkServerFactory) : INetworkManager
{
    private readonly INetworkServerFactory _networkServerFactory = networkServerFactory;
    private readonly ILogger<NetworkManager> _logger = logger;

    public List<IServer> Servers { get; } = [];

    public async Task StartServersAsync()
    {
        foreach (var server in Servers)
        {
            await server.StartAsync();
        }
    }

    public void SetupServers(IList<INetworkHostConfig> hostConfigs)
    {
        if (hostConfigs is null || hostConfigs.Count == 0)
        {
            _logger.LogWarning("No network host configurations provided. Skipping setup.");

            return;
        }

        foreach (var config in hostConfigs)
        {
            SetupHost(config);
        }
    }

    private void SetupHost(INetworkHostConfig config)
    {
        if (config is null)
        {
            _logger.LogError("Network host configuration cannot be null.");
            return;
        }

        var server = _networkServerFactory.CreateServerFromConfig(config);

        if (server is null)
        {
            _logger.LogError($"Failed to create server for configuration: {config.Type}");
            return;
        }

        Servers.Add(server);
    }
}