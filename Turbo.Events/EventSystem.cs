using System.Threading;
using System.Threading.Tasks;
using Turbo.Events.Registry;
using Turbo.Primitives.Events;

namespace Turbo.Events;

/// <summary>
/// The hotel's event system. A plugin publishes through the contract it implements,
/// <see cref="IEventPublisher"/>, which the host services give as this same instance.
/// </summary>
public sealed class EventSystem(EventRegistry registry) : IEventPublisher
{
    private readonly EventRegistry _registry = registry;

    public async Task PublishAsync(IEvent env, CancellationToken ct = default)
    {
        await _registry.PublishAsync(env, null, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Publishes a post-commit event (<see cref="DomainEvent"/>) once its change is saved: on the
    /// thread pool, off the caller's turn (a grain's included), and without waiting for its
    /// handlers, so none can hold up or undo what happened. A handler that throws is logged by
    /// the registry.
    /// </summary>
    public void PublishCommitted(DomainEvent evt) =>
        _ = Task.Run(() => _registry.PublishAsync(evt, null, CancellationToken.None));
}
