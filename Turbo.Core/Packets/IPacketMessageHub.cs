using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;

namespace Turbo.Core.Packets;

public interface IPacketMessageHub
{
    public void Publish<T>(T message, ISessionContext ctx) where T : IMessageEvent;
    public Task PublishAsync<T>(T message, ISessionContext ctx) where T : IMessageEvent;
    public void Subscribe<T>(object subscriber, Action<T, ISessionContext> handler) where T : IMessageEvent;
    public void Subscribe<T>(object subscriber, Func<T, ISessionContext, Task> handler) where T : IMessageEvent;
    public void RegisterCallable<T>(ICallable<T> callable) where T : IMessageEvent;
    public void UnRegisterCallable<T>(ICallable<T> callable) where T : IMessageEvent;
    public void Unsubscribe(object subscriber);
    public void Unsubscribe<T>(object subscriber, Action<T, ISessionContext> handler = null) where T : IMessageEvent;
    public void Unsubscribe<T>(object subscriber, Func<T, ISessionContext, Task> handler) where T : IMessageEvent;
    public bool Exists(object subscriber);
    public bool Exists<T>(object subscriber) where T : IMessageEvent;
    public bool Exists<T>(object subscriber, Action<T, ISessionContext> handler) where T : IMessageEvent;
    public bool Exists<T>(object subscriber, Func<T, ISessionContext, Task> handler) where T : IMessageEvent;
    public List<ICallable<T>> GetCallables<T>() where T : IMessageEvent;
}