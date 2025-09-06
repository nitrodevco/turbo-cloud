using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives;

namespace Turbo.Events.Abstractions.Registry;

public interface IEventBehavior<TEvent>
    where TEvent : IEvent
{
    Task InvokeAsync(TEvent interaction, EventContext ctx, Func<Task> next, CancellationToken ct);
}
