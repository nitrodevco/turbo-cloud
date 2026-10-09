using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Crypto;
using Turbo.Primitives.Crypto;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Networking.Session;

/// <summary>
/// The per-connection state and send path shared by <see cref="TcpSessionContext"/> and
/// <see cref="WebSocketSessionContext"/>. The two derive from different SuperSocket session base
/// classes, so they cannot share a base; each holds one of these and forwards to it, and only
/// the transport write itself differs between them.
/// </summary>
internal sealed class SessionContextState(ILogger<ISessionContext> logger)
{
    private readonly ILogger<ISessionContext> _logger = logger;
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    // Written by the session observer, read by every incoming packet on the receive loop.
    private int _activeRoomId = -1;

    // Written by the receive loop, read by the heartbeat on another thread.
    private long _lastReceivedTicks = DateTime.UtcNow.Ticks;

    // How long a client has to answer a WebSocket close frame before its connection is dropped.
    private static readonly TimeSpan CLOSE_ANSWER_GRACE = TimeSpan.FromSeconds(5);

    // 1 once the session has started closing; a session is closed once.
    private int _closing;

    // Set once a send finds the connection's writer completed. Only read and written under the
    // send semaphore.
    private bool _writerCompleted;

    public bool PolicyDone { get; set; } = true;
    public string RevisionId { get; set; } = "Default";
    public IRc4Engine? CryptoIn { get; private set; }
    public IRc4Engine? CryptoOut { get; private set; }

    public RoomId ActiveRoomId => Volatile.Read(ref _activeRoomId);

    public void SetActiveRoomId(RoomId roomId) => Volatile.Write(ref _activeRoomId, roomId.Value);

    public DateTime LastReceivedUtc =>
        new(Interlocked.Read(ref _lastReceivedTicks), DateTimeKind.Utc);

    public void MarkReceived() =>
        Interlocked.Exchange(ref _lastReceivedTicks, DateTime.UtcNow.Ticks);

    public void SetupEncryption(byte[] key, bool setCryptoOut = false)
    {
        CryptoIn = new Rc4Engine(key);

        if (setCryptoOut)
            CryptoOut = new Rc4Engine(key);
    }

    /// <summary>
    /// Serialises sends on one connection and runs <paramref name="send"/>, which writes
    /// <paramref name="count"/> composers starting with <paramref name="first"/> (named in the
    /// log if it fails). A send failure is logged and not rethrown, because callers broadcast to
    /// many sessions and one bad connection must not fail the rest. A connection that closed
    /// while the send was in flight is an ordinary race with the client leaving: it is logged once
    /// at debug, without the stack trace, and every later send to it is dropped quietly.
    /// </summary>
    /// <remarks>
    /// <paramref name="send"/> takes its state explicitly so the per-send callers can pass a
    /// static lambda instead of allocating a closure for every composer.
    /// </remarks>
    public async Task SendAsync<TState>(
        ISessionContext session,
        TState state,
        Func<TState, CancellationToken, ValueTask> send,
        IComposer first,
        int count,
        CancellationToken ct
    )
    {
        await _sendSemaphore.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (_writerCompleted || session.Connection.IsClosed)
                return;

            await send(state, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsClosedDuringSend(session, ex))
        {
            _writerCompleted = true;

            _logger.LogDebug(
                "Dropped {Count} composer(s) starting with {Composer} for session {SessionKey}: connection closed during send",
                count,
                first.GetType().Name,
                session.SessionKey
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send {Count} composer(s) starting with {Composer} to session {SessionKey}",
                count,
                first.GetType().Name,
                session.SessionKey
            );
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    /// <summary>
    /// Closes the connection once: runs <paramref name="close"/> (for a WebSocket, a close frame
    /// the client is meant to answer), and if the connection is still open a few seconds later,
    /// or the close itself failed, drops it with <paramref name="drop"/>. Later calls do nothing.
    /// </summary>
    /// <remarks>
    /// A WebSocket close is a handshake, and SuperSocket waits for the answer before it lets the
    /// connection go. The heartbeat used to call this every tick for a client that never answered,
    /// and each close frame restarted SuperSocket's 120 second wait, so the connection was never
    /// dropped, the session was never removed and its player stayed in their room. Every session
    /// queued behind it for the same check was stuck too. A close the client does not answer is
    /// now cut off after <see cref="CLOSE_ANSWER_GRACE"/>, which closes the session and removes
    /// the player like any other close.
    ///
    /// Not serialised with sends: the connections closed are often ones whose sends have stalled.
    /// </remarks>
    public async Task CloseAsync(
        ISessionContext session,
        Func<ValueTask> close,
        Func<ValueTask> drop
    )
    {
        if (Interlocked.Exchange(ref _closing, 1) != 0)
            return;

        var grace = CLOSE_ANSWER_GRACE;

        try
        {
            await close().ConfigureAwait(false);
        }
        catch (Exception ex) when (IsClosedDuringSend(session, ex))
        {
            // A WebSocket close writes a close frame, so closing a connection the client is
            // already dropping meets the same completed writer a send does.
            _logger.LogDebug(
                "Session {SessionKey} was already closing when it was closed",
                session.SessionKey
            );
            grace = TimeSpan.Zero;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Closing session {SessionKey} failed; dropping the connection",
                session.SessionKey
            );
            grace = TimeSpan.Zero;
        }

        if (!session.Connection.IsClosed)
            _ = DropUnansweredAsync(session, drop, grace);
    }

    private async Task DropUnansweredAsync(
        ISessionContext session,
        Func<ValueTask> drop,
        TimeSpan grace
    )
    {
        try
        {
            // A close that failed was logged where it failed; only an unanswered one is news.
            if (grace > TimeSpan.Zero)
            {
                await Task.Delay(grace).ConfigureAwait(false);

                if (session.Connection.IsClosed)
                    return;

                _logger.LogInformation(
                    "Session {SessionKey} did not answer its close within {Grace}; dropping the connection",
                    session.SessionKey,
                    grace
                );
            }

            await drop().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dropping session {SessionKey} failed", session.SessionKey);
        }
    }

    // SuperSocket completes the connection's pipe writer when it closes, and a write racing that
    // throws "Writing is not allowed after writer was completed" before IsClosed is set.
    private static bool IsClosedDuringSend(ISessionContext session, Exception ex) =>
        session.Connection.IsClosed
        || (
            ex is InvalidOperationException
            && ex.Message.Contains("Writing is not allowed", StringComparison.Ordinal)
        );
}
