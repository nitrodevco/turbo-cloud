namespace Turbo.Primitives.WiredTrading.Enums;

/// <summary>The kind of a completed wired transaction; only <see cref="Rewarded"/> carries reward contents.</summary>
public enum WiredTransactionSuccessType
{
    Deposit = 0,
    Withdraw = 1,
    Rewarded = 2,
    Payment = 3,
    Trade = 4,
}
