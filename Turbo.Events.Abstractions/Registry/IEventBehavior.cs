using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Events.Abstractions.Registry;

public interface IEventBehavior<TEvent>
{
    Task InvokeAsync(TEvent @event, EventContext ctx, Func<Task> next, CancellationToken ct);
}
