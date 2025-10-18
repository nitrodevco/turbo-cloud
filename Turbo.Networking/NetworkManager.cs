using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperSocket.ProtoBase;
using SuperSocket.Server.Host;
using Turbo.Messages;
using Turbo.Networking.Abstractions;
using Turbo.Networking.Abstractions.Revisions;
using Turbo.Networking.Abstractions.Session;
using Turbo.Networking.Configuration;
using Turbo.Networking.Decoder;
using Turbo.Networking.Encoder;
using Turbo.Networking.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking;

public sealed class NetworkManager(
    IOptions<NetworkingConfig> config,
    IRevisionManager revisionManager,
    MessageSystem messageSystem,
    ILogger<NetworkManager> logger
) : INetworkManager
{
    private readonly object _hostGate = new();
    private readonly NetworkingConfig _config = config.Value;
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly MessageSystem _messageSystem = messageSystem;
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
                    services.AddSingleton(_revisionManager);
                    services.AddSingleton<PackageEncoder>();
                }
            )
            .UseSession<SessionContext>()
            .UsePipelineFilter<PackageDecoder>()
            .UsePackageHandler(
                async (session, packet) =>
                {
                    if (packet is null)
                        return;

                    var ctx = (ISessionContext)session;

                    try
                    {
                        var revision =
                            _revisionManager.GetRevision(ctx.RevisionId)
                            ?? throw new ArgumentNullException("No revision set");

                        if (revision.Parsers.TryGetValue(packet.Header, out var parser))
                        {
                            var message = parser.Parse(packet);

                            _logger.LogInformation(
                                "Processing {Header} {PacketType} for {SessionId}",
                                packet.Header,
                                message.GetType().Name,
                                ctx.SessionID
                            );

                            _ = _messageSystem
                                .PublishAsync(message, ctx, CancellationToken.None)
                                .ConfigureAwait(false);

                            return;
                        }

                        _logger.LogInformation(
                            "Parser not found with header {Header} for {SessionId}",
                            packet.Header,
                            ctx.SessionID
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to process packet {Packet} for session {SessionId}",
                            packet.Header,
                            session.SessionID
                        );
                    }
                }
            );

        _superSocketHost = host.Build();
    }
}
