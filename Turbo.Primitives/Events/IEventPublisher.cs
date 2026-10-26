using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Events;

/// <summary>
/// Publishes on the hotel's event system, to every <c>IEventHandler&lt;T&gt;</c> of the event's
/// type, a plugin's included. The host's event system is it: a plugin takes it from the host
/// services and needs no reference to <c>Turbo.Events</c>.
/// </summary>
public interface IEventPublisher
{
    /// <summary>Publishes <paramref name="env"/> and waits for its handlers.</summary>
    Task PublishAsync(IEvent env, CancellationToken ct = default);

    /// <summary>
    /// Publishes a post-commit event once its change is saved: on the thread pool, off the
    /// caller's turn (a grain's included), and without waiting for its handlers, so none can hold
    /// up or undo what happened. A handler that throws is logged.
    /// </summary>
    void PublishCommitted(DomainEvent evt);
}
