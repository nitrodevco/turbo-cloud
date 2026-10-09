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
using SuperSocket.Server.Host;
using SuperSocket.WebSocket.Server;
using Turbo.Networking.Extensions;
using Turbo.Networking.Package;
using Turbo.Networking.Session;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Networking.Revisions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>
/// The heartbeat's two ends on a real WebSocket server: a client is heard from when its packets
/// arrive, not when the packets queued before them finish, and a connection the server closes
/// goes away, and its player with it, even when the client never answers the close.
/// </summary>
public sealed class SessionLivenessTests : IAsyncDisposable
{
    private readonly SessionHarness _hotel = new();
    private readonly TaskCompletionSource<ISessionContext> _connected = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly TaskCompletionSource _slowHandlerStarted = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly TaskCompletionSource _releaseSlowHandler = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly CapturingLogger<SessionLivenessTests> _closeLog = new();
    private readonly ClientWebSocket _client = new();
    private readonly IHost _host;
    private readonly int _port = FreePort();

    public SessionLivenessTests()
    {
        var fakes = new Fakes();
        var builder = WebSocketHostBuilder.Create();

        builder.ConfigureSuperSocket(options =>
        {
            options.Name = "SessionLivenessTests";
            options.AddListener(new ListenOptions { Ip = "127.0.0.1", Port = _port });
        });
        builder.ConfigureServices(
            (_, services) =>
            {
                services.AddSingleton<ISessionGateway>(_hotel.Gateway);
                services.AddSingleton(fakes.Create<IRevisionManager>());
                services.AddSingleton(
                    new PackageEncoder(
                        fakes.Create<IRevisionManager>(),
                        fakes.Create<IExtensionPacketRegistry>(),
                        new ComposerPayloadCache(),
                        NullLogger<PackageEncoder>.Instance
                    )
                );
            }
        );
        builder.ConfigureLogging(
            (_, logging) => logging.AddProvider(new CloseLogProvider(_closeLog))
        );
        builder.UseReceiveMarking();
        // The first message stands for a handler stuck on a slow grain call.
        builder.UseWebSocketMessageHandler(
            async (session, _) =>
            {
                if (_connected.TrySetResult((ISessionContext)session))
                {
                    _slowHandlerStarted.SetResult();
                    await _releaseSlowHandler.Task;
                }
            }
        );
        builder.UseSession<WebSocketSessionContext>();
        builder.UseSessionGateway();

        _host = builder.Build();
    }

    public async ValueTask DisposeAsync()
    {
        _releaseSlowHandler.TrySetResult();
        _client.Dispose();
        await _host.StopAsync(TimeSpan.FromSeconds(5));
        _host.Dispose();
    }

    private async Task<ISessionContext> ConnectAsync()
    {
        await _host.StartAsync();
        await _client.ConnectAsync(new Uri($"ws://127.0.0.1:{_port}/"), CancellationToken.None);
        await SendAsync();

        return await _connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private Task SendAsync() =>
        _client.SendAsync(
            new ArraySegment<byte>([0, 0, 0, 2, 0, 1]),
            WebSocketMessageType.Binary,
            true,
            CancellationToken.None
        );

    [Fact]
    public async Task APacketArrivingWhileAHandlerIsSlow_CountsAsHeardFrom()
    {
        var session = await ConnectAsync();
        await _slowHandlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(100);
        var pongSent = DateTime.UtcNow;
        await SendAsync();

        // The handler before it is still running; the heartbeat must still see this one.
        var heard = await WaitUntilAsync(
            () => session.LastReceivedUtc >= pongSent,
            TimeSpan.FromSeconds(2)
        );

        heard
            .Should()
            .BeTrue("a Pong queued behind a slow handler still proves the client is there");
        _releaseSlowHandler.SetResult();
    }

    [Fact]
    public async Task AClientThatNeverAnswersTheClose_IsDropped_AndItsPlayerLeaves()
    {
        var session = await ConnectAsync();
        _releaseSlowHandler.SetResult();
        await _hotel.Gateway.AddSessionToPlayerAsync(session.SessionKey, SessionHarness.PlayerId);
        _hotel.Gateway.GetOnlinePlayerIds().Should().NotBeEmpty();

        // The client never reads, so it never answers the close frame. The heartbeat closes a
        // silent session again on each of its ticks.
        await session.CloseSessionAsync("heartbeat timeout");
        var handshakeStarted = ((WebSocketSession)session).CloseHandshakeStartTime;
        for (var i = 0; i < 3; i++)
        {
            await Task.Delay(200);
            await session.CloseSessionAsync();
        }

        ((WebSocketSession)session)
            .CloseHandshakeStartTime.Should()
            .Be(handshakeStarted, "one close frame is sent, not one per heartbeat tick");

        var gone = await WaitUntilAsync(
            () => session.Connection.IsClosed && !_hotel.Gateway.GetOnlinePlayerIds().Any(),
            TimeSpan.FromSeconds(10)
        );

        gone.Should().BeTrue("the connection is dropped and the session removed with its player");
        _closeLog
            .AtLeast(LogLevel.Information)
            .Select(e => e.Message)
            .Should()
            .Contain(m =>
                m.Contains("of player 1 closed: LocalClosing, server reason: heartbeat timeout")
            );
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

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    // The close log is written under one category; everything else is dropped.
    private sealed class CloseLogProvider(ILogger log) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) =>
            categoryName == "Turbo.Networking.Session" ? log : NullLogger.Instance;

        public void Dispose() { }
    }
}
