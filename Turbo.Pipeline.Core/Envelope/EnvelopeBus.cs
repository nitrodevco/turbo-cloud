using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Pipeline.Abstractions.Attributes;
using Turbo.Pipeline.Abstractions.Delegates;
using Turbo.Pipeline.Abstractions.Enums;
using Turbo.Pipeline.Abstractions.Envelope;
using Turbo.Pipeline.Abstractions.Registry;
using Turbo.Pipeline.Core.Configuration;

namespace Turbo.Pipeline.Core.Envelope;

public abstract class EnvelopeBus<TEnvelope, TInteraction, TContext, TConfig>(
    Channel<TEnvelope> ch,
    TConfig cfg,
    IServiceProvider root,
    ILogger logger
) : BackgroundService, IEnvelopeBus<TInteraction>
    where TEnvelope : EnvelopeBase<TInteraction>
    where TContext : PipelineContext
    where TConfig : PipelineConfig
{
    protected readonly Channel<TEnvelope> _ch = ch;
    protected readonly TConfig _cfg = cfg;

    protected readonly IServiceProvider _root = root;
    protected readonly ILogger _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var r = _ch.Reader;

        try
        {
            while (await r.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (r.TryRead(out var env))
                {
                    await EnqueueEnvelopeAsync(env, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    public async ValueTask PublishAsync(
        TInteraction interaction,
        object? args = null,
        string? tag = null,
        CancellationToken ct = default
    )
    {
        _cfg.OnPublished?.Invoke(interaction!);

        await WriteAsync(CreateEnvelope(interaction!, args, tag, null), ct);
    }

    public async Task PublishAndWaitAsync(
        TInteraction interaction,
        object? args = null,
        string? tag = null,
        CancellationToken ct = default
    )
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _cfg.OnPublished?.Invoke(interaction!);

        await WriteAsync(CreateEnvelope(interaction!, args, tag, tcs), ct);
        await tcs.Task.WaitAsync(ct);
    }

    private async ValueTask WriteAsync(TEnvelope env, CancellationToken ct)
    {
        var w = _ch.Writer;
        switch (_cfg.Backpressure)
        {
            case BackpressureMode.Wait:
                await w.WriteAsync(env, ct);
                break;
            case BackpressureMode.DropOldest:
                while (!w.TryWrite(env))
                    _ch.Reader.TryRead(out _);
                break;
            case BackpressureMode.DropNewest:
                w.TryWrite(env);
                break;
            case BackpressureMode.Fail:
                if (!w.TryWrite(env))
                    throw new InvalidOperationException("Envelope channel full");
                break;
        }
    }

    protected abstract TEnvelope CreateEnvelope(
        TInteraction interaction,
        object? args,
        string? tag,
        TaskCompletionSource? tcs
    );

    public abstract ValueTask EnqueueEnvelopeAsync(TEnvelope envelope, CancellationToken ct);

    protected abstract Type GetHandlerForType(Type interactionType);

    protected abstract Type GetBehaviorForType(Type interactionType);

    protected abstract TContext CreateContextForEnvelope(TEnvelope envelope, IServiceProvider sp);

    protected abstract Task ExecuteEnvelopesSequentially(
        TEnvelope env,
        TContext ctx,
        object[]? handlers,
        HandlerInvoker invoker,
        CancellationToken ct
    );

    protected abstract Task ExecuteEnvelopesParallel(
        TEnvelope env,
        TContext ctx,
        object[]? handlers,
        HandlerInvoker invoker,
        CancellationToken ct
    );

    protected async Task ProcessOne(TEnvelope env, CancellationToken ct)
    {
        if (env == null || env.Data == null)
            return;

        var started = Stopwatch.GetTimestamp();
        var eventType = env.Data.GetType();

        using var scope = _root.CreateScope();
        var sp = scope.ServiceProvider;

        var ctx = CreateContextForEnvelope(env, sp);
        var closedHandler = GetHandlerForType(eventType);
        var closedBehavior = GetBehaviorForType(eventType);

        var handlers = sp.GetServices(closedHandler)
            .Cast<object>()
            .OrderBy(h => h.GetType().GetCustomAttribute<OrderAttribute>()?.Value ?? 0)
            .Where(h =>
                env.Tag is null
                || h.GetType().GetCustomAttributes<TagAttribute>().Any(t => t.Tag == env.Tag)
            )
            .ToArray();

        var behaviors = sp.GetServices(closedBehavior)
            .Cast<object>()
            .OrderBy(b => b.GetType().GetCustomAttribute<OrderAttribute>()?.Value ?? 0)
            .Where(b =>
                env.Tag is null
                || b.GetType().GetCustomAttributes<TagAttribute>().Any(t => t.Tag == env.Tag)
            )
            .ToArray();

        if (handlers.Length == 0 && behaviors.Length == 0)
        {
            env.SyncTcs?.TrySetResult();

            return;
        }

        var handlerInvokerMap = handlers
            .Select(h => h.GetType())
            .Distinct()
            .ToDictionary(t => t, t => InvokerCache.GetHandlerInvoker(closedHandler, t));

        var behaviorInvokerMap = behaviors
            .Select(b => b.GetType())
            .Distinct()
            .ToDictionary(t => t, t => InvokerCache.GetBehaviorInvoker(closedBehavior, t));

        Task InvokeHandler(object handler, object data, PipelineContext ctx, CancellationToken ct)
        {
            var handlerType = handler.GetType();

            if (!handlerInvokerMap.TryGetValue(handlerType, out var invoke))
            {
                invoke = InvokerCache.GetHandlerInvoker(closedHandler, handlerType);
                handlerInvokerMap[handlerType] = invoke;
            }

            return invoke(handler, data, ctx, ct);
        }

        Task InvokeBehavior(
            object behavior,
            object message,
            PipelineContext ctx,
            Func<Task> next,
            CancellationToken ct
        )
        {
            var bt = behavior.GetType();

            if (!behaviorInvokerMap.TryGetValue(bt, out var inv))
            {
                inv = InvokerCache.GetBehaviorInvoker(closedBehavior, bt);
                behaviorInvokerMap[bt] = inv;
            }

            return inv(behavior, message, ctx, next, ct);
        }

        async Task InvokeHandlers()
        {
            if (ctx.IsAborted)
                return;

            if (_cfg.Execution == ExecutionMode.Sequential || handlers.Length <= 1)
            {
                await ExecuteEnvelopesSequentially(env, ctx, handlers, InvokeHandler, ct);
            }
            else
            {
                await ExecuteEnvelopesParallel(env, ctx, handlers, InvokeHandler, ct);
            }
        }

        var pipeline = behaviors
            .Reverse()
            .Aggregate(
                (Func<Task>)InvokeHandlers,
                (next, b) => (Func<Task>)(() => InvokeBehavior(b, env.Data, ctx, next, ct))
            );

        try
        {
            await pipeline().ConfigureAwait(false);

            _cfg.OnHandled?.Invoke(env, Stopwatch.GetElapsedTime(started));

            env.SyncTcs?.TrySetResult();
        }
        catch (Exception ex)
        {
            _cfg.OnError?.Invoke(env, ex);

            if (_cfg.SwallowHandlerExceptions)
                env.SyncTcs?.TrySetResult();
            else
                env.SyncTcs?.TrySetException(ex);
        }
    }
}
