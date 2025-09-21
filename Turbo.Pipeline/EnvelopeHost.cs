using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Pipeline.Delegates;
using Turbo.Pipeline.Registry;
using Turbo.Runtime;

namespace Turbo.Pipeline;

public class EnvelopeHost<TEnvelope, TMeta, TContext>(
    Func<TEnvelope, TMeta, TContext> createContext
)
{
    private readonly ConcurrentDictionary<Type, Bucket<TContext>> _byEvent = new();
    private readonly Func<TEnvelope, TMeta, TContext> _createContext = createContext;

    public IDisposable RegisterHandler(
        Type envType,
        IServiceProvider sp,
        Func<IServiceProvider, object> activator,
        HandlerInvoker<TContext> invoker
    )
    {
        var b = _byEvent.GetOrAdd(envType, _ => new Bucket<TContext>());

        lock (b.Gate)
        {
            b.Handlers.Add(new HandlerReg<TContext>(sp, activator, invoker));
            b.Version++;
        }

        return new ActionDisposable(() =>
        {
            lock (b.Gate)
            {
                b.Handlers.RemoveAll(h => h.Activator == activator && h.Invoker == invoker);
                b.Version++;
            }
        });
    }

    public IDisposable RegisterBehavior(
        Type envType,
        IServiceProvider sp,
        Func<IServiceProvider, object> activator,
        BehaviorInvoker<TContext> invoker,
        int order
    )
    {
        var b = _byEvent.GetOrAdd(envType, _ => new Bucket<TContext>());

        lock (b.Gate)
        {
            b.Behaviors.Add(new BehaviorReg<TContext>(sp, order, activator, invoker));
            b.Version++;
        }

        return new ActionDisposable(() =>
        {
            lock (b.Gate)
            {
                b.Behaviors.RemoveAll(x =>
                    x.Activator == activator && x.Invoker == invoker && x.Order == order
                );
                b.Version++;
            }
        });
    }

    public async Task PublishAsync(TEnvelope env, TMeta meta, CancellationToken ct)
    {
        var t = env?.GetType();

        if (t is null || env is null || !_byEvent.TryGetValue(t, out var bucket))
            return;

        var pipe = GetOrBuildPipeline(t, bucket);
        var ctx = _createContext(env, meta);

        await pipe(env, ctx, ct).ConfigureAwait(false);
    }

    private static Func<object, TContext, CancellationToken, Task> GetOrBuildPipeline(
        Type envType,
        Bucket<TContext> bucket
    )
    {
        var cached = bucket.CachedPipeline;

        if (cached is not null && bucket.CachedVersion == bucket.Version)
            return cached;

        lock (bucket.Gate)
        {
            if (bucket.CachedPipeline is not null && bucket.CachedVersion == bucket.Version)
                return bucket.CachedPipeline;

            var handlers = bucket.Handlers.ToArray();
            var behaviors = bucket.Behaviors.OrderBy(x => x.Order).ToArray();
            var pipeline = BuildPipeline(envType, handlers, behaviors);

            bucket.CachedPipeline = pipeline;
            bucket.CachedVersion = bucket.Version;

            return pipeline;
        }
    }

    private static Func<object, TContext, CancellationToken, Task> BuildPipeline(
        Type eventType,
        HandlerReg<TContext>[] handlers,
        BehaviorReg<TContext>[] behaviors
    )
    {
        async Task RunAsync(object env, TContext ctx, CancellationToken ct)
        {
            await using var scopes = new ScopeBag();

            Task terminal() => InvokeHandlers(scopes, handlers, env, ctx, ct);

            var composed = behaviors
                .AsEnumerable()
                .Reverse()
                .Aggregate(
                    () => terminal(),
                    (next, beh) =>
                        () =>
                        {
                            var sp = scopes.Get(beh.ServiceProvider);
                            var inst = beh.Activator(sp);

                            try
                            {
                                return beh.Invoker(inst, env, ctx, () => next(), ct);
                            }
                            finally
                            {
                                if (inst is IAsyncDisposable iad)
                                    iad.DisposeAsync().AsTask();
                                if (inst is IDisposable d)
                                    d.Dispose();
                            }
                        }
                );

            await composed().ConfigureAwait(false);
        }

        return RunAsync;

        static async Task InvokeHandlers(
            ScopeBag scopes,
            HandlerReg<TContext>[] regs,
            object env,
            TContext ctx,
            CancellationToken ct
        )
        {
            if (regs.Length == 0)
                return;

            var tasks = new List<Task>(regs.Length);

            foreach (var h in regs)
            {
                var sp = scopes.Get(h.ServiceProvider);
                var inst = h.Activator(sp);

                tasks.Add(Run(inst, h, env, ctx, ct));

                static async Task Run(
                    object inst,
                    HandlerReg<TContext> h,
                    object env,
                    TContext ctx,
                    CancellationToken ct
                )
                {
                    try
                    {
                        await h.Invoker(inst, env, ctx, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (inst is IAsyncDisposable iad)
                            iad.DisposeAsync().AsTask();
                        if (inst is IDisposable d)
                            d.Dispose();
                    }
                }
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }
}
