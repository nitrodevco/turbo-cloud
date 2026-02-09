using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Primitives.Players.Grains;

public interface IPlayerWalletGrain : IGrainWithIntegerKey
{
    public Task<WalletDebitResult> TryDebitAsync(
        List<WalletDebitRequest> requests,
        CancellationToken ct
    );
    public Task<int> GetAmountForCurrencyAsync(CurrencyKind kind, CancellationToken ct);
    public Task<Dictionary<int, int>> GetActivityPointsAsync(CancellationToken ct);
}
