using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Abstractions.Host;
using SuperSocket.Server.Abstractions.Session;
using SuperSocket.Server.Host;
using SuperSocket.WebSocket.Server;
using Turbo.Networking.Configuration;
using Turbo.Networking.Extensions;
using Turbo.Networking.Package;
using Turbo.Networking.Session;
using Turbo.Networking.Tcp;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Networking.Revisions;
using Turbo.Primitives.Packets;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>
/// The 4-byte length in front of each packet is the client's word, read before it has logged in.
/// A length no packet can have (below a header, negative, past
/// <see cref="NetworkingConfig.MaxIncomingPacketBytes"/>, or <c>int.MaxValue</c>, which overflowed
/// the length check) closes the connection with its reason instead of throwing out of the
/// decoder or keeping the connection buffering for a packet that never ends; a WebSocket
/// connection never holds more than one largest packet unread, and lets go of a buffer that
/// grew for a large packet once that packet has been read. Both servers are the real ones,
/// with the filter, message handler and connections the hotel registers.
/// </summary>
public sealed class IncomingPacketLengthTests : IAsyncDisposable
{
    private const int MAX_PACKET_BYTES = 256 * 1024;
    private const short HEADER = 4000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly CapturingLogger<ClientPacketDecoder> _decoderLog = new();
    private readonly CapturingLogger<IncomingPacketLengthTests> _serverLog = new();
    private readonly RecordingPackageHandler _handled = new();
    private readonly TaskCompletionSource<ISessionContext> _connected = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly ClientWebSocket _webSocket = new();
    private readonly TcpClient _tcp = new();
    private IHost? _host;

    public static TheoryData<int> ImpossibleLengths =>
        [int.MaxValue, MAX_PACKET_BYTES + 1, 1, 0, -1, int.MinValue];

    [Theory]
    [MemberData(nameof(ImpossibleLengths))]
    public async Task ATcpClientDeclaringALengthNoPacketHas_IsClosedWithItsReason(int length)
    {
        var session = await ConnectTcpAsync();
        var stream = _tcp.GetStream();

        await stream.WriteAsync(Packet(length, 16), Ct);

        (await ReadUntilClosedAsync(stream)).Should().BeTrue("the server closes the connection");
        await AssertRefusedAsync(session, $"declared a packet of {length} bytes");
    }

    [Theory]
    [MemberData(nameof(ImpossibleLengths))]
    public async Task AWebSocketClientDeclaringALengthNoPacketHas_IsClosedWithItsReason(int length)
    {
        var session = await ConnectWebSocketAsync();

        await SendFrameAsync(Packet(length, 16));

        await AssertRefusedAsync(session, $"declared a packet of {length} bytes");
        // The decoder refuses (and sets the close reason) before the receiver lets go of the
        // buffer, so under load the buffer can still be there the instant the reason is seen.
        (await WaitUntilAsync(() => session.ReceiveBuffer is null, TimeSpan.FromSeconds(5)))
            .Should()
            .BeTrue("a refused connection keeps nothing it sent");
    }

    [Fact]
    public async Task AWebSocketClientSendingMoreThanOnePacketCanHold_IsRefusedBeforeItIsBuffered()
    {
        var session = await ConnectWebSocketAsync();
        // A largest packet's start, then more than the rest of it in the next frame: whatever
        // the length says, no client needs this much held unread.
        var start = Packet(MAX_PACKET_BYTES, 1024);
        await SendFrameAsync(start);

        await SendFrameAsync(new byte[MAX_PACKET_BYTES * 2]);

        await AssertRefusedAsync(session, "bytes of unread packet data");
        session.ReceiveBuffer.Should().BeNull("the refused frame is never copied");
    }

    /// <summary>
    /// The session reads the forwarded address from the WebSocket upgrade a proxy on the same
    /// machine sent (<see cref="ClientAddresses"/> has the rule): what the login's policies get.
    /// </summary>
    [Fact]
    public async Task A_WebSocket_session_behind_a_local_proxy_has_the_forwarded_address()
    {
        _webSocket.Options.SetRequestHeader(ClientAddresses.FORWARDED_FOR, "198.51.100.4");

        var session = await ConnectWebSocketAsync();

        session.RemoteAddress.Should().Be("198.51.100.4");
    }

    [Fact]
    public async Task ALargeButLegalPacketSplitAcrossFrames_IsHandled_AndTheBufferShrinksBack()
    {
        var session = await ConnectWebSocketAsync();
        var packet = Packet(200 * 1024, 200 * 1024 - 2);

        await SendFrameAsync(packet.AsMemory(0, 100 * 1024));
        await SendFrameAsync(packet.AsMemory(100 * 1024));
        await SendFrameAsync(Packet(6, 4));

        (await WaitUntilAsync(() => _handled.Packets.Count == 2, TimeSpan.FromSeconds(5)))
            .Should()
            .BeTrue("both packets are handled");
        _handled.Packets.Select(p => p.Header).Should().Equal(HEADER, HEADER);
        (
            await WaitUntilAsync(
                () => session.ReceiveBuffer?.Capacity <= 4096,
                TimeSpan.FromSeconds(5)
            )
        )
            .Should()
            .BeTrue("the buffer that grew for the large packet is let go once it is read");
        session.ServerCloseReason.Should().BeNull();
        _decoderLog.AtLeast(LogLevel.Warning).Should().BeEmpty();
    }

    private async Task AssertRefusedAsync(ISessionContext session, string reason)
    {
        (await WaitUntilAsync(() => session.ServerCloseReason is not null, TimeSpan.FromSeconds(5)))
            .Should()
            .BeTrue("the server closes the session itself");
        session.ServerCloseReason.Should().Contain(reason);
        _handled.Packets.Should().BeEmpty("nothing it sent reaches a handler");
        _decoderLog
            .AtLeast(LogLevel.Warning)
            .Should()
            .ContainSingle("the refusal is logged once")
            .Which.Message.Should()
            .Contain(reason);
        _serverLog
            .AtLeast(LogLevel.Error)
            .Should()
            .BeEmpty("nothing is thrown out of the decoder or the message handler");
    }

    // The length as the client wrote it, then a header and the body bytes that were sent.
    private static byte[] Packet(int declaredLength, int bodyBytes)
    {
        var bytes = new byte[4 + 2 + bodyBytes];
        BinaryPrimitives.WriteInt32BigEndian(bytes, declaredLength);
        BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(4), HEADER);

        return bytes;
    }

    private Task SendFrameAsync(ReadOnlyMemory<byte> data) =>
        _webSocket.SendAsync(data, WebSocketMessageType.Binary, true, Ct).AsTask();

    private static async Task<bool> ReadUntilClosedAsync(NetworkStream stream)
    {
        var buffer = new byte[256];
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            while (await stream.ReadAsync(buffer, timeout.Token) > 0) { }

            return true;
        }
        catch (IOException)
        {
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private ClientPacketDecoder NewDecoder() =>
        new(new NetworkingConfig { MaxIncomingPacketBytes = MAX_PACKET_BYTES }, _decoderLog);

    private async Task<ISessionContext> ConnectTcpAsync()
    {
        var port = await StartAsync(port =>
        {
            var builder = SuperSocketHostBuilder.Create<IClientPacket>();
            Configure(builder, port);
            builder.ConfigureServices(
                (_, services) =>
                {
                    services.AddSingleton<IClientPacketDecoder>(NewDecoder());
                    services.AddSingleton<IPackageHandler<IClientPacket>>(_handled);
                }
            );
            builder.UseSession<TcpSessionContext>();
            builder.UsePipelineFilter<TcpFilter>();

            return builder.Build();
        });

        await _tcp.ConnectAsync(IPAddress.Loopback, port, Ct);

        return await _connected.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }

    private async Task<WebSocketSessionContext> ConnectWebSocketAsync()
    {
        var port = await StartAsync(port =>
        {
            var builder = WebSocketHostBuilder.Create();
            Configure(builder, port);
            builder.UseWebSocketMessageHandler(
                new WebSocketPacketReceiver(NewDecoder(), _handled).ReceiveAsync
            );
            builder.UseSession<WebSocketSessionContext>();

            return builder.Build();
        });

        await _webSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), Ct);

        return (WebSocketSessionContext)
            await _connected.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }

    private void Configure<TPackage>(ISuperSocketHostBuilder<TPackage> builder, int port)
    {
        builder.ConfigureSuperSocket(options =>
        {
            options.Name = nameof(IncomingPacketLengthTests);
            options.AddListener(new ListenOptions { Ip = "127.0.0.1", Port = port });
        });
        builder.ConfigureServices(
            (_, services) =>
                services.AddSingleton(
                    new PackageEncoder(
                        new Fakes().Create<IRevisionManager>(),
                        new Fakes().Create<IExtensionPacketRegistry>(),
                        new ComposerPayloadCache(),
                        NullLogger<PackageEncoder>.Instance
                    )
                )
        );
        builder.ConfigureLogging(
            (_, logging) =>
            {
                logging.ClearProviders();
                logging.AddProvider(new EverythingTo(_serverLog));
            }
        );
        builder.UseClosingConnections();
        builder.UseSessionHandler(session =>
        {
            _connected.TrySetResult((ISessionContext)session);

            return ValueTask.CompletedTask;
        });
    }

    // A port found free can be taken before the server binds it, so it starts on another.
    private async Task<int> StartAsync(Func<int, IHost> build)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            var host = build(port);

            try
            {
                await host.StartAsync(Ct);
                _host = host;

                return port;
            }
            catch (Exception) when (attempt < 5)
            {
                host.Dispose();
            }
        }
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            if (condition())
                return true;
            await Task.Delay(25);
        }
        return condition();
    }

    public async ValueTask DisposeAsync()
    {
        _webSocket.Dispose();
        _tcp.Dispose();

        if (_host is null)
            return;

        await _host.StopAsync(TimeSpan.FromSeconds(5));
        _host.Dispose();
    }

    private sealed class RecordingPackageHandler : IPackageHandler<IClientPacket>
    {
        public ConcurrentQueue<IClientPacket> Packets { get; } = new();

        public ValueTask Handle(IAppSession session, IClientPacket package, CancellationToken ct)
        {
            Packets.Enqueue(package);

            return ValueTask.CompletedTask;
        }
    }

    private sealed class EverythingTo(ILogger log) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => log;

        public void Dispose() { }
    }
}
