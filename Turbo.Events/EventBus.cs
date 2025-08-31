using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Events.Abstractions;
using Turbo.Events.Abstractions.Enums;

namespace Turbo.Events;

public class EventBus(Channel<EventEnvelope> channel, IEventBusConfig opts, ILogger<EventBus> log)
    : IEventBus
{
    private readonly Channel<EventEnvelope> _channel = channel;
    private readonly IEventBusConfig _opts = opts;
    private readonly ILogger<EventBus> _logger = log;

    public async ValueTask PublishAsync<TEvent>(
        TEvent @event,
        string? tag = null,
        CancellationToken ct = default
    )
    {
        var env = new EventEnvelope(@event!, tag, null, DateTimeOffset.UtcNow);
        await WriteAsync(env, ct);
        _opts.OnPublished?.Invoke(@event!);
    }

    public async Task PublishAndWaitAsync<TEvent>(
        TEvent @event,
        string? tag = null,
        CancellationToken ct = default
    )
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var env = new EventEnvelope(@event!, tag, tcs, DateTimeOffset.UtcNow);
        await WriteAsync(env, ct);
        _opts.OnPublished?.Invoke(@event!);
        await tcs.Task.WaitAsync(ct);
    }

    private async ValueTask WriteAsync(EventEnvelope env, CancellationToken ct)
    {
        var w = _channel.Writer;
        switch (_opts.Backpressure)
        {
            case BackpressureModeType.Wait:
                await w.WriteAsync(env, ct);
                break;
            case BackpressureModeType.DropOldest:
                while (!w.TryWrite(env))
                {
                    // drain one
                    if (_channel.Reader.TryRead(out _))
                    { /* dropped oldest */
                    }
                    else
                        await Task.Delay(1, ct);
                }
                break;
            case BackpressureModeType.DropNewest:
                w.TryWrite(env); // drop if full
                break;
            case BackpressureModeType.Fail:
                if (!w.TryWrite(env))
                    throw new InvalidOperationException("Event channel is full");
                break;
        }
    }
}
