using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SuperSocket.Server.Host;
using Turbo.Core.Configuration;
using Turbo.Core.Networking;
using Turbo.Core.Networking.Encryption;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;
using Turbo.Networking.Encryption;
using Turbo.Networking.Pipeline;
using Turbo.Networking.Session;

namespace Turbo.Networking;

public class NetworkManager(IEmulatorConfig config, IPacketProcessor packetProcessor)
    : INetworkManager
{
    private readonly IEmulatorConfig _config = config;
    private readonly IPacketProcessor _packetProcessor = packetProcessor;
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

                    await ctx.EnqueuePacketAsync(packet);
                }
            );

        _superSocketHost = host.Build();
    }
}
