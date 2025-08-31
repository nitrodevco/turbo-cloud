using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Core.Events.Registry;

public interface IEventBehavior<TEvent>
    where TEvent : IEvent
{
    Task InvokeAsync(TEvent @event, EventContext ctx, Func<Task> next, CancellationToken ct);
}
