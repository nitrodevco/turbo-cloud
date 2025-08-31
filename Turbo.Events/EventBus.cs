using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Core.Events;
using Turbo.Core.Events.Enums;

namespace Turbo.Events;

public class EventBus : IEventBus
{
    private readonly Channel<EventEnvelope> _channel;
    private readonly IEventBusConfig _opts;
    private readonly ILogger<EventBus> _log;

    public EventBus(Channel<EventEnvelope> channel, IEventBusConfig opts, ILogger<EventBus> log)
    {
        _channel = channel;
        _opts = opts;
        _log = log;
    }

    public async ValueTask PublishAsync<TEvent>(
        TEvent @event,
        string? tag = null,
        CancellationToken ct = default
    )
        where TEvent : IEvent
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
        where TEvent : IEvent
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
