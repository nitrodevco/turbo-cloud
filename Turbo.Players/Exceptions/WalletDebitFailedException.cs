using System;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Players.Exceptions;

/// <summary>
/// Raised when a wallet debit did not move the balance by the requested amount, which means the
/// currency could not cover it. Carries the request detail so the caller can report which currency
/// fell short without re-deriving it.
/// </summary>
public sealed class WalletDebitFailedException(
    CurrencyKind currencyKind,
    int requestedAmount,
    int appliedAmount
) : Exception($"Wallet debit of {requestedAmount} failed; {appliedAmount} was applied instead.")
{
    public CurrencyKind CurrencyKind { get; } = currencyKind;

    /// <summary>The amount the debit asked for.</summary>
    public int RequestedAmount { get; } = requestedAmount;

    /// <summary>The amount the balance actually moved by.</summary>
    public int AppliedAmount { get; } = appliedAmount;
}
