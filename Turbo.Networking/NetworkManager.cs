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

    // One of each for both hosts. They are stateless apart from the encoder's payload cache,
    // which is shared on purpose so a broadcast reaching TCP and WebSocket players is still
    // serialized once. The WebSocket receive loop uses these fields directly instead of
    // resolving them from the host for every message.
    private readonly ClientPacketDecoder _packetDecoder = new();
    private readonly PackageHandler _packageHandler = new(
        revisionManager,
        messageSystem,
        loggerFactory.CreateLogger<PackageHandler>()
    );
    private readonly PackageEncoder _packageEncoder = new(
        revisionManager,
        new ComposerPayloadCache(),
        loggerFactory.CreateLogger<PackageEncoder>()
    );

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

                if (
                    session is not ISessionContext ctx
                    || ctx.WsBuffer is not { } buffer
                    || package.OpCode != OpCode.Binary
                )
                    return;

                foreach (var segment in package.Data)
                    buffer.Write(segment.Span);

                var memory = buffer.WrittenMemory;
                var consumed = 0;

                // Packets are decoded one at a time and each is handled before the next is read:
                // the handshake's handler switches decryption on, and a packet behind it in the
                // same frame must be decoded with the new key. The reader is rebuilt from the
                // offset after each await (it cannot live across one), and whatever is left is
                // moved to the front once, instead of recopying the tail after every packet.
                try
                {
                    while (consumed < memory.Length)
                    {
                        var packet = TryReadPacket(memory[consumed..], ctx, out var read);

                        if (packet is null)
                            break;

                        consumed += read;

                        await _packageHandler
                            .Handle(session, packet, CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                }
                finally
                {
                    // Even on a failure, so packets already handled are not handled again with
                    // the next frame.
                    CompactReceiveBuffer(buffer, memory, consumed);
                }
            }
        );
        builder.UseSession<WebSocketSessionContext>();
        builder.UseSessionGateway();

        _wsHost = builder.Build();
    }

    // A SequenceReader is a ref struct and cannot sit in the async receive loop, so the decode
    // step lives here and reports how far it read.
    private IClientPacket? TryReadPacket(
        ReadOnlyMemory<byte> unread,
        ISessionContext ctx,
        out int consumed
    )
    {
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(unread));
        var packet = _packetDecoder.TryRead(ref reader, ctx);

        consumed = (int)reader.Consumed;

        return packet;
    }

    // Moves the unread tail of a WebSocket receive buffer to its front. ResetWrittenCount keeps
    // the backing array (Clear would zero it first), and the copy handles the overlap.
    private static void CompactReceiveBuffer(
        ArrayBufferWriter<byte> buffer,
        ReadOnlyMemory<byte> written,
        int consumed
    )
    {
        if (consumed == 0)
            return;

        var remaining = written[consumed..];

        buffer.ResetWrittenCount();

        if (remaining.Length == 0)
            return;

        remaining.Span.CopyTo(buffer.GetSpan(remaining.Length));
        buffer.Advance(remaining.Length);
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
