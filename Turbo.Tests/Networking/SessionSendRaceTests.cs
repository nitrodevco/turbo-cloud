using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Turbo.Networking.Session;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Networking;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>
/// A client that leaves while a Ping is being written: SuperSocket completes the connection's
/// writer before it marks the connection closed, so the write throws. That is the client leaving,
/// not a fault, and must not print a stack trace for every send that follows.
/// </summary>
public class SessionSendRaceTests
{
    private static readonly Type StateType = typeof(SessionGateway).Assembly.GetType(
        "Turbo.Networking.Session.SessionContextState",
        throwOnError: true
    )!;

    private readonly CapturingLogger<ISessionContext> _logger = new();
    private readonly object _state;
    private readonly ISessionContext _session = new SessionHarness().NewSession(1);
    private int _writes;

    public SessionSendRaceTests()
    {
        _state = Activator.CreateInstance(StateType, _logger)!;
    }

    private Task SendAsync()
    {
        Func<int, CancellationToken, ValueTask> send = (_, _) =>
        {
            _writes++;
            throw new InvalidOperationException(
                "Writing is not allowed after writer was completed."
            );
        };

        var method = StateType
            .GetMethod("SendAsync", BindingFlags.Public | BindingFlags.Instance)!
            .MakeGenericMethod(typeof(int));

        return (Task)
            method.Invoke(
                _state,
                [_session, 0, send, new PingMessage(), 1, CancellationToken.None]
            )!;
    }

    [Fact]
    public async Task SendAfterTheWriterCompleted_IsDroppedOnce_WithoutAStackTrace()
    {
        await SendAsync();
        await SendAsync();
        await SendAsync();

        _writes.Should().Be(1, "the connection is gone after the first failed write");
        _logger.AtLeast(LogLevel.Warning).Should().BeEmpty();

        var dropped = _logger.Entries.Should().ContainSingle().Subject;
        dropped.Level.Should().Be(LogLevel.Debug);
        dropped.Exception.Should().BeNull();
        dropped.Message.Should().Contain(nameof(PingMessage));
    }

    // The heartbeat closing a silent client as it drops: the WebSocket close frame meets the
    // completed writer.
    [Fact]
    public async Task CloseAfterTheWriterCompleted_DoesNotThrow_OrLogAStackTrace()
    {
        Func<ValueTask> close = () =>
            throw new InvalidOperationException(
                "Writing is not allowed after writer was completed."
            );

        var closing = (Task)
            StateType
                .GetMethod("CloseAsync", BindingFlags.Public | BindingFlags.Instance)!
                .Invoke(_state, [_session, close])!;

        await closing.Invoking(t => t).Should().NotThrowAsync();
        _logger.AtLeast(LogLevel.Warning).Should().BeEmpty();
        _logger.Entries.Should().ContainSingle().Which.Exception.Should().BeNull();
    }
}
