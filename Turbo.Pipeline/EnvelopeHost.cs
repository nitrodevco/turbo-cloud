using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Pipeline.Delegates;
using Turbo.Pipeline.Registry;
using Turbo.Runtime;

namespace Turbo.Pipeline;

/// <summary>
/// Dispatches envelopes to their registered handlers and behaviors. A handler or behavior that
/// fails is logged here and does not stop its siblings, and the failure is not rethrown: this is
/// the one place a handler failure is logged, so callers must not log it again.
/// </summary>
public class EnvelopeHost<TEnvelope, TMeta, TContext>(
    IServiceProvider host,
    EnvelopeHostOptions<TEnvelope, TMeta, TContext> options,
    ILogger logger
)
{
    private readonly IServiceProvider _host = host;
    private readonly EnvelopeHostOptions<TEnvelope, TMeta, TContext> _opt = options;
    private readonly ILogger _logger = logger;
    private readonly ConcurrentDictionary<Type, Bucket<TContext>> _byEvent = new();

    // The pipeline for each envelope type, built on first use and kept until any registration
    // changes. With inheritance dispatch a type's pipeline draws on the buckets of its base types
    // and interfaces, so one registration can change many pipelines: every change bumps the
    // version and drops them all, rather than working out which ones it touched. Dropping them
    // also lets go of the types of an unloaded plugin.
    private readonly ConcurrentDictionary<Type, CachedPipeline> _pipelines = new();
    private int _registrationVersion;

    public IDisposable RegisterHandler(
        Type envType,
        Type handlerType,
        IServiceProvider sp,
        Func<IServiceProvider, object> activator,
        HandlerInvoker<TContext> invoker
    )
    {
        ArgumentNullException.ThrowIfNull(envType);

        var b = _byEvent.GetOrAdd(envType, _ => new Bucket<TContext>());
        var resolutionProvider = sp == _host ? _host : new CompositeServiceProvider(sp, _host);

        lock (b.Gate)
        {
            b.Handlers = b.Handlers.Add(
                new HandlerReg<TContext>(handlerType, sp, resolutionProvider, activator, invoker)
            );
            OnRegistrationsChanged();
        }

        return new ActionDisposable(() =>
        {
            lock (b.Gate)
            {
                b.Handlers = b.Handlers.RemoveAll(h =>
                    h.Activator == activator && h.Invoker == invoker
                );
                OnRegistrationsChanged();
            }
        });
    }

    public IDisposable RegisterBehavior(
        Type envType,
        Type behaviorType,
        IServiceProvider sp,
        Func<IServiceProvider, object> activator,
        BehaviorInvoker<TContext> invoker,
        int order
    )
    {
        ArgumentNullException.ThrowIfNull(envType);

        var b = _byEvent.GetOrAdd(envType, _ => new Bucket<TContext>());

        lock (b.Gate)
        {
            b.Behaviors = b.Behaviors.Add(
                new BehaviorReg<TContext>(
                    behaviorType,
                    sp,
                    new CompositeServiceProvider(_host, sp),
                    order,
                    activator,
                    invoker
                )
            );
            OnRegistrationsChanged();
        }

        return new ActionDisposable(() =>
        {
            lock (b.Gate)
            {
                b.Behaviors = b.Behaviors.RemoveAll(x =>
                    x.Activator == activator && x.Invoker == invoker && x.Order == order
                );
                OnRegistrationsChanged();
            }
        });
    }

    public async Task PublishAsync(TEnvelope env, TMeta? meta, CancellationToken ct)
    {
        if (env is null)
            return;

        var pipeline = GetOrBuildPipeline(env.GetType());

        // Nothing handles this type, so its context (which may cost a lookup) is not built.
        if (pipeline is null)
            return;

        var ctx = await _opt.CreateContextAsync(env, meta).ConfigureAwait(false);

        await pipeline(env, ctx, ct).ConfigureAwait(false);
    }

    private void OnRegistrationsChanged()
    {
        Interlocked.Increment(ref _registrationVersion);
        _pipelines.Clear();
    }

    // The version is read before the registrations, so a pipeline built while a registration
    // changes is stored under the old version and rebuilt on the next publish, never kept.
    private Func<object, TContext, CancellationToken, ValueTask>? GetOrBuildPipeline(Type envType)
    {
        var version = Volatile.Read(ref _registrationVersion);

        if (_pipelines.TryGetValue(envType, out var cached) && cached.Version == version)
            return cached.Pipeline;

        var (handlers, behaviors) = ResolveForType(envType);
        var pipeline =
            handlers.IsEmpty && behaviors.IsEmpty ? null : BuildPipeline(handlers, behaviors);

        _pipelines[envType] = new CachedPipeline(version, pipeline);

        return pipeline;
    }

    private (
        ImmutableArray<HandlerReg<TContext>> Handlers,
        ImmutableArray<BehaviorReg<TContext>> Behaviors
    ) ResolveForType(Type t)
    {
        if (!_opt.EnableInheritanceDispatch)
        {
            if (!_byEvent.TryGetValue(t, out var own))
                return ([], []);

            return (own.Handlers, SortByOrder(own.Behaviors));
        }

        var handlerBuilder = ImmutableArray.CreateBuilder<HandlerReg<TContext>>();
        var behaviorBuilder = ImmutableArray.CreateBuilder<BehaviorReg<TContext>>();

        foreach (var tp in EnumerateTypeGraph(t))
        {
            if (_byEvent.TryGetValue(tp, out var b))
            {
                handlerBuilder.AddRange(b.Handlers);
                behaviorBuilder.AddRange(b.Behaviors);
            }
        }

        return (handlerBuilder.ToImmutable(), SortByOrder(behaviorBuilder.ToImmutable()));

        static IEnumerable<Type> EnumerateTypeGraph(Type t)
        {
            yield return t;

            for (var cur = t.BaseType; cur is not null; cur = cur.BaseType)
                yield return cur;

            foreach (var iface in t.GetInterfaces())
                yield return iface;
        }
    }

    private static ImmutableArray<BehaviorReg<TContext>> SortByOrder(
        ImmutableArray<BehaviorReg<TContext>> behaviors
    ) => behaviors.Sort(static (a, b) => a.Order.CompareTo(b.Order));

    // The behavior chain is built here, once per pipeline; only the continuation handed to each
    // behavior is created per envelope. With no behaviors the pipeline is the handler call.
    private Func<object, TContext, CancellationToken, ValueTask> BuildPipeline(
        ImmutableArray<HandlerReg<TContext>> handlers,
        ImmutableArray<BehaviorReg<TContext>> behaviors
    )
    {
        Func<object, TContext, CancellationToken, ValueTask> terminal = (env, ctx, ct) =>
            InvokeHandlersAsync(handlers, env, ctx, ct);

        for (int i = behaviors.Length - 1; i >= 0; i--)
        {
            var beh = behaviors[i];
            var next = terminal;

            terminal = (env, ctx, ct) => InvokeBehaviorAsync(beh, next, env, ctx, ct);
        }

        return terminal;
    }

    private async ValueTask InvokeBehaviorAsync(
        BehaviorReg<TContext> beh,
        Func<object, TContext, CancellationToken, ValueTask> next,
        object env,
        TContext ctx,
        CancellationToken ct
    )
    {
        object? inst = null;

        try
        {
            inst = beh.Activator(beh.ResolutionProvider);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to activate behavior {BehaviorType} for {EnvelopeType}",
                beh.BehaviorType,
                env.GetType()
            );

            await next(env, ctx, ct).ConfigureAwait(false);

            return;
        }

        try
        {
            await beh.Invoker(
                    inst,
                    env,
                    ctx,
                    async () => await next(env, ctx, ct).ConfigureAwait(false),
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Behavior {BehaviorType} failed for {EnvelopeType}",
                beh.BehaviorType,
                env.GetType()
            );
        }
        finally
        {
            if (inst is IAsyncDisposable iad)
                await iad.DisposeAsync().ConfigureAwait(false);
            else if (inst is IDisposable d)
                d.Dispose();
        }
    }

    private async ValueTask InvokeHandlersAsync(
        ImmutableArray<HandlerReg<TContext>> regs,
        object env,
        TContext ctx,
        CancellationToken ct
    )
    {
        if (regs.IsDefaultOrEmpty)
            return;

        if (_opt.HandlerMode == HandlerExecutionMode.Sequential)
        {
            for (int i = 0; i < regs.Length; i++)
                await InvokeOneAsync(regs[i], env, ctx, ct).ConfigureAwait(false);

            return;
        }

        if (regs.Length == 1)
        {
            await InvokeOneAsync(regs[0], env, ctx, ct).ConfigureAwait(false);

            return;
        }

        if (_opt.MaxHandlerDegreeOfParallelism is int dop && dop > 0 && dop < regs.Length)
        {
            var work = new List<Func<CancellationToken, ValueTask>>(regs.Length);

            for (int i = 0; i < regs.Length; i++)
            {
                var r = regs[i];
                work.Add(token => InvokeOneAsync(r, env, ctx, token));
            }

            await BoundedHelper.RunAsync(work, dop, ct).ConfigureAwait(false);
        }
        else
        {
            var tasks = new Task[regs.Length];

            for (int i = 0; i < regs.Length; i++)
                tasks[i] = InvokeOneAsync(regs[i], env, ctx, ct).AsTask();

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    private async ValueTask InvokeOneAsync(
        HandlerReg<TContext> h,
        object env,
        TContext ctx,
        CancellationToken ct
    )
    {
        object? inst = null;

        try
        {
            inst = h.Activator(h.ResolutionProvider);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to activate handler {HandlerType} for {EnvelopeType}",
                h.HandlerType,
                env.GetType()
            );

            return;
        }

        try
        {
            await h.Invoker(inst, env, ctx, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Handler {HandlerType} failed for {EnvelopeType}",
                h.HandlerType,
                env.GetType()
            );
        }
        finally
        {
            if (inst is IAsyncDisposable iad)
                await iad.DisposeAsync().ConfigureAwait(false);
            else if (inst is IDisposable d)
                d.Dispose();
        }
    }

    private sealed record CachedPipeline(
        int Version,
        Func<object, TContext, CancellationToken, ValueTask>? Pipeline
    );
}
