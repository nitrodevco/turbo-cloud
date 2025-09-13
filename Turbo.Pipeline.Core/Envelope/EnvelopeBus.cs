using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Turbo.Pipeline.Abstractions.Attributes;
using Turbo.Pipeline.Abstractions.Delegates;
using Turbo.Pipeline.Abstractions.Enums;
using Turbo.Pipeline.Abstractions.Envelope;
using Turbo.Pipeline.Abstractions.Registry;
using Turbo.Pipeline.Core.Configuration;
using Turbo.Pipeline.Core.Registry;

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

    private readonly ConcurrentDictionary<Type, List<Handler<TContext>>> _handlers = new();
    private readonly ConcurrentDictionary<Type, List<Behavior<TContext>>> _behaviors = new();

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
        CancellationToken ct = default
    ) => _dispatcher.EnqueueAndWaitAsync(CreateEnvelope(interaction!, args), ct);

    public Task PublishFireAndForgetAsync(
        TInteraction interaction,
        object? args = null,
        CancellationToken ct = default
    ) => _dispatcher.EnqueueFireAndForgetAsync(CreateEnvelope(interaction!, args), ct);

    public virtual void CompleteKey(string key) => _dispatcher.CompleteKey(key);

    public virtual void AbortKey(string key) => _dispatcher.AbortKey(key);

    protected abstract string GetKeyForEnvelope(TEnvelope envelope);

    protected abstract TEnvelope CreateEnvelope(TInteraction interaction, object? args);

    protected abstract Type GetHandlerForType();

    protected abstract Type GetBehaviorForType();

    protected abstract TContext CreateContextForEnvelope(TEnvelope envelope, IServiceProvider sp);

    protected async Task ProcessOne(TEnvelope env, CancellationToken ct)
    {
        if (env == null || env.Data == null)
            return;

        var started = Stopwatch.GetTimestamp();
        var eventType = env.Data.GetType();

        List<Behavior<TContext>> behaviors;
        if (_behaviors.TryGetValue(eventType, out var bList))
        {
            lock (bList)
                behaviors = [.. bList];
        }
        else
            behaviors = [];

        List<Handler<TContext>> handlers;
        if (_handlers.TryGetValue(eventType, out var hList))
        {
            lock (hList)
                handlers = [.. hList];
        }
        else
            handlers = [];

        if (handlers.Count == 0 && behaviors.Count == 0)
            return;

        using var scope = _root.CreateScope();

        var sp = scope.ServiceProvider;
        var ctx = CreateContextForEnvelope(env, sp);

        async Task InvokeAllHandlers()
        {
            // Host-owned
            foreach (var h in handlers.Where(x => x.UseAmbientScope))
            {
                var inst = h.CreateInScope(_root);
                await h.Invoke(inst, env.Data!, ctx, ct).ConfigureAwait(false);
            }

            // Plugin-owned (per-call scopes)
            foreach (var h in handlers.Where(x => !x.UseAmbientScope))
            {
                using var s = h.BeginScope!();
                var inst = h.CreateInScope(s.ServiceProvider);
                await h.Invoke(inst, env.Data!, ctx, ct).ConfigureAwait(false);
            }
        }

        Func<Task> next = InvokeAllHandlers;

        // Plugin behaviors (reverse compose)
        for (int i = behaviors.Count - 1; i >= 0; i--)
        {
            var b = behaviors[i];
            if (!b.UseAmbientScope)
            {
                var prev = next;
                next = () =>
                {
                    using var s = b.BeginScope!();
                    var inst = b.CreateInScope(s.ServiceProvider);
                    return b.Invoke(inst, env.Data!, ctx, prev, ct);
                };
            }
        }

        // Host behaviors (reverse compose)
        for (int i = behaviors.Count - 1; i >= 0; i--)
        {
            var b = behaviors[i];
            if (b.UseAmbientScope)
            {
                var prev = next;
                next = () =>
                {
                    var inst = b.CreateInScope(_root);
                    return b.Invoke(inst, env.Data!, ctx, prev, ct);
                };
            }
        }

        await next().ConfigureAwait(false);
    }

    public IDisposable RegisterFromAssembly(
        string ownerId,
        Assembly assembly,
        IServiceProvider ownerRoot,
        bool useAmbientScope
    )
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(ownerRoot);

        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("required", nameof(ownerId));

        var added = new List<IDisposable>();
        Func<IServiceScope>? beginScope = useAmbientScope ? null : () => ownerRoot.CreateScope();

        foreach (var type in SafeGetLoadableTypes(assembly))
        {
            if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                continue;

            var order = type.GetCustomAttribute<OrderAttribute>()?.Value ?? 0;

            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType)
                    continue;
                var def = iface.GetGenericTypeDefinition();

                var isHandler = def == GetHandlerForType();
                var isBehavior = def == GetBehaviorForType();

                if ((!isHandler && !isBehavior) || iface.ContainsGenericParameters)
                    continue;

                var msgType = iface.GetGenericArguments()[0];

                if (isHandler)
                {
                    var invoker = BuildHandlerInvoker(iface);
                    var reg = new Handler<TContext>(
                        OwnerId: ownerId,
                        MessageType: msgType,
                        UseAmbientScope: useAmbientScope,
                        BeginScope: beginScope,
                        CreateInScope: sp => ActivatorUtilities.CreateInstance(sp, type),
                        Invoke: invoker
                    );

                    var list = _handlers.GetOrAdd(msgType, _ => []);
                    lock (list)
                        list.Add(reg);

                    added.Add(
                        new ActionDisposable(() =>
                        {
                            if (_handlers.TryGetValue(msgType, out var lst))
                                lock (lst)
                                    lst.Remove(reg);
                        })
                    );
                }
                else if (isBehavior)
                {
                    var invoker = BuildBehaviorInvoker(iface);
                    var reg = new Behavior<TContext>(
                        OwnerId: ownerId,
                        MessageType: msgType,
                        Order: order,
                        UseAmbientScope: useAmbientScope,
                        BeginScope: beginScope,
                        CreateInScope: sp => ActivatorUtilities.CreateInstance(sp, type),
                        Invoke: invoker
                    );

                    var list = _behaviors.GetOrAdd(msgType, _ => []);
                    lock (list)
                    {
                        list.Add(reg);
                        list.Sort((a, b) => a.Order.CompareTo(b.Order));
                    }

                    added.Add(
                        new ActionDisposable(() =>
                        {
                            if (_behaviors.TryGetValue(msgType, out var lst))
                                lock (lst)
                                    lst.Remove(reg);
                        })
                    );
                }
            }
        }

        return new CompositeDisposable(added);
    }

    private static HandlerInvoker<TContext> BuildHandlerInvoker(Type ifaceMaybeClosed)
    {
        var method =
            ifaceMaybeClosed.GetMethod("HandleAsync")
            ?? throw new ArgumentException("HandleAsync not found", nameof(ifaceMaybeClosed));

        var ifaceClosed =
            method.DeclaringType
            ?? throw new ArgumentException(
                "Method has no declaring type",
                nameof(ifaceMaybeClosed)
            );

        if (ifaceClosed.IsGenericTypeDefinition || ifaceClosed.ContainsGenericParameters)
            throw new ArgumentException(
                "Invoker requires a closed interface type",
                nameof(ifaceMaybeClosed)
            );

        var inst = Expression.Parameter(typeof(object), "inst");
        var msg = Expression.Parameter(typeof(object), "msg");
        var ctx = Expression.Parameter(typeof(object), "ctx");
        var ct = Expression.Parameter(typeof(CancellationToken), "ct");

        var p = method.GetParameters(); // [0]=TEvent, [1]=Ctx, [2]=CancellationToken

        static Expression Conv(Expression e, Type t) => e.Type == t ? e : Expression.Convert(e, t);

        var call = Expression.Call(
            Conv(inst, ifaceClosed), // ✅ cast to the CLOSED iface
            method,
            Conv(msg, p[0].ParameterType),
            Conv(ctx, p[1].ParameterType),
            Conv(ct, p[2].ParameterType)
        );

        return Expression.Lambda<HandlerInvoker<TContext>>(call, inst, msg, ctx, ct).Compile();
    }

    private static BehaviorInvoker<TContext> BuildBehaviorInvoker(Type ifaceMaybeClosed)
    {
        var method =
            ifaceMaybeClosed.GetMethod("InvokeAsync")
            ?? throw new ArgumentException("InvokeAsync not found", nameof(ifaceMaybeClosed));

        var ifaceClosed =
            method.DeclaringType
            ?? throw new ArgumentException(
                "Method has no declaring type",
                nameof(ifaceMaybeClosed)
            );

        if (ifaceClosed.IsGenericTypeDefinition || ifaceClosed.ContainsGenericParameters)
            throw new ArgumentException(
                "Invoker requires a closed interface type",
                nameof(ifaceMaybeClosed)
            );

        // params
        var inst = Expression.Parameter(typeof(object), "inst");
        var msg = Expression.Parameter(typeof(object), "msg");
        var ctx = Expression.Parameter(typeof(object), "ctx");
        var next = Expression.Parameter(typeof(Func<Task>), "next");
        var ct = Expression.Parameter(typeof(CancellationToken), "ct");

        var p = method.GetParameters(); // [0]=TEvent, [1]=Ctx, [2]=Func<Task>, [3]=CancellationToken

        // local helper: cast when needed
        static Expression Conv(Expression e, Type t) => e.Type == t ? e : Expression.Convert(e, t);

        var call = Expression.Call(
            Conv(inst, ifaceClosed), // ✅ cast to the CLOSED iface
            method,
            Conv(msg, p[0].ParameterType),
            Conv(ctx, p[1].ParameterType),
            Conv(next, p[2].ParameterType),
            Conv(ct, p[3].ParameterType)
        );

        return Expression
            .Lambda<BehaviorInvoker<TContext>>(call, inst, msg, ctx, next, ct)
            .Compile();
    }

    protected static IEnumerable<Type> SafeGetLoadableTypes(Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
