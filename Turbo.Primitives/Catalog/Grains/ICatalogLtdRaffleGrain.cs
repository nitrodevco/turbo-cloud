using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Catalog.Grains;

public interface ICatalogLtdRaffleGrain : IGrainWithIntegerKey
{
    public Task<LtdRaffleEntryResult> EnterRaffleAsync(PlayerId playerId, CancellationToken ct);

    public Task<LtdSeriesSnapshot?> GetSeriesSnapshotAsync(CancellationToken ct);

    public Task ForceRunRaffleAsync(CancellationToken ct);
    public Task ReloadSeriesAsync(CancellationToken ct);
}
