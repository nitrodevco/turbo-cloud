using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Networking.Configuration;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Networking;

namespace Turbo.Networking.Session;

/// <summary>
/// Keeps the connection list honest. A client that sleeps, loses its network or is killed sends
/// no close, and the socket stays open on the server's side; its player went on being online
/// and standing in their room until something wrote to it and failed, which might be never.
///
/// Every <see cref="NetworkingConfig.PingIntervalMilliseconds"/> each logged-in connection is
/// sent a Ping, which the client answers with a Pong (<c>IncomingMessages.onPing</c>); any
/// packet counts as an answer (<see cref="ISessionContext.MarkReceived"/>). A connection silent
/// for longer than <see cref="NetworkingConfig.SessionTimeoutMilliseconds"/> is closed, and the
/// ordinary close path (<see cref="SessionGateway.RemoveSessionAsync"/>) takes the player out of
/// their room and offline. A connection that never logged in is not pinged, so it is closed once
/// it has been silent that long too.
///
/// The connections are checked side by side, and a Ping that cannot be sent within one interval
/// closes its connection: a client that keeps sending but stops reading fills its socket, its
/// sends wait on one another, and checking connections one after another let that one stall the
/// heartbeat for every other connection in the hotel.
/// </summary>
internal sealed class SessionHeartbeat(
    NetworkingConfig config,
    ISessionGateway sessionGateway,
    ILogger<SessionHeartbeat> logger
) : IAsyncDisposable
{
    private readonly NetworkingConfig _config = config;
    private readonly ISessionGateway _sessionGateway = sessionGateway;
    private readonly ILogger<SessionHeartbeat> _logger = logger;

    // One composer for every ping: it carries nothing, and a composer is never changed after it
    // is handed to a send.
    private static readonly PingMessage PING = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public void Start()
    {
        if (_loop is not null)
            return;

        _cts = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is null || _loop is null)
            return;

        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // The loop ends by being cancelled; that is the normal stop.
        }

        _cts.Dispose();
        _cts = null;
        _loop = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromMilliseconds(_config.PingIntervalMilliseconds)
        );

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            // One bad tick must not stop the heartbeat: a connection nobody checks again is the
            // bug this exists for.
            try
            {
                await TickAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Session heartbeat tick failed");
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var silentSince =
            DateTime.UtcNow - TimeSpan.FromMilliseconds(_config.SessionTimeoutMilliseconds);
        var checks = new List<Task>();

        foreach (var session in _sessionGateway.GetSessions())
        {
            if (!session.Connection.IsClosed)
                checks.Add(CheckEachAsync(session, silentSince, ct));
        }

        await Task.WhenAll(checks).ConfigureAwait(false);
    }

    // Each connection on its own: one that fails to close must not keep the rest from being checked.
    private async Task CheckEachAsync(
        ISessionContext session,
        DateTime silentSince,
        CancellationToken ct
    )
    {
        try
        {
            await CheckAsync(session, silentSince, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Session heartbeat failed for session {SessionKey}",
                session.SessionKey
            );
        }
    }

    private async Task CheckAsync(
        ISessionContext session,
        DateTime silentSince,
        CancellationToken ct
    )
    {
        if (session.LastReceivedUtc < silentSince)
        {
            _logger.LogInformation(
                "Closing session {SessionKey}: nothing received since {LastReceived}",
                session.SessionKey,
                session.LastReceivedUtc
            );

            await session
                .CloseSessionAsync(
                    $"heartbeat timeout, nothing received since {session.LastReceivedUtc:O}"
                )
                .ConfigureAwait(false);

            return;
        }

        // Only a logged-in client is set up to answer; before the handshake finishes a Ping would
        // reach one that cannot read it yet.
        if (_sessionGateway.GetPlayerId(session.SessionKey) <= 0)
            return;

        using var ping = CancellationTokenSource.CreateLinkedTokenSource(ct);

        ping.CancelAfter(_config.PingIntervalMilliseconds);

        try
        {
            await session.SendComposerAsync(PING, ping.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Closing session {SessionKey}: a Ping could not be sent within {Interval} ms",
                session.SessionKey,
                _config.PingIntervalMilliseconds
            );

            await session
                .CloseSessionAsync(
                    $"heartbeat send stalled for {_config.PingIntervalMilliseconds} ms"
                )
                .ConfigureAwait(false);
        }
    }
}
