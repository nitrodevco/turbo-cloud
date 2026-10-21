using System;
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
using SuperSocket.WebSocket.Server;
using Turbo.Messages;
using Turbo.Networking.Configuration;
using Turbo.Networking.Extensions;
using Turbo.Networking.Package;
using Turbo.Networking.Session;
using Turbo.Networking.Tcp;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Networking.Revisions;
using Turbo.Primitives.Packets;

namespace Turbo.Networking;

public sealed class NetworkManager(
    IOptions<NetworkingConfig> config,
    ISessionGateway sessionGateway,
    IRevisionManager revisionManager,
    IExtensionPacketRegistry extensions,
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

    // One of each for both hosts. They are stateless apart from the encoder's payload cache,
    // which is shared on purpose so a broadcast reaching TCP and WebSocket players is still
    // serialized once. The WebSocket receiver is handed these directly instead of resolving them
    // from the host for every message.
    private readonly ClientPacketDecoder _packetDecoder = new(
        config.Value,
        loggerFactory.CreateLogger<ClientPacketDecoder>()
    );
    private readonly PackageHandler _packageHandler = new(
        revisionManager,
        extensions,
        messageSystem,
        loggerFactory.CreateLogger<PackageHandler>()
    );
    private readonly PackageEncoder _packageEncoder = new(
        revisionManager,
        extensions,
        new ComposerPayloadCache(),
        loggerFactory.CreateLogger<PackageEncoder>()
    );

    private readonly object _tcpGate = new();
    private readonly object _wsGate = new();

    private IHost? _tcpHost;
    private IHost? _wsHost;

    private readonly SessionHeartbeat _heartbeat = new(
        config.Value,
        sessionGateway,
        loggerFactory.CreateLogger<SessionHeartbeat>()
    );

    public async Task StartAsync(CancellationToken ct)
    {
        bool needTcpStart = false;
        bool needsWsStart = false;

        lock (_tcpGate)
        {
            if (_config.TcpEnabled && _tcpHost is null)
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

        _heartbeat.Start();
    }

    public async Task StopAsync()
    {
        IHost? tcpHost;
        IHost? wsHost;

        // Before the hosts: a heartbeat closing sessions while they shut down would race them.
        await _heartbeat.DisposeAsync().ConfigureAwait(false);

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
        builder.UseClosingConnections();

        _tcpHost = builder.Build();
    }

    private void CreateWsSocket()
    {
        var builder = WebSocketHostBuilder.Create();

        ConfigureCommon(builder, "WebSocketServer");
        builder.UseReceiveMarking();
        builder.UseWebSocketMessageHandler(
            new WebSocketPacketReceiver(_packetDecoder, _packageHandler).ReceiveAsync
        );
        builder.UseSession<WebSocketSessionContext>();
        builder.UseSessionGateway();
        builder.UseClosingConnections();

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
        services.AddSingleton<IPackageHandler<IClientPacket>>(_packageHandler);
        services.AddSingleton<IClientPacketDecoder>(_packetDecoder);
        services.AddSingleton(_packageEncoder);
        services.AddSingleton<IPackageEncoder<OutgoingPackage>>(_packageEncoder);
    }
}
