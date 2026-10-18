using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Primitives.Players.Grains;

/// <summary>
/// A player's balances. Every method that moves one takes the <see cref="MovementCause"/> of the
/// action it is part of; while <c>Turbo:Movements:Enabled</c> is on, each currency it moves is one
/// movement, written in the same transaction as the balance (docs/movements.md).
/// </summary>
public interface IPlayerWalletGrain : IGrainWithIntegerKey
{
    public Task<WalletDebitResult> TryDebitAsync(
        List<WalletDebitRequest> requests,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Debits at most once per <paramref name="reference"/> for this player, as the referenced
    /// <see cref="CreditAsync(CurrencyKind, int, string, MovementCause, CancellationToken)"/>
    /// credits: a retry takes nothing and answers success with
    /// <see cref="WalletDebitResult.AlreadyApplied"/>. Debits and credits keep apart receipts, so
    /// a debit under the reference of an earlier credit (taking back a grant) takes the money. A
    /// refused debit keeps no receipt. The reference follows the credit's rules, otherwise this
    /// throws.
    /// </summary>
    public Task<WalletDebitResult> TryDebitAsync(
        List<WalletDebitRequest> requests,
        string reference,
        MovementCause cause,
        CancellationToken ct
    );
    public Task<int> GetAmountForCurrencyAsync(CurrencyKind kind, CancellationToken ct);

    /// <summary>Adds to a balance and tells the player. False when the currency does not exist.</summary>
    public Task<bool> CreditAsync(
        CurrencyKind kind,
        int amount,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>
    /// Credits at most once per <paramref name="reference"/> for this player, so a caller that
    /// retries after a crash cannot double-credit. The reference is opaque and compared case-insensitively on MySQL;
    /// by convention it is <c>&lt;plugin-key&gt;:&lt;id&gt;</c>. It must be non-blank, at most
    /// <see cref="WalletCreditReference.MaxLength"/> characters and not start with the wallet's own
    /// <c>#op:</c> or <c>#debit:</c>, otherwise this throws.
    /// </summary>
    public Task<WalletCreditResult> CreditAsync(
        CurrencyKind kind,
        int amount,
        string reference,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>Credits a command reward and persists its notification with the balance.</summary>
    public Task<bool> CreditRewardAsync(
        CurrencyKind kind,
        int amount,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>Commits an immutable award receipt with the balance; replay cannot credit twice.</summary>
    public Task<bool> CreditAchievementAsync(
        string awardKey,
        CurrencyKind kind,
        int amount,
        MovementCause cause,
        CancellationToken ct
    );

    /// <summary>Submits accumulated reward notices to the active session, retaining unsuccessful sends.</summary>
    public Task DeliverPendingRewardsAsync(CancellationToken ct);
    public Task<Dictionary<int, int>> GetActivityPointsAsync(CancellationToken ct);
}
