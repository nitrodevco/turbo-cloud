using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.LoadBots.Metrics;
using Turbo.LoadBots.Protocol;
using Turbo.Revisions.Revision20260909;

namespace Turbo.LoadBots.Client;

/// <summary>The server did not send what a bot was waiting for in time.</summary>
public sealed class BotTimeoutException(string message) : Exception(message);

/// <summary>The connection closed while a bot was waiting for a reply.</summary>
public sealed class BotDisconnectedException(string message) : Exception(message);

/// <summary>
/// One socket to the server. A receive loop frames the messages, hands each to the handlers
/// registered for its header (which keep the bot's picture of the world current) and then to
/// whoever is waiting for it. Pings are answered here, as the client does.
/// </summary>
public sealed class BotConnection : IAsyncDisposable
{
    private readonly IBotTransport _transport;
    private readonly BotMetrics _metrics;
    private readonly ILogger _logger;
    private readonly Dictionary<int, List<Action<ServerMessage>>> _handlers = [];
    private readonly List<Waiter> _waiters = [];
    private readonly Lock _waitersLock = new();
    private readonly CancellationTokenSource _closing = new();
    private Task? _receiveLoop;

    public BotConnection(IBotTransport transport, BotMetrics metrics, ILogger logger)
    {
        _transport = transport;
        _metrics = metrics;
        _logger = logger;

        On(
            MessageComposer.PingMessageComposer,
            ping => _ = SendQuietlyAsync(ClientRequests.Pong())
        );
    }

    public bool IsOpen => _receiveLoop is { IsCompleted: false };

    /// <summary>Completes when the server closes the socket or the bot disconnects.</summary>
    public Task Closed => _receiveLoop ?? Task.CompletedTask;

    public async Task ConnectAsync(CancellationToken ct)
    {
        await _transport.ConnectAsync(ct);
        _metrics.Count("connections.opened");
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_closing.Token), CancellationToken.None);
    }

    /// <summary>
    /// Registers a handler for one header. Handlers run on the receive loop in arrival order,
    /// before any waiter sees the message, so they must be quick and must not block.
    /// </summary>
    public void On(int header, Action<ServerMessage> handler)
    {
        if (!_handlers.TryGetValue(header, out var list))
            _handlers[header] = list = [];

        list.Add(handler);
    }

    /// <summary>
    /// Starts waiting for the first message <paramref name="match"/> accepts. Call it before
    /// sending the request it answers, so a fast reply cannot slip past.
    /// </summary>
    public Task<ServerMessage> ExpectAsync(
        Func<ServerMessage, bool> match,
        TimeSpan timeout,
        string what
    )
    {
        var waiter = new Waiter(match, what);

        lock (_waitersLock)
            _waiters.Add(waiter);

        _ = TimeOutAsync(waiter, timeout);

        return waiter.Completion.Task;
    }

    public Task<ServerMessage> ExpectAsync(int header, TimeSpan timeout, string what) =>
        ExpectAsync(message => message.Header == header, timeout, what);

    public async Task SendAsync(ClientMessage message, CancellationToken ct)
    {
        var frame = PacketFraming.Frame(message.Header, message.Body);

        await _transport.SendAsync(frame, ct);

        _metrics.Count("packets.sent");
        _metrics.Count("bytes.sent", frame.Length);
    }

    private async Task SendQuietlyAsync(ClientMessage message)
    {
        try
        {
            await SendAsync(message, _closing.Token);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogDebug(ex, "Could not send {Header}", message.Header);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        using var buffer = new ReceiveBuffer();
        var reason = "server closed the connection";

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var read = await _transport.ReceiveAsync(buffer.FreeSpace(), ct);

                if (read == 0)
                    break;

                buffer.Advance(read);
                _metrics.Count("bytes.received", read);

                while (PacketFraming.TryReadFrame(buffer.Data, out var message, out var consumed))
                {
                    buffer.Consume(consumed);
                    Dispatch(message!);
                }
            }

            if (ct.IsCancellationRequested)
                reason = "bot disconnected";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            reason = "bot disconnected";
        }
        catch (Exception ex)
        {
            reason = $"{ex.GetType().Name}: {ex.Message}";
            _logger.LogDebug(ex, "Receive loop ended");
        }

        if (reason != "bot disconnected")
            _metrics.Count("connections.dropped");

        FailAll(reason);
    }

    private void Dispatch(ServerMessage message)
    {
        _metrics.Count("packets.received");

        if (_handlers.TryGetValue(message.Header, out var handlers))
        {
            foreach (var handler in handlers)
            {
                try
                {
                    handler(message);
                }
                catch (Exception ex)
                {
                    // A message the bot cannot read is itself a finding: the decoder and the
                    // serializer disagree.
                    _metrics.Check(
                        "protocol.decodes",
                        false,
                        CheckSeverity.Hard,
                        $"header {message.Header}: {ex.Message}"
                    );
                }
            }
        }

        // Every waiter the message answers gets it: a stale waiter must not swallow a reply.
        List<Waiter>? matched = null;

        lock (_waitersLock)
        {
            for (var i = _waiters.Count - 1; i >= 0; i--)
            {
                if (!Matches(_waiters[i], message))
                    continue;

                (matched ??= []).Add(_waiters[i]);
                _waiters.RemoveAt(i);
            }
        }

        if (matched is null)
            return;

        foreach (var waiter in matched)
            waiter.Completion.TrySetResult(message);
    }

    /// <summary>A predicate that decodes may throw on a body it misreads; that is no match.</summary>
    private bool Matches(Waiter waiter, ServerMessage message)
    {
        try
        {
            return waiter.Match(message);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            _metrics.Check(
                "protocol.decodes",
                false,
                CheckSeverity.Hard,
                $"header {message.Header} while waiting for {waiter.What}: {ex.Message}"
            );

            return false;
        }
    }

    private async Task TimeOutAsync(Waiter waiter, TimeSpan timeout)
    {
        try
        {
            await Task.Delay(timeout, _closing.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_waitersLock)
        {
            if (!_waiters.Remove(waiter))
                return;
        }

        waiter.Completion.TrySetException(
            new BotTimeoutException($"No {waiter.What} within {timeout.TotalSeconds:0.#}s")
        );
    }

    private void FailAll(string reason)
    {
        List<Waiter> pending;

        lock (_waitersLock)
        {
            pending = [.. _waiters];
            _waiters.Clear();
        }

        foreach (var waiter in pending)
            waiter.Completion.TrySetException(
                new BotDisconnectedException($"Waiting for {waiter.What}: {reason}")
            );
    }

    public async ValueTask DisposeAsync()
    {
        await _closing.CancelAsync();

        if (_receiveLoop is not null)
        {
            try
            {
                await _receiveLoop;
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }

        await _transport.DisposeAsync();
        _closing.Dispose();
    }

    private sealed class Waiter(Func<ServerMessage, bool> match, string what)
    {
        public Func<ServerMessage, bool> Match { get; } = match;
        public string What { get; } = what;
        public TaskCompletionSource<ServerMessage> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
