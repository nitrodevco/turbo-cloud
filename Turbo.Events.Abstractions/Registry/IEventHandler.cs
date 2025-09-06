using System.Threading;
using System.Threading.Tasks;
using Turbo.Pipeline.Abstractions.Registry;
using Turbo.Primitives;

namespace Turbo.Events.Abstractions.Registry;

public interface IEventHandler<TEvent>
    where TEvent : IEvent
{
    Task HandleAsync(TEvent interaction, EventContext ctx, CancellationToken ct);
}
