using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Contracts.Hotel;

/// <summary>What a credit or a debit did.</summary>
public enum WalletOutcome
{
    /// <summary>The balance changed.</summary>
    Done,

    /// <summary>Done before under the same reference: nothing changed now. Treat it as done.</summary>
    AlreadyDone,

    /// <summary>
    /// Nothing changed: no such currency, an amount below 1, a balance that can't cover a debit or
    /// would overflow, or the write failed. No receipt is kept, so the same reference can go through later.
    /// </summary>
    Refused,
}

/// <summary>
/// Players' balances. A currency is named as in the hotel's currency types (<c>credits</c>,
/// <c>duckets</c>, <c>diamonds</c>; any case). Every change is keyed by a reference of the
/// plugin's (<c>&lt;plugin-key&gt;:&lt;id&gt;</c>, at most 128 characters), so it is made at most
/// once however often it is asked (a retry after a crash), and is recorded in the hotel's movement
/// log under its cause, a code of the plugin's (<c>skills.level_reward</c>). A
/// credit and a debit keep apart receipts: a debit under a credit's reference takes it back, once
/// (<c>docs/wallet-idempotent-credit.md</c>).
/// </summary>
public interface IHotelWallet
{
    /// <summary>Whether the hotel has an enabled currency of that name.</summary>
    [HotelCall(HotelCallContext.Anywhere)]
    bool CurrencyExists(string currency);

    /// <summary>The player's balance of the currency; 0 when the hotel has no such currency.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<int> BalanceAsync(PlayerId player, string currency, CancellationToken ct);

    /// <summary>Adds <paramref name="amount"/> to the player's balance (online or not) and tells them.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<WalletOutcome> CreditAsync(
        PlayerId player,
        string currency,
        int amount,
        string reference,
        string cause,
        CancellationToken ct
    );

    /// <summary>Takes <paramref name="amount"/> from the player's balance, all or nothing.</summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<WalletOutcome> DebitAsync(
        PlayerId player,
        string currency,
        int amount,
        string reference,
        string cause,
        CancellationToken ct
    );
}

/// <summary>The rules of a wallet reference, which the host and the fake hotel both apply.</summary>
public static class HotelReference
{
    /// <summary>The longest reference, in characters.</summary>
    public const int MAX_LENGTH = 128;

    private static readonly string[] Reserved = ["#op:", "#debit:"];

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> for a reference that is blank, longer than
    /// <see cref="MAX_LENGTH"/> or starts with one of the wallet's own receipt keys (<c>#op:</c>,
    /// <c>#debit:</c>): a mistake in the plugin, not something a player did.
    /// </summary>
    public static void ThrowIfInvalid(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reference.Length, MAX_LENGTH);

        foreach (var reserved in Reserved)
            if (reference.StartsWith(reserved, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"A wallet reference cannot start with {reserved}",
                    nameof(reference)
                );
    }
}
