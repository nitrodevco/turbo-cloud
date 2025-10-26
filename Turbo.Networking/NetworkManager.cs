using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperSocket.ProtoBase;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Host;
using Turbo.Messages;
using Turbo.Networking.Abstractions;
using Turbo.Networking.Abstractions.Revisions;
using Turbo.Networking.Configuration;
using Turbo.Networking.Extensions;
using Turbo.Networking.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking;

public sealed class NetworkManager(
    IOptions<NetworkingConfig> config,
    IRevisionManager revisionManager,
    MessageSystem messageSystem,
    ILoggerFactory loggerFactory
) : INetworkManager
{
    private readonly object _hostGate = new();
    private readonly NetworkingConfig _config = config.Value;
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly MessageSystem _messageSystem = messageSystem;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private IHost? _superSocketHost;

    public async Task StartAsync()
    {
        bool needStart = false;

        lock (_hostGate)
        {
            if (_superSocketHost is null)
            {
                CreateSuperSocket();
                needStart = true;
            }
        }

        if (needStart && _superSocketHost is not null)
            await _superSocketHost.StartAsync().ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        IHost? hostToStop = null;

        lock (_hostGate)
        {
            if (_superSocketHost is null)
                return;

            hostToStop = _superSocketHost;
            _superSocketHost = null;
        }

        if (hostToStop is not null)
        {
            await hostToStop.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
    }

    private void CreateSuperSocket()
    {
        var builder = SuperSocketHostBuilder.Create<IClientPacket>();

        builder.ConfigureLogging(
            (ctx, logging) =>
            {
                logging.ClearProviders();
            }
        );

        builder.ConfigureServices(
            (ctx, services) =>
            {
                services.AddSingleton(_config);
                services.AddSingleton(_revisionManager);
                services.AddSingleton(_messageSystem);
                services.AddSingleton(_loggerFactory);
                services.AddSingleton<IPackageEncoder<OutgoingPackage>, PackageEncoder>();
                services.AddSingleton<IPackageHandler<IClientPacket>, PackageHandler>();
            }
        );

        builder.UseSession<SessionContext>();
        builder.UsePipelineFilter<PackageFilter>();
        //builder.UsePingPong();

        _superSocketHost = builder.Build();
    }
}
