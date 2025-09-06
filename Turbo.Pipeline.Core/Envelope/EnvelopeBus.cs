using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Turbo.Pipeline.Abstractions.Attributes;
using Turbo.Pipeline.Abstractions.Enums;
using Turbo.Pipeline.Abstractions.Envelope;
using Turbo.Pipeline.Abstractions.Registry;
using Turbo.Pipeline.Core.Configuration;

namespace Turbo.Pipeline.Core.Envelope;

public abstract class EnvelopeBus<TEnvelope, TInteraction, TContext, TConfig>
    : IEnvelopeBus<TInteraction>
    where TEnvelope : EnvelopeBase<TInteraction>
    where TContext : PipelineContext
    where TConfig : PipelineConfig
{
    protected readonly TConfig _cfg;
    protected readonly IServiceProvider _root;
    protected readonly ILogger _logger;

    private readonly OrderedPerKeyDispatcher<string, TEnvelope> _dispatcher;

    public EnvelopeBus(TConfig cfg, IServiceProvider root, ILogger logger)
    {
        _cfg = cfg;
        _root = root;
        _logger = logger;

        _dispatcher = new OrderedPerKeyDispatcher<string, TEnvelope>(
            keySelector: GetKeyForEnvelope,
            handler: ProcessOne,
            capacityPerKey: _cfg.ChannelCapacity,
            fullMode: _cfg.Backpressure,
            dropIfCanceledBeforeStart: true
        );
    }

    public Task PublishAsync(
        TInteraction interaction,
        object? args = null,
        string? tag = null,
        CancellationToken ct = default
    ) => _dispatcher.EnqueueAndWaitAsync(CreateEnvelope(interaction!, args, tag), ct);

    public Task PublishFireAndForgetAsync(
        TInteraction interaction,
        object? args = null,
        string? tag = null,
        CancellationToken ct = default
    ) => _dispatcher.EnqueueFireAndForgetAsync(CreateEnvelope(interaction!, args, tag), ct);

    public virtual void CompleteKey(string key) => _dispatcher.CompleteKey(key);

    public virtual void AbortKey(string key) => _dispatcher.AbortKey(key);

    protected abstract string GetKeyForEnvelope(TEnvelope envelope);

    protected abstract TEnvelope CreateEnvelope(
        TInteraction interaction,
        object? args,
        string? tag
    );

    protected abstract Type GetHandlerForType(Type interactionType);

    protected abstract Type GetBehaviorForType(Type interactionType);

    protected abstract TContext CreateContextForEnvelope(TEnvelope envelope, IServiceProvider sp);

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
            return;

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
                foreach (var h in handlers)
                {
                    await InvokeHandler(h, env.Data, ctx, ct).ConfigureAwait(false);
                }
            }
            else
            {
                await ParallelHelpers
                    .RunBoundedAsync(
                        handlers,
                        _cfg.DegreeOfParallelism,
                        h => InvokeHandler(h, env.Data, ctx, ct),
                        ct
                    )
                    .ConfigureAwait(false);
            }
        }

        var pipeline = behaviors
            .Reverse()
            .Aggregate(
                InvokeHandlers,
                (next, b) => () => InvokeBehavior(b, env.Data, ctx, next, ct)
            );

        await pipeline().ConfigureAwait(false);
    }
}
