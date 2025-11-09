using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Players;

public interface IPlayerEndpointGrain : IGrainWithIntegerKey
{
    public Task BindConnectionAsync(string connectionId);
    public Task UnbindConnectionAsync(string connectionId);
    public Task SendComposerAsync(IComposer composer, CancellationToken ct = default);
}
