using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperSocket.Server.Host;
using Turbo.Networking.Abstractions;
using Turbo.Networking.Abstractions.Session;
using Turbo.Networking.Configuration;
using Turbo.Networking.Pipeline;
using Turbo.Networking.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking;

public sealed class NetworkManager(
    IOptions<NetworkingConfig> config,
    PacketProcessor packetProcessor,
    ILogger<NetworkManager> logger
) : INetworkManager
{
    private readonly object _hostGate = new();
    private readonly NetworkingConfig _config = config.Value;
    private readonly PacketProcessor _packetProcessor = packetProcessor;
    private readonly ILogger<NetworkManager> _logger = logger;
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
        var host = SuperSocketHostBuilder
            .Create<IClientPacket>()
            .ConfigureServices(
                (ctx, services) =>
                {
                    services.AddSingleton(_config);
                    services.AddSingleton(_packetProcessor);
                    services.AddSingleton<PipelineFilter>();
                }
            )
            .UseSessionFactory<SessionContextFactory>()
            .UsePipelineFilter<PipelineFilter>()
            //.UsePipelineFilterFactory<PipelineFilterFactory>()
            .UsePackageHandler(
                async (session, packet) =>
                {
                    if (packet is null)
                        return;

                    var ctx = (ISessionContext)session;

                    try
                    {
                        await _packetProcessor.ProcessPacket(ctx, packet, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to process packet {Packet} for session {SessionId}",
                            packet,
                            session.SessionID
                        );
                    }
                }
            );

        _superSocketHost = host.Build();
    }
}
