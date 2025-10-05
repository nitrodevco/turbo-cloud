using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Pipeline.Delegates;
using Turbo.Pipeline.Registry;
using Turbo.Runtime;

namespace Turbo.Pipeline;

public class EnvelopeHost<TEnvelope, TMeta, TContext>(
    IServiceProvider host,
    EnvelopeHostOptions<TEnvelope, TMeta, TContext> options
)
{
    private readonly IServiceProvider _host = host;
    private readonly EnvelopeHostOptions<TEnvelope, TMeta, TContext> _opt = options;
    private readonly ConcurrentDictionary<Type, Bucket<TContext>> _byEvent = new();

    public IDisposable RegisterHandler(
        Type envType,
        IServiceProvider sp,
        Func<IServiceProvider, object> activator,
        HandlerInvoker<TContext> invoker
    )
    {
        ArgumentNullException.ThrowIfNull(envType);

        var b = _byEvent.GetOrAdd(envType, _ => new Bucket<TContext>());

        lock (b.Gate)
        {
            b.Handlers = b.Handlers.Add(new HandlerReg<TContext>(sp, activator, invoker));
            b.Version++;
            InvalidateCache(b);
        }

        return new ActionDisposable(() =>
        {
            lock (b.Gate)
            {
                b.Handlers = b.Handlers.RemoveAll(h =>
                    h.Activator == activator && h.Invoker == invoker
                );
                b.Version++;
                InvalidateCache(b);
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
        ArgumentNullException.ThrowIfNull(envType);

        var b = _byEvent.GetOrAdd(envType, _ => new Bucket<TContext>());

        lock (b.Gate)
        {
            b.Behaviors = b.Behaviors.Add(new BehaviorReg<TContext>(sp, order, activator, invoker));
            b.Version++;
            InvalidateCache(b);
        }

        return new ActionDisposable(() =>
        {
            lock (b.Gate)
            {
                b.Behaviors = b.Behaviors.RemoveAll(x =>
                    x.Activator == activator && x.Invoker == invoker && x.Order == order
                );
                b.Version++;
                InvalidateCache(b);
            }
        });
    }

    public async Task PublishAsync(TEnvelope env, TMeta? meta, CancellationToken ct)
    {
        if (env is null)
            return;

        var t = env.GetType();

        if (!_byEvent.TryGetValue(t, out var bucket) && !_opt.EnableInheritanceDispatch)
            return;

        var pipeline = GetOrBuildPipeline(t, bucket);

        if (pipeline is null)
            return;

        var ctx = _opt.CreateContext(env, meta);

        await pipeline(env!, ctx, ct).ConfigureAwait(false);
    }

    private Func<object, TContext, CancellationToken, ValueTask>? GetOrBuildPipeline(
        Type envType,
        Bucket<TContext>? primaryBucket
    )
    {
        if (!_opt.EnableInheritanceDispatch)
        {
            if (primaryBucket is null)
                return null;

            var cached = Volatile.Read(ref primaryBucket.CachedPipeline);

            if (
                cached is not null
                && primaryBucket.CachedVersion == primaryBucket.Version
                && primaryBucket.CachedForEnvType == envType
            )
                return cached;

            lock (primaryBucket.Gate)
            {
                if (
                    primaryBucket.CachedPipeline is not null
                    && primaryBucket.CachedVersion == primaryBucket.Version
                    && primaryBucket.CachedForEnvType == envType
                )
                    return primaryBucket.CachedPipeline;

                var pipeline = BuildPipeline(primaryBucket.Handlers, primaryBucket.Behaviors);

                primaryBucket.CachedPipeline = pipeline;
                primaryBucket.CachedVersion = primaryBucket.Version;
                primaryBucket.CachedForEnvType = envType;

                return pipeline;
            }
        }

        var (handlers, behaviors, globalVersion) = ResolveForType(envType);

        primaryBucket ??= _byEvent.GetOrAdd(envType, _ => new Bucket<TContext>());

        var cached2 = Volatile.Read(ref primaryBucket.CachedPipeline);

        if (
            cached2 is not null
            && primaryBucket.CachedVersion == globalVersion
            && primaryBucket.CachedForEnvType == envType
        )
            return cached2;

        lock (primaryBucket.Gate)
        {
            if (
                primaryBucket.CachedPipeline is not null
                && primaryBucket.CachedVersion == globalVersion
                && primaryBucket.CachedForEnvType == envType
            )
                return primaryBucket.CachedPipeline;

            var pipeline = BuildPipeline(handlers, behaviors);

            primaryBucket.CachedPipeline = pipeline;
            primaryBucket.CachedVersion = globalVersion;
            primaryBucket.CachedForEnvType = envType;

            return pipeline;
        }

        (
            ImmutableArray<HandlerReg<TContext>>,
            ImmutableArray<BehaviorReg<TContext>>,
            int
        ) ResolveForType(Type t)
        {
            var types = EnumerateTypeGraph(t);
            var handlerBuilder = ImmutableArray.CreateBuilder<HandlerReg<TContext>>();
            var behaviorBuilder = ImmutableArray.CreateBuilder<BehaviorReg<TContext>>();
            var versionSum = 0;

            foreach (var tp in types)
            {
                if (_byEvent.TryGetValue(tp, out var b))
                {
                    versionSum = unchecked(versionSum + b.Version);
                    handlerBuilder.AddRange(b.Handlers);
                    behaviorBuilder.AddRange(b.Behaviors);
                }
            }

            var behaviors = behaviorBuilder
                .ToImmutable()
                .Sort(
                    static (a, b) =>
                    {
                        var cmp = a.Order.CompareTo(b.Order);
                        return cmp != 0 ? cmp : 0;
                    }
                );

            return (handlerBuilder.ToImmutable(), behaviors, versionSum);
        }

        static IEnumerable<Type> EnumerateTypeGraph(Type t)
        {
            yield return t;

            for (var cur = t.BaseType; cur is not null; cur = cur.BaseType)
                yield return cur;

            foreach (var iface in t.GetInterfaces())
                yield return iface;
        }
    }

    private Func<object, TContext, CancellationToken, ValueTask> BuildPipeline(
        ImmutableArray<HandlerReg<TContext>> handlers,
        ImmutableArray<BehaviorReg<TContext>> behaviors
    )
    {
        return async (env, ctx, ct) =>
        {
            var bag = new JoinedScopeBag(_host.CreateAsyncScope());

            try
            {
                Func<object, TContext, CancellationToken, ValueTask> terminal = async (
                    env,
                    ctx,
                    ct
                ) => await InvokeHandlersAsync(handlers, env, ctx, ct).ConfigureAwait(false);

                for (int i = behaviors.Length - 1; i >= 0; i--)
                {
                    var beh = behaviors[i];
                    var next = terminal;

                    terminal = async (env, ctx, ct) =>
                    {
                        var sp = bag.Get(beh.ServiceProvider);

                        object? inst = null;

                        try
                        {
                            inst = beh.Activator(sp);
                        }
                        catch (Exception ex)
                        {
                            _opt.OnBehaviorActivationError?.Invoke(ex, env);

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
                            _opt.OnBehaviorInvokeError?.Invoke(ex, env);
                        }
                        finally
                        {
                            Console.WriteLine("Finished the behavior");
                            if (inst is IAsyncDisposable iad)
                                await iad.DisposeAsync().ConfigureAwait(false);
                            else if (inst is IDisposable d)
                                d.Dispose();
                        }
                    };
                }

                await terminal(env, ctx, ct).ConfigureAwait(false);
            }
            finally
            {
                Console.WriteLine("Disposing the bag");
                await bag.DisposeAsync().ConfigureAwait(false);
            }
        };
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
        IServiceScope scope = _host.CreateAsyncScope();

        try
        {
            if (scope != h.ServiceProvider)
            {
                scope = new JoinedScope(scope, h.ServiceProvider.CreateAsyncScope());
            }

            object? inst = null;

            try
            {
                inst = h.Activator(scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                _opt.OnHandlerActivationError?.Invoke(ex, env);

                return;
            }

            try
            {
                await h.Invoker(inst, env, ctx, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _opt.OnHandlerInvokeError?.Invoke(ex, env);
            }
            finally
            {
                if (inst is IAsyncDisposable iad)
                    await iad.DisposeAsync().ConfigureAwait(false);
                else if (inst is IDisposable d)
                    d.Dispose();
            }
        }
        finally
        {
            if (scope is IAsyncDisposable pad)
                await pad.DisposeAsync().ConfigureAwait(false);
            else
                scope.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvalidateCache(Bucket<TContext> b)
    {
        b.CachedPipeline = null;
        b.CachedForEnvType = null;
        b.CachedVersion = 0;
    }
}
