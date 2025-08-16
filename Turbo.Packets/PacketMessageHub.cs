namespace Turbo.Packets;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Turbo.Core.Networking.Session;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Messages;

public class PacketMessageHub(ILogger<PacketMessageHub> logger) : IPacketMessageHub
{
    private readonly ILogger<PacketMessageHub> _logger = logger;

    // Immutable per-type listener lists for safe lock-free reads
    private readonly ConcurrentDictionary<Type, ImmutableArray<IPacketListener>> _handlers = new();
    private readonly Lock _handlersLock = new();

    // Filters (“callables”) grouped per-type
    private readonly ConcurrentDictionary<Type, List<object>> _filters = new();
    private readonly Lock _filtersLock = new();

    // ---------- Publish ----------
    public void Publish<T>(T message, ISessionContext ctx)
        where T : IMessageEvent
        => PublishAsync(message, ctx, CancellationToken.None).GetAwaiter().GetResult();

    public Task PublishAsync<T>(T message, ISessionContext ctx)
        where T : IMessageEvent
        => PublishAsync(message, ctx, CancellationToken.None);

    public async Task PublishAsync<T>(T message, ISessionContext ctx, CancellationToken ct)
        where T : IMessageEvent
    {
        // Filters (short-circuit on veto)
        foreach (var f in SnapshotFilters<T>())
        {
            try
            {
                if (!f.Call(message, ctx))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Filter threw for {MessageType}", typeof(T).Name);
                return;
            }
        }

        // Immutable snapshot; arrival order preserved
        var listeners = _handlers.TryGetValue(typeof(T), out var arr) ? arr : ImmutableArray<IPacketListener>.Empty;

        foreach (var listener in listeners)
        {
            if (!listener.Sender.IsAlive)
            {
                continue;
            }

            try
            {
                switch (listener.Action)
                {
                    case Action<T, ISessionContext> action:
                        action(message, ctx);
                        break;

                    case Func<T, ISessionContext, Task> func:
                        await func(message, ctx).ConfigureAwait(false);
                        break;

                    default:
                        _logger.LogWarning("Unsupported delegate type {Type}", listener.Action.GetType().Name);
                        break;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Listener threw for {MessageType}", typeof(T).Name);
            }
        }
    }

    // ---------- Subscribe (token-only) ----------
    public IDisposable Subscribe<T>(object subscriber, Action<T, ISessionContext> handler)
        where T : IMessageEvent
        => AddListener(subscriber, typeof(T), handler);

    public IDisposable Subscribe<T>(object subscriber, Func<T, ISessionContext, Task> handler)
        where T : IMessageEvent
        => AddListener(subscriber, typeof(T), handler);

    private IDisposable AddListener(object subscriber, Type messageType, Delegate handler)
    {
        var id = Guid.NewGuid();
        var listener = new IPacketListener
        {
            Id = id,
            Sender = new WeakReference(subscriber),
            Action = handler,
        };

        lock (_handlersLock)
        {
            var cur = _handlers.TryGetValue(messageType, out var existing)
                ? existing.ToList()
                : new List<IPacketListener>(1);

            // prune dead, append to preserve arrival order
            cur.RemoveAll(l => !l.Sender.IsAlive);
            cur.Add(listener);

            _handlers[messageType] = cur.ToImmutableArray();
        }

        return new PacketSubscriptionToken(this, messageType, id);
    }

    public void RemoveListenerById(Type messageType, Guid id)
    {
        lock (_handlersLock)
        {
            if (!_handlers.TryGetValue(messageType, out var arr))
            {
                return;
            }

            var kept = arr.Where(l => l.Id != id && l.Sender.IsAlive).ToImmutableArray();
            _handlers[messageType] = kept;
        }
    }

    // ---------- Filters (callables) ----------
    public void RegisterCallable<T>(ICallable<T> callable)
        where T : IMessageEvent
    {
        var list = _filters.GetOrAdd(typeof(T), _ => new List<object>());
        lock (_filtersLock)
        {
            list.Add(callable);
        }
    }

    public void UnRegisterCallable<T>(ICallable<T> callable)
        where T : IMessageEvent
    {
        if (_filters.TryGetValue(typeof(T), out var list))
        {
            lock (_filtersLock)
            {
                list.Remove(callable);
            }
        }
    }

    private IReadOnlyList<ICallable<T>> SnapshotFilters<T>()
        where T : IMessageEvent
    {
        if (!_filters.TryGetValue(typeof(T), out var list))
        {
            return Array.Empty<ICallable<T>>();
        }

        lock (_filtersLock)
        {
            return list.OfType<ICallable<T>>().ToArray();
        }
    }
}
