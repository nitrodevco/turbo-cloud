using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerWalletGrain
{
    /// <summary>
    /// The balance and the receipt of the reference commit together, and so does its movement
    /// while recording. The primary key of the receipt settles a race with another activation.
    /// </summary>
    public async Task<WalletCreditResult> CreditAsync(
        CurrencyKind kind,
        int amount,
        string reference,
        MovementCause cause,
        CancellationToken ct
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            reference.Length,
            WalletCreditReference.MaxLength
        );

        var (result, balance) = await CreditOnceAsync(
            kind,
            amount,
            reference,
            reward: false,
            cause,
            ct
        );

        if (result == WalletCreditResult.Applied && balance is not null)
            await _grainFactory.TrySendComposerToPlayerAsync(PlayerId, balance, ct);

        return result;
    }
}
