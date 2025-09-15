using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Turbo.Pipeline.Attributes;
using Turbo.Pipeline.Registry;

namespace Turbo.Pipeline;

public class GenericBus<TInteraction, TContext, TMeta>
{
    private static readonly Type HandlerOpen = typeof(IHandler<,>);
    private static readonly Type BehaviorOpen = typeof(IBehavior<,>);

    private readonly IServiceProvider _hostRoot;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Func<IServiceProvider, object, TMeta, TContext> _createContext;

    private readonly ConcurrentDictionary<string, IServiceProvider> _ownerRoots = new(
        StringComparer.Ordinal
    );
    private readonly ConcurrentDictionary<Type, Handler<TContext>[]> _handlers = new();
    private readonly ConcurrentDictionary<Type, Behavior<TContext>[]> _behaviors = new();
    private readonly ILogger? _logger;

    public GenericBus(
        IServiceProvider hostRoot,
        Func<IServiceProvider, object, TMeta, TContext> createContext
    )
    {
        _hostRoot = hostRoot ?? throw new ArgumentNullException(nameof(hostRoot));
        _scopeFactory = hostRoot.GetRequiredService<IServiceScopeFactory>();
        _createContext = createContext ?? throw new ArgumentNullException(nameof(createContext));
        _logger = hostRoot.GetService<ILogger<GenericBus<TInteraction, TContext, TMeta>>>();

        // Always register host root
        _ownerRoots[CompositeEnvelopeScope.HostOwnerId] = _hostRoot;
    }

    // Convenience ctor when your context doesn't need the envelope value
    public GenericBus(
        IServiceProvider hostRoot,
        Func<IServiceProvider, TMeta, TContext> createContext
    )
        : this(hostRoot, (sp, _env, meta) => createContext(sp, meta)) { }

    public IDisposable RegisterFromAssembly(
        string ownerId,
        Assembly asm,
        IServiceProvider ownerRoot,
        bool useAmbientScope
    )
    {
        if (ownerId is null)
            throw new ArgumentNullException(nameof(ownerId));
        if (asm is null)
            throw new ArgumentNullException(nameof(asm));
        if (ownerRoot is null)
            throw new ArgumentNullException(nameof(ownerRoot));

        // remember the owner root (idempotent)
        _ownerRoots.AddOrUpdate(ownerId, ownerRoot, (_, __) => ownerRoot);

        var added = new List<IDisposable>();
        var scopeFactory = ownerRoot.GetRequiredService<IServiceScopeFactory>();

        foreach (var type in AssemblyUtil.SafeGetLoadableTypes(asm))
        {
            if (type.ContainsGenericParameters)
                continue;

            var order = type.GetCustomAttribute<OrderAttribute>(inherit: false)?.Value ?? 0;
            var seenHandlers = new HashSet<Type>();
            var seenBehaviors = new HashSet<Type>();

            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsConstructedGenericType)
                    continue;

                // HANDLER?
                if (TypeUtil.TryGetGenericAncestor(iface, HandlerOpen, out var closedHandler))
                {
                    var envType = closedHandler.GetGenericArguments()[0];
                    if (!typeof(TInteraction).IsAssignableFrom(envType))
                        continue;

                    if (seenHandlers.Add(closedHandler))
                    {
                        var factory = ActivatorUtilities.CreateFactory(type, Type.EmptyTypes);
                        Func<IServiceProvider, object> create = useAmbientScope
                            ? (Func<IServiceProvider, object>)(
                                sp => factory(sp, Array.Empty<object?>())
                            )
                            : (sp => sp.GetRequiredService(type));

                        var invoker = InvokerCache<TContext>.GetHandler(envType);

                        var reg = new Handler<TContext>(
                            OwnerId: ownerId,
                            MessageType: envType,
                            ImplementationType: type,
                            UseAmbientScope: useAmbientScope,
                            ScopeFactory: scopeFactory,
                            Create: create,
                            Invoke: invoker
                        );

                        _handlers.AddOrUpdate(
                            envType,
                            _ => new[] { reg },
                            (_, old) =>
                            {
                                var len = old.Length;
                                var next = new Handler<TContext>[len + 1];
                                Array.Copy(old, 0, next, 0, len);
                                next[len] = reg;
                                return next;
                            }
                        );

                        added.Add(
                            new ActionDisposable(() =>
                            {
                                // atomic remove: retry loop
                                while (true)
                                {
                                    if (!_handlers.TryGetValue(envType, out var cur))
                                        break;
                                    var idx = Array.IndexOf(cur, reg);
                                    if (idx < 0)
                                        break;
                                    if (cur.Length == 1)
                                    {
                                        // try remove entire entry
                                        if (
                                            _handlers.TryRemove(
                                                new KeyValuePair<Type, Handler<TContext>[]>(
                                                    envType,
                                                    cur
                                                )
                                            )
                                        )
                                            break;
                                        continue; // retry
                                    }

                                    var next = new Handler<TContext>[cur.Length - 1];
                                    if (idx > 0)
                                        Array.Copy(cur, 0, next, 0, idx);
                                    if (idx < cur.Length - 1)
                                        Array.Copy(cur, idx + 1, next, idx, cur.Length - idx - 1);

                                    if (_handlers.TryUpdate(envType, next, cur))
                                        break;
                                    // else retry
                                }
                            })
                        );
                    }
                    continue;
                }

                // BEHAVIOR?
                if (TypeUtil.TryGetGenericAncestor(iface, BehaviorOpen, out var closedBehavior))
                {
                    var envType = closedBehavior.GetGenericArguments()[0];
                    if (!typeof(TInteraction).IsAssignableFrom(envType))
                        continue;

                    if (seenBehaviors.Add(closedBehavior))
                    {
                        var factory = ActivatorUtilities.CreateFactory(type, Type.EmptyTypes);
                        Func<IServiceProvider, object> create = useAmbientScope
                            ? (Func<IServiceProvider, object>)(
                                sp => factory(sp, Array.Empty<object?>())
                            )
                            : (sp => sp.GetRequiredService(type));

                        var invoker = InvokerCache<TContext>.GetBehavior(envType);

                        var reg = new Behavior<TContext>(
                            OwnerId: ownerId,
                            MessageType: envType,
                            ImplementationType: type,
                            Order: order,
                            UseAmbientScope: useAmbientScope,
                            ScopeFactory: scopeFactory,
                            Create: create,
                            Invoke: invoker
                        );

                        _behaviors.AddOrUpdate(
                            envType,
                            _ => new[] { reg },
                            (_, old) =>
                            {
                                // insert ordered by Order
                                var len = old.Length;
                                var next = new Behavior<TContext>[len + 1];
                                var inserted = false;
                                var p = 0;
                                for (int i = 0; i < len; i++)
                                {
                                    if (!inserted && reg.Order < old[i].Order)
                                    {
                                        next[p++] = reg;
                                        inserted = true;
                                    }
                                    next[p++] = old[i];
                                }
                                if (!inserted)
                                    next[p++] = reg;
                                return next;
                            }
                        );

                        added.Add(
                            new ActionDisposable(() =>
                            {
                                // atomic remove similar to handlers
                                while (true)
                                {
                                    if (!_behaviors.TryGetValue(envType, out var cur))
                                        break;
                                    var idx = Array.IndexOf(cur, reg);
                                    if (idx < 0)
                                        break;
                                    if (cur.Length == 1)
                                    {
                                        if (
                                            _behaviors.TryRemove(
                                                new KeyValuePair<Type, Behavior<TContext>[]>(
                                                    envType,
                                                    cur
                                                )
                                            )
                                        )
                                            break;
                                        continue;
                                    }
                                    var next = new Behavior<TContext>[cur.Length - 1];
                                    if (idx > 0)
                                        Array.Copy(cur, 0, next, 0, idx);
                                    if (idx < cur.Length - 1)
                                        Array.Copy(cur, idx + 1, next, idx, cur.Length - idx - 1);
                                    if (_behaviors.TryUpdate(envType, next, cur))
                                        break;
                                }
                            })
                        );
                    }
                }
            }
        }

        return new CompositeDisposable(added);
    }

    // No pluginRoots param anymore — the bus knows them.
    public async Task PublishAsync(
        object envelope,
        bool createHostAmbientScope,
        TMeta meta,
        CancellationToken ct = default
    )
    {
        var envType = envelope.GetType();

        var handlers = _handlers.TryGetValue(envType, out var hs)
            ? hs.ToArray()
            : Array.Empty<Handler<TContext>>();
        var behaviors = _behaviors.TryGetValue(envType, out var bs)
            ? bs.ToArray()
            : Array.Empty<Behavior<TContext>>();
        if (handlers.Length == 0 && behaviors.Length == 0)
            return;

        // Figure out which owners are relevant for THIS envelope (excluding host — host is passed separately)
        var neededOwnerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var h in handlers)
            if (h.OwnerId != CompositeEnvelopeScope.HostOwnerId)
                neededOwnerIds.Add(h.OwnerId);
        foreach (var b in behaviors)
            if (b.OwnerId != CompositeEnvelopeScope.HostOwnerId)
                neededOwnerIds.Add(b.OwnerId);

        var ownerRoots = new Dictionary<string, IServiceProvider>(
            neededOwnerIds.Count,
            StringComparer.Ordinal
        );
        foreach (var id in neededOwnerIds)
            if (_ownerRoots.TryGetValue(id, out var sp))
                ownerRoots[id] = sp;

        await using var composite = CompositeEnvelopeScope.Begin(
            hostRoot: _hostRoot,
            createHostAmbientScope: createHostAmbientScope,
            pluginRoots: ownerRoots
        ); // internally the composite will build per-owner ambient scopes

        var hostAmbient = composite.AmbientForOwner(CompositeEnvelopeScope.HostOwnerId);
        var baseCtx = _createContext(hostAmbient, envelope, meta);

        var invokeH = InvokerCache<TContext>.GetHandler(envType);
        var invokeB = InvokerCache<TContext>.GetBehavior(envType);

        await using var instanceCache = new PerPublishInstanceCache();

        object GetOrCreateAmbient(
            string ownerId,
            Type impl,
            Func<IServiceProvider, object> create
        ) =>
            instanceCache.GetOrAdd(
                (ownerId, impl),
                () =>
                {
                    var sp = composite.AmbientForOwner(ownerId);
                    return create(sp);
                }
            );

        // Handlers runner (sequential)
        Func<Task> runHandlers = async () =>
        {
            foreach (var reg in handlers)
            {
                if (reg.UseAmbientScope)
                {
                    var inst = GetOrCreateAmbient(reg.OwnerId, reg.ImplementationType, reg.Create);
                    await reg.Invoke(inst, envelope, baseCtx, ct).ConfigureAwait(false);
                }
                else
                {
                    await using var scope = reg.ScopeFactory.CreateAsyncScope();
                    var inst = reg.Create(scope.ServiceProvider);
                    await reg.Invoke(inst, envelope, baseCtx, ct).ConfigureAwait(false);
                }
            }
        };

        // Behaviors pipeline (LIFO)
        Func<Task> pipeline = runHandlers;
        for (int i = behaviors.Length - 1; i >= 0; i--)
        {
            var reg = behaviors[i];
            var next = pipeline;

            pipeline = async () =>
            {
                if (reg.UseAmbientScope)
                {
                    var inst = GetOrCreateAmbient(reg.OwnerId, reg.ImplementationType, reg.Create);
                    await reg.Invoke(inst, envelope, baseCtx, next, ct).ConfigureAwait(false);
                }
                else
                {
                    await using var scope = reg.ScopeFactory.CreateAsyncScope();
                    var inst = reg.Create(scope.ServiceProvider);
                    await reg.Invoke(inst, envelope, baseCtx, next, ct).ConfigureAwait(false);
                }
            };
        }

        await pipeline().ConfigureAwait(false);
    }

    // QoL overload when you don't need meta
    public Task PublishAsync(
        object envelope,
        bool createHostAmbientScope,
        CancellationToken ct = default
    ) => PublishAsync(envelope, createHostAmbientScope, default!, ct);
}
