using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives;

namespace Turbo.Events.Abstractions.Registry;

public interface IEventBehavior<TEvent>
    where TEvent : IEvent
{
    Task InvokeAsync(TEvent @event, EventContext ctx, Func<Task> next, CancellationToken ct);
}
