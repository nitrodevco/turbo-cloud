using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Crypto;
using Turbo.Primitives.Crypto;
using Turbo.Primitives.Networking;
using Turbo.Runtime;

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

    public bool PolicyDone { get; set; } = true;
    public string RevisionId { get; set; } = "Default";
    public DateTime LastActivityUtc { get; private set; } = DateTime.UtcNow;
    public AsyncSignal PongWaiter { get; } = new();
    public CancellationTokenSource HeartbeatCts { get; } = new();
    public IRc4Engine? CryptoIn { get; private set; }
    public IRc4Engine? CryptoOut { get; private set; }

    public void Touch()
    {
        LastActivityUtc = DateTime.UtcNow;
    }

    public void SetupEncryption(byte[] key, bool setCryptoOut = false)
    {
        CryptoIn = new Rc4Engine(key);

        if (setCryptoOut)
            CryptoOut = new Rc4Engine(key);
    }

    /// <summary>
    /// Serialises sends on one connection and runs <paramref name="send"/>. A send failure is
    /// logged and not rethrown, because callers broadcast to many sessions and one bad connection
    /// must not fail the rest. A connection that closed while the send was in flight is an
    /// ordinary race with the client leaving, so it is logged at debug only.
    /// </summary>
    public async Task SendAsync(
        ISessionContext session,
        IComposer composer,
        Func<CancellationToken, ValueTask> send,
        CancellationToken ct
    )
    {
        await _sendSemaphore.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (session.Connection.IsClosed)
                return;

            await send(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsClosedDuringSend(session, ex))
        {
            _logger.LogDebug(
                ex,
                "Dropped {Composer} for session {SessionKey}: connection closed during send",
                composer.GetType().Name,
                session.SessionKey
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send {Composer} to session {SessionKey}",
                composer.GetType().Name,
                session.SessionKey
            );
        }
        finally
        {
            _sendSemaphore.Release();
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
