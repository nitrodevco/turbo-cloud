using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Pipeline.Abstractions.Envelope;

public interface IEnvelopeBus<TInteraction>
{
    public Task PublishAsync(
        TInteraction interaction,
        object? args = null,
        string? tag = null,
        CancellationToken ct = default
    );

    public Task PublishFireAndForgetAsync(
        TInteraction interaction,
        object? args = null,
        string? tag = null,
        CancellationToken ct = default
    );
}
