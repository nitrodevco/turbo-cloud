using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Primitives.Players.Grains;

public interface IPlayerWalletGrain : IGrainWithIntegerKey
{
    public Task<WalletDebitResult> TryDebitAsync(
        ImmutableArray<WalletDebitRequest> requests,
        CancellationToken ct
    );

    public Task RefundAsync(ImmutableArray<WalletDebitRequest> requests, CancellationToken ct);
}
