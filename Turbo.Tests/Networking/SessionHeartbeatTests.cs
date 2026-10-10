using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Networking.Configuration;
using Turbo.Networking.Session;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>
/// A client that keeps sending but stops reading fills its socket, and every send to it waits.
/// The heartbeat must still ping every other connection, and must close the one whose Ping
/// cannot go out, rather than wait on it forever.
/// </summary>
public sealed class SessionHeartbeatTests
{
    private readonly Fakes _fakes = new();
    private readonly TaskCompletionSource _livePinged = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly TaskCompletionSource _stuckClosed = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    [Fact]
    public async Task AConnectionWhoseSendsStall_NeitherHoldsUpTheOthers_NorStaysOpen()
    {
        // The stuck one is listed first, so a heartbeat that waits on it never reaches the other.
        var stuck = _fakes.Create<ISessionContext>("stuck");
        var live = _fakes.Create<ISessionContext>("live");
        var gateway = _fakes.Create<ISessionGateway>();

        _fakes.Handlers["get_LastReceivedUtc"] = _ => DateTime.UtcNow;
        _fakes.Handlers["GetSessions"] = _ => new List<ISessionContext> { stuck, live };
        _fakes.Handlers["GetPlayerId"] = _ => (PlayerId)1;
        _fakes.Handlers["SendComposerAsync"] = call =>
        {
            if (call.Key is "live" && call.Args[0] is PingMessage)
            {
                _livePinged.TrySetResult();

                return Task.CompletedTask;
            }

            // A send to a socket that is not being read: it waits until it is given up on.
            return Task.Delay(Timeout.Infinite, (CancellationToken)call.Args[1]!);
        };
        _fakes.Handlers["CloseSessionAsync"] = call =>
        {
            if (call.Key is "stuck")
                _stuckClosed.TrySetResult();

            return Task.CompletedTask;
        };

        var heartbeat = new SessionHeartbeat(
            new NetworkingConfig
            {
                PingIntervalMilliseconds = 50,
                SessionTimeoutMilliseconds = 60000,
            },
            gateway,
            NullLogger<SessionHeartbeat>.Instance
        );

        heartbeat.Start();

        try
        {
            await _livePinged.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await _stuckClosed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await heartbeat.DisposeAsync();
        }

        _fakes
            .Log.Calls.Where(c => c.Key is "live" && c.Method == "CloseSessionAsync")
            .Should()
            .BeEmpty();
    }
}
