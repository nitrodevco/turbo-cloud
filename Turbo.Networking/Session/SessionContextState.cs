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
    /// Runs <paramref name="close"/>. A WebSocket close writes a close frame, so closing a
    /// connection the client is already dropping meets the same completed writer a send does;
    /// the connection is going down either way, so that is logged at debug and not thrown.
    /// </summary>
    /// <remarks>
    /// Not serialised with sends: the connections closed are often ones whose sends have stalled.
    /// </remarks>
    public async Task CloseAsync(ISessionContext session, Func<ValueTask> close)
    {
        try
        {
            await close().ConfigureAwait(false);
        }
        catch (Exception ex) when (IsClosedDuringSend(session, ex))
        {
            _logger.LogDebug(
                "Session {SessionKey} was already closing when it was closed",
                session.SessionKey
            );
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
