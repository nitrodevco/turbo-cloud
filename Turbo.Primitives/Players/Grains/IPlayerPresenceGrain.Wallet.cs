using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    public Task OnCurrencyUpdateAsync(WalletCurrencyUpdateSnapshot snapshot, CancellationToken ct);
}
