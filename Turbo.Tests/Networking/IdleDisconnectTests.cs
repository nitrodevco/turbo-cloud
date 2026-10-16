using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SuperSocket.Server.Abstractions.Session;
using Turbo.Messages;
using Turbo.Messages.Registry;
using Turbo.Networking.Configuration;
using Turbo.Networking.Extensions;
using Turbo.Networking.Package;
using Turbo.Networking.Revisions;
using Turbo.Networking.Session;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;
using Turbo.Revisions.Revision20260909;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>
/// Habbo disconnects an idle player with DisconnectReason 112, "Idle disconnection!"
/// (DisconnectReasonEvent.as:87, 147; disconnected.idle). What the client sends on its own timers
/// (Pong, latency test, friend list and forum polls) shows the client is there, not the player.
/// </summary>
public sealed class IdleDisconnectTests
{
    private readonly Fakes _fakes = new();

    [Theory]
    [InlineData("PongMessageEvent", new byte[0])]
    [InlineData("LatencyPingRequestMessageEvent", new byte[] { 0, 0, 0, 7 })]
    [InlineData("FriendListUpdateMessageEvent", new byte[0])]
    [InlineData("GetUnreadForumsCountMessageEvent", new byte[0])]
    public async Task AClientTimerMessage_DoesNotCountAsThePlayerDoingSomething(
        string header,
        byte[] body
    )
    {
        await HandleAsync(PacketHarness.Incoming(header), body);

        _fakes.Log.Calls.Should().NotContain(c => c.Method == "MarkActive");
    }

    [Fact]
    public async Task AWalk_CountsAsThePlayerDoingSomething()
    {
        await HandleAsync(
            PacketHarness.Incoming("MoveAvatarMessageEvent"),
            [0, 0, 0, 3, 0, 0, 0, 4]
        );

        _fakes.Log.Calls.Should().Contain(c => c.Method == "MarkActive");
    }

    [Fact]
    public async Task APlayerIdleTooLong_IsToldIdle_AndDisconnected()
    {
        var sent = await HeartbeatAsync(
            idleDisconnectMs: 60_000,
            lastActive: DateTime.UtcNow.AddMinutes(-2)
        );

        sent.OfType<DisconnectReasonEventMessageComposer>()
            .Select(x => x.Reason)
            .Should()
            .Equal(112);
        _fakes.Log.Calls.Should().Contain(c => c.Method == "CloseSessionAsync");
    }

    [Fact]
    public async Task APlayerActiveWithinTheTimeout_IsOnlyPinged()
    {
        var sent = await HeartbeatAsync(
            idleDisconnectMs: 60_000,
            lastActive: DateTime.UtcNow.AddSeconds(-10)
        );

        sent.Should().AllBeOfType<PingMessage>();
        _fakes.Log.Calls.Should().NotContain(c => c.Method == "CloseSessionAsync");
    }

    [Fact]
    public async Task WithTheTimeoutOff_NobodyIsDisconnectedForBeingIdle()
    {
        var sent = await HeartbeatAsync(idleDisconnectMs: 0, lastActive: DateTime.MinValue);

        sent.Should().AllBeOfType<PingMessage>();
        _fakes.Log.Calls.Should().NotContain(c => c.Method == "CloseSessionAsync");
    }

    private async Task HandleAsync(int header, byte[] body)
    {
        var revisions = new RevisionManager(NullLogger<RevisionManager>.Instance);
        revisions.RegisterRevision(new Revision20260909());
        _fakes.Handlers["get_RevisionId"] = _ => revisions.DefaultRevisionId;
        var session = _fakes.Create<ISessionContext>("session");

        var handler = new PackageHandler(
            revisions,
            new ExtensionPacketRegistry(),
            new MessageSystem(
                new MessageRegistry(
                    new ServiceCollection().BuildServiceProvider(),
                    NullLogger<MessageRegistry>.Instance
                )
            ),
            NullLogger<PackageHandler>.Instance
        );

        await handler.Handle((IAppSession)session, new ClientPacket(header, body), default);
    }

    // Runs the heartbeat until it has pinged or closed the one session, and returns what it sent.
    private async Task<List<IComposer>> HeartbeatAsync(int idleDisconnectMs, DateTime lastActive)
    {
        var session = _fakes.Create<ISessionContext>("session");
        var gateway = _fakes.Create<ISessionGateway>();
        var sent = new List<IComposer>();
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _fakes.Handlers["get_LastReceivedUtc"] = _ => DateTime.UtcNow;
        _fakes.Handlers["get_LastActiveUtc"] = _ => lastActive;
        _fakes.Handlers["GetSessions"] = _ => new List<ISessionContext> { session };
        _fakes.Handlers["GetPlayerId"] = _ => (PlayerId)1;
        _fakes.Handlers["SendComposerAsync"] = call =>
        {
            lock (sent)
                sent.Add((IComposer)call.Args[0]!);

            if (call.Args[0] is PingMessage)
                first.TrySetResult();

            return Task.CompletedTask;
        };
        _fakes.Handlers["CloseSessionAsync"] = _ =>
        {
            first.TrySetResult();

            return Task.CompletedTask;
        };

        var heartbeat = new SessionHeartbeat(
            new NetworkingConfig
            {
                PingIntervalMilliseconds = 50,
                SessionTimeoutMilliseconds = 60_000,
                IdleDisconnectMilliseconds = idleDisconnectMs,
            },
            gateway,
            NullLogger<SessionHeartbeat>.Instance
        );

        heartbeat.Start();

        try
        {
            await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await heartbeat.DisposeAsync();
        }

        lock (sent)
            return [.. sent];
    }
}
