using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace Turbo.Primitives.Movements.Grains;

/// <summary>
/// Hands the asset movement outbox to the registered consumers, one grain for the hotel. It polls
/// only while a consumer is registered; registering one wakes it. See <c>docs/movements.md</c>.
/// </summary>
public interface IAssetMovementDispatcherGrain : IGrainWithStringKey
{
    /// <summary>Starts polling if it is not already; a no-op when no consumer is registered.</summary>
    public Task WakeAsync(CancellationToken ct);
}
