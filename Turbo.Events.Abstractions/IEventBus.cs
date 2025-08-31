using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Events.Abstractions;

public interface IEventBus
{
    ValueTask PublishAsync<TEvent>(
        TEvent @event,
        string? tag = null,
        CancellationToken ct = default
    );

    Task PublishAndWaitAsync<TEvent>(
        TEvent @event,
        string? tag = null,
        CancellationToken ct = default
    );
}
