using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives;

namespace Turbo.Messaging.Abstractions.Registry;

public interface IMessageBehavior<TMessage>
    where TMessage : IMessageEvent
{
    Task InvokeAsync(
        TMessage interaction,
        MessageContext ctx,
        Func<Task> next,
        CancellationToken ct
    );
}
