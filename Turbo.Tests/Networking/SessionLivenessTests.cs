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
using Turbo.Networking.Configuration;
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
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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
    private IHost? _host;
    private int _port;

    private IHost BuildHost(int port)
    {
        var fakes = new Fakes();
        var builder = WebSocketHostBuilder.Create();

        builder.ConfigureSuperSocket(options =>
        {
            options.Name = "SessionLivenessTests";
            options.AddListener(new ListenOptions { Ip = "127.0.0.1", Port = port });
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
#pragma warning disable VSTHRD003 // The handler is held open until the test releases it.
                    await _releaseSlowHandler.Task;
#pragma warning restore VSTHRD003
                }
            }
        );
        builder.UseSession<WebSocketSessionContext>();
        builder.UseSessionGateway();

        return builder.Build();
    }

    public async ValueTask DisposeAsync()
    {
        _releaseSlowHandler.TrySetResult();
        _client.Dispose();

        if (_host is null)
            return;

        await _host.StopAsync(TimeSpan.FromSeconds(5));
        _host.Dispose();
    }

    private async Task<ISessionContext> ConnectAsync()
    {
        await StartAsync();
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
        await _slowHandlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        await Task.Delay(100, Ct);
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
            await Task.Delay(200, Ct);
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

    [Fact]
    public async Task AConnectionStuckBehindAHandler_IsTakenOffline_ByTheHeartbeat()
    {
        var session = await ConnectAsync();
        await _slowHandlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        await _hotel.Gateway.AddSessionToPlayerAsync(session.SessionKey, SessionHarness.PlayerId);

        // The client goes on sending behind the stuck handler until the server stops reading it,
        // then goes away. SuperSocket cannot finish closing such a connection, by the client or
        // by the server, until the handler returns.
        using (var flooding = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
        {
            try
            {
                for (var i = 0; i < 200; i++)
                    await _client.SendAsync(
                        new ArraySegment<byte>(new byte[1000]),
                        WebSocketMessageType.Binary,
                        true,
                        flooding.Token
                    );
            }
            catch (OperationCanceledException) { }
        }
        _client.Abort();

        var heartbeat = new SessionHeartbeat(
            new NetworkingConfig
            {
                PingIntervalMilliseconds = 200,
                SessionTimeoutMilliseconds = 500,
            },
            _hotel.Gateway,
            NullLogger<SessionHeartbeat>.Instance
        );
        heartbeat.Start();

        try
        {
            var gone = await WaitUntilAsync(
                () =>
                    _hotel.Gateway.GetSession(session.SessionKey) is null
                    && !_hotel.Gateway.GetOnlinePlayerIds().Any(),
                TimeSpan.FromSeconds(5)
            );

            gone.Should()
                .BeTrue("a session the server closed is let go, with its player, if it stays open");
            SessionAsserts.PresenceObserver(_hotel.Presence).Should().BeNull();
            // Either of the heartbeat's checks may close it first: nothing received within the
            // timeout, or a Ping it cannot send behind the stuck handler. Which one wins is the
            // runner's timing (windows-latest CI, 2026-10-18: the send stalled first).
            session.ServerCloseReason.Should().MatchRegex("^heartbeat (timeout|send stalled)");
        }
        finally
        {
            await heartbeat.DisposeAsync();
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

    // A port found free can be taken again before the server binds it, by a test or a client
    // connection running beside this one, and then the server fails to start: so it starts on
    // another (1 in 200 runs failed so under load).
    private async Task StartAsync()
    {
        for (var attempt = 1; ; attempt++)
        {
            _port = FreePort();
            var host = BuildHost(_port);

            try
            {
                await host.StartAsync();
                _host = host;

                return;
            }
            catch (Exception) when (attempt < 5)
            {
                host.Dispose();
            }
        }
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
