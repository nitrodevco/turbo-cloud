using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
    public Task OnCurrencyUpdateAsync(
        WalletCurrencyUpdateSnapshot snapshot,
        CancellationToken ct
    ) => _walletModule.OnCurrencyUpdateAsync(snapshot, ct);
}
