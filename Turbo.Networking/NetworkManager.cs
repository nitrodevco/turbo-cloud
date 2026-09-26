using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using SuperSocket.ProtoBase;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Abstractions.Host;
using SuperSocket.Server.Host;
using SuperSocket.WebSocket;
using SuperSocket.WebSocket.Server;
using Turbo.Messages;
using Turbo.Networking.Configuration;
using Turbo.Networking.Extensions;
using Turbo.Networking.Package;
using Turbo.Networking.Session;
using Turbo.Networking.Tcp;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Revisions;
using Turbo.Primitives.Packets;

namespace Turbo.Networking;

public sealed class NetworkManager(
    IOptions<NetworkingConfig> config,
    ISessionGateway sessionGateway,
    IRevisionManager revisionManager,
    MessageSystem messageSystem,
    ILoggerFactory loggerFactory,
    IGrainFactory grainFactory
) : INetworkManager
{
    private readonly NetworkingConfig _config = config.Value;
    private readonly ISessionGateway _sessionGateway = sessionGateway;
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly MessageSystem _messageSystem = messageSystem;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IGrainFactory _grainFactory = grainFactory;

    private readonly object _tcpGate = new();
    private readonly object _wsGate = new();

    private IHost? _tcpHost;
    private IHost? _wsHost;

    public async Task StartAsync(CancellationToken ct)
    {
        bool needTcpStart = false;
        bool needsWsStart = false;

        lock (_tcpGate)
        {
            if (_tcpHost is null)
            {
                CreateTcpSocket();
                needTcpStart = true;
            }
        }

        lock (_wsGate)
        {
            if (_wsHost is null)
            {
                CreateWsSocket();
                needsWsStart = true;
            }
        }

        if (needTcpStart && _tcpHost is not null)
            await _tcpHost.StartAsync(ct).ConfigureAwait(false);

        if (needsWsStart && _wsHost is not null)
            await _wsHost.StartAsync(ct).ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        IHost? tcpHost;
        IHost? wsHost;

        lock (_tcpGate)
        {
            tcpHost = _tcpHost;
            _tcpHost = null;
        }

        lock (_wsGate)
        {
            wsHost = _wsHost;
            _wsHost = null;
        }

        if (tcpHost is not null)
            await tcpHost.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        if (wsHost is not null)
            await wsHost.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
    }

    private void CreateTcpSocket()
    {
        var builder = SuperSocketHostBuilder.Create<IClientPacket>();

        ConfigureCommon(builder, "TcpServer");
        builder.UseSession<TcpSessionContext>();
        builder.UsePipelineFilter<TcpFilter>();
        builder.UseSessionGateway();
        //builder.UsePingPong();

        _tcpHost = builder.Build();
    }

    private void CreateWsSocket()
    {
        var builder = WebSocketHostBuilder.Create();

        ConfigureCommon(builder, "WebSocketServer");
        builder.UseWebSocketMessageHandler(
            async (session, package) =>
            {
                ArgumentNullException.ThrowIfNull(package);

                if (session is not ISessionContext ctx || package.OpCode != OpCode.Binary)
                    return;

                var sp = session.Server?.ServiceProvider;
                var decoder = sp?.GetService<IClientPacketDecoder>();
                var handler = sp?.GetService<IPackageHandler<IClientPacket>>();

                if (decoder is null || handler is null)
                    return;

                foreach (var segment in package.Data)
                    ctx.WsBuffer?.Write(segment.Span);

                while (true)
                {
                    if (ctx.WsBuffer is null)
                        break;

                    var memory = ctx.WsBuffer.WrittenMemory;

                    if (memory.Length == 0)
                        break;

                    var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(memory));

                    var packet = decoder.TryRead(ref reader, ctx);

                    if (packet is null)
                        break;

                    var remaining = memory.Span[(int)reader.Consumed..].ToArray();

                    ctx.WsBuffer?.Clear();
                    ctx.WsBuffer?.Write(remaining);

                    await handler
                        .Handle(session, packet, CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
        );
        builder.UseSession<WebSocketSessionContext>();
        builder.UseSessionGateway();

        _wsHost = builder.Build();
    }

    // Both hosts read their own server section, log through the application's logger factory
    // (SuperSocket's own providers are cleared) and share the singletons that route packets.
    private void ConfigureCommon<TPackage>(
        ISuperSocketHostBuilder<TPackage> builder,
        string serverSection
    )
    {
        builder.ConfigureServerOptions((ctx, config) => config.GetSection(serverSection));
        builder.ConfigureLogging((ctx, logging) => logging.ClearProviders());
        builder.ConfigureServices((ctx, services) => ConfigureCommonServices(services));
    }

    private void ConfigureCommonServices(IServiceCollection services)
    {
        services.AddSingleton(_sessionGateway);
        services.AddSingleton(_revisionManager);
        services.AddSingleton(_messageSystem);
        services.AddSingleton(_loggerFactory);
        services.AddSingleton(_grainFactory);
        services.AddSingleton<IPackageHandler<IClientPacket>, PackageHandler>();
        services.AddSingleton<IClientPacketDecoder, ClientPacketDecoder>();
        services.AddSingleton<IPackageEncoder<OutgoingPackage>, PackageEncoder>();
    }
}
