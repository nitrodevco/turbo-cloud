namespace Turbo.Primitives.Players.Wallet;

/// <summary>Limits of the opaque reference that makes a wallet credit idempotent.</summary>
public static class WalletCreditReference
{
    /// <summary>The longest reference accepted, in characters.</summary>
    public const int MaxLength = 128;
}
