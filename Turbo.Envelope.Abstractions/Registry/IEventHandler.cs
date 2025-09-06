using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives;

namespace Turbo.Events.Abstractions.Registry;

public interface IEventHandler<in TEvent>
    where TEvent : IEvent
{
    Task HandleAsync(TEvent @event, EventContext ctx, CancellationToken ct);
}
