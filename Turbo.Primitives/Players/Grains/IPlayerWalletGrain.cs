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

    /// <summary>Adds to a balance and tells the player. False when the currency does not exist.</summary>
    public Task<bool> CreditAsync(CurrencyKind kind, int amount, CancellationToken ct);

    /// <summary>Credits a command reward and persists its notification with the balance.</summary>
    public Task<bool> CreditRewardAsync(CurrencyKind kind, int amount, CancellationToken ct);

    /// <summary>Commits an immutable award receipt with the balance; replay cannot credit twice.</summary>
    public Task<bool> CreditAchievementAsync(
        string awardKey,
        CurrencyKind kind,
        int amount,
        CancellationToken ct
    );

    /// <summary>Submits accumulated reward notices to the active session, retaining unsuccessful sends.</summary>
    public Task DeliverPendingRewardsAsync(CancellationToken ct);
    public Task<Dictionary<int, int>> GetActivityPointsAsync(CancellationToken ct);
}
