using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Abstractions.Envelope;

public interface IEnvelopeBus<TInteraction>
{
    ValueTask PublishAsync(
        TInteraction interaction,
        object? args = null,
        string? tag = null,
        CancellationToken ct = default
    );

    Task PublishAndWaitAsync(
        TInteraction interaction,
        object? args = null,
        string? tag = null,
        CancellationToken ct = default
    );
}
