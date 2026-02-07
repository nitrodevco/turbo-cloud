using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans.Snapshots.Players;

namespace Turbo.Primitives.Players;

public interface IPlayerGrain : IGrainWithIntegerKey
{
    public Task<PlayerSummarySnapshot> GetSummaryAsync(CancellationToken ct);

    public Task<PlayerWalletSnapshot> GetWalletAsync(CancellationToken ct);
}
