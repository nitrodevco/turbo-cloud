using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Core.Events;

public interface IEventBus
{
    // Fire-and-forget (enqueue and return)
    ValueTask PublishAsync<TEvent>(
        TEvent @event,
        string? tag = null,
        CancellationToken ct = default
    )
        where TEvent : IEvent;

    // Publish and wait until all handlers complete (same semantics, but awaits dispatch)
    Task PublishAndWaitAsync<TEvent>(
        TEvent @event,
        string? tag = null,
        CancellationToken ct = default
    )
        where TEvent : IEvent;
}
