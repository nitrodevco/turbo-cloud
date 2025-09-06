using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives;

namespace Turbo.Envelope.Abstractions;

public interface IEnvelopeBus<TEnvelope, TInteraction>
    where TInteraction : IEvent
{
    ValueTask PublishAsync(
        TInteraction interaction,
        object? args = null,
        CancellationToken ct = default
    );

    Task PublishAndWaitAsync(
        TInteraction interaction,
        object? args = null,
        CancellationToken ct = default
    );
}
