using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;

namespace Turbo.Core.Packets;

public interface IPacketMessageHub
{
    void Publish<T>(T message, ISessionContext ctx)
        where T : IMessageEvent;

    Task PublishAsync<T>(T message, ISessionContext ctx)
        where T : IMessageEvent;

    Task PublishAsync<T>(T message, ISessionContext ctx, CancellationToken ct)
        where T : IMessageEvent;

    // Subscribe (returns token; dispose to unsubscribe)
    IDisposable Subscribe<T>(object subscriber, Action<T, ISessionContext> handler)
        where T : IMessageEvent;

    IDisposable Subscribe<T>(object subscriber, Func<T, ISessionContext, Task> handler)
        where T : IMessageEvent;

    public void RemoveListenerById(Type messageType, Guid id);

    // Filters (callables)
    void RegisterCallable<T>(ICallable<T> callable)
        where T : IMessageEvent;

    void UnRegisterCallable<T>(ICallable<T> callable)
        where T : IMessageEvent;
}
