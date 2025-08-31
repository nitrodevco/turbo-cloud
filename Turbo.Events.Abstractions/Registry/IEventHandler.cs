using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Events.Abstractions.Registry;

public interface IEventHandler<in TEvent>
{
    Task HandleAsync(TEvent @event, EventContext ctx, CancellationToken ct);
}
