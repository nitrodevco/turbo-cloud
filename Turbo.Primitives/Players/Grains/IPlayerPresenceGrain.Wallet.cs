using System.Threading;
using System.Threading.Tasks;
using Orleans.Concurrency;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    [AlwaysInterleave]
    public Task OnCurrencyUpdateAsync(WalletCurrencyUpdateSnapshot snapshot, CancellationToken ct);
}
