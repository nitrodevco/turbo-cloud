using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Core.Events;
using Turbo.Core.Events.Enums;
using Turbo.Core.Events.Registry;
using Turbo.Events.Exceptions;

namespace Turbo.Events;

public class EventProcessor : BackgroundService
{
    private readonly Channel<EventEnvelope> _channel;
    private readonly IServiceProvider _root;
    private readonly EventRegistry _registry;
    private readonly IEventBusConfig _opts;
    private readonly ILogger<EventProcessor> _log;
    private readonly PartitionedExecutor _executor = new();

    public EventProcessor(
        Channel<EventEnvelope> channel,
        IServiceProvider root,
        EventRegistry reg,
        IEventBusConfig opts,
        ILogger<EventProcessor> log
    )
    {
        _channel = channel;
        _root = root;
        _registry = reg;
        _opts = opts;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _channel.Reader;
        while (await reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
        {
            while (reader.TryRead(out var env))
            {
                _ = ProcessOne(env, stoppingToken); // do not await; we manage concurrency internally
            }
        }
    }

    private async Task ProcessOne(EventEnvelope env, CancellationToken ct)
    {
        var evt = env.Event;
        var evtType = evt.GetType();
        var tag = env.Tag;
        var (handlers, behaviors) = _registry.Get(evtType, tag);

        if (handlers.Count == 0 && behaviors.Count == 0)
        {
            env.SyncTcs?.TrySetResult();

            return;
        }

        using var scope = _root.CreateScope();

        var sp = scope.ServiceProvider;
        var ctx = new EventContext { Services = sp };
        var started = Stopwatch.GetTimestamp();

        async Task invokeHandlers()
        {
            if (_opts.PartitionKey is { } pkSelector && pkSelector(evt) is { } key)
            {
                // Per-key serial execution; schedule each handler onto the key mailbox
                foreach (var h in handlers)
                    await _executor
                        .Enqueue(key, () => InvokeHandler(sp, h, evt, ctx, ct), ct)
                        .ConfigureAwait(false);
            }
            else if (_opts.Execution == ExecutionModeType.Sequential)
            {
                foreach (var h in handlers)
                    await InvokeHandler(sp, h, evt, ctx, ct).ConfigureAwait(false);
            }
            else
            {
                await ParallelHelpers
                    .RunBoundedAsync(
                        handlers,
                        _opts.DegreeOfParallelism,
                        h => InvokeHandler(sp, h, evt, ctx, ct),
                        ct
                    )
                    .ConfigureAwait(false);
            }
        }

        Func<Task> pipeline = behaviors
            .Select(b => b.Invoke)
            .Reverse()
            .Aggregate(invokeHandlers, (next, b) => () => b(sp, evt, ctx, next, ct));

        try
        {
            await pipeline().ConfigureAwait(false);
            _opts.OnHandled?.Invoke(evt, Stopwatch.GetElapsedTime(started));
            env.SyncTcs?.TrySetResult();
        }
        catch (EventAbortedException ex)
        {
            // treat as non-error abort
            _opts.OnHandled?.Invoke(evt, Stopwatch.GetElapsedTime(started));
            env.SyncTcs?.TrySetResult();
            // optional: metrics/log at a lower level
            _log.LogDebug("Event {Type} aborted: {Reason}", evtType.Name, ex.Message);
        }
        catch (Exception ex)
        {
            _opts.OnError?.Invoke(evt, ex);
            if (_opts.SwallowHandlerExceptions)
                env.SyncTcs?.TrySetResult();
            else
                env.SyncTcs?.TrySetException(ex);
        }
    }

    private async Task InvokeHandler(
        IServiceProvider sp,
        Handler h,
        object evt,
        EventContext ctx,
        CancellationToken ct
    )
    {
        try
        {
            var instance = sp.GetRequiredService(h.ServiceType);
            await h.Invoke(instance, evt, ctx, ct).ConfigureAwait(false);
        }
        catch
        {
            throw; // upstream decides swallow vs throw
        }
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        _log.LogInformation("Stopping EventPump; completing channel and draining…");
        _channel.Writer.TryComplete();
        await _executor.DrainAsync(TimeSpan.FromSeconds(10)); // best-effort drain
        await base.StopAsync(ct);
    }
}
