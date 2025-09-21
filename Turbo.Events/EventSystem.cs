using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Abstractions;
using Turbo.Events.Registry;

namespace Turbo.Events;

public sealed class EventSystem(EventRegistry registry)
{
    private readonly EventRegistry _registry = registry;

    public async Task PublishAsync(IEvent env, CancellationToken ct = default)
    {
        if (_registry is null)
            return;

        await _registry.PublishAsync(env, new object(), ct);
    }
}
