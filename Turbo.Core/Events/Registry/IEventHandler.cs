using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Core.Events.Registry;

public interface IEventHandler<in TEvent>
    where TEvent : IEvent
{
    Task HandleAsync(TEvent @event, EventContext ctx, CancellationToken ct);
}
