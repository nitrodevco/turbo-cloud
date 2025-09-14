using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SuperSocket.Server.Host;
using Turbo.Networking.Abstractions;
using Turbo.Networking.Abstractions.Session;
using Turbo.Networking.Configuration;
using Turbo.Networking.Pipeline;
using Turbo.Networking.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking;

public class NetworkManager(NetworkingConfig config, PacketProcessor packetProcessor)
    : INetworkManager
{
    private readonly NetworkingConfig _config = config;
    private readonly PacketProcessor _packetProcessor = packetProcessor;
    private IHost _superSocketHost;

    public async Task StartAsync()
    {
        if (_superSocketHost is null)
        {
            CreateSuperSocket();

            await _superSocketHost.StartAsync();
        }
    }

    public async Task StopAsync()
    {
        if (_superSocketHost is not null)
        {
            await _superSocketHost.StopAsync();

            _superSocketHost.Dispose();

            _superSocketHost = null;
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
                }
            )
            .UseSessionFactory<SessionContextFactory>()
            .UsePipelineFilterFactory<PipelineFilterFactory>()
            .UsePackageHandler(
                async (session, packet) =>
                {
                    if (packet is null)
                        return;

                    var ctx = (ISessionContext)session;

                    await _packetProcessor.ProcessPacket(ctx, packet, CancellationToken.None);
                }
            );

        _superSocketHost = host.Build();
    }
}
