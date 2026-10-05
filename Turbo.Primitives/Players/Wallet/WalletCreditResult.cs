using Orleans;

namespace Turbo.Primitives.Players.Wallet;

/// <summary>The outcome of a credit made with a reference.</summary>
[GenerateSerializer]
public enum WalletCreditResult
{
    /// <summary>The balance was credited.</summary>
    Applied = 0,

    /// <summary>The reference was already used for this player; nothing changed.</summary>
    AlreadyApplied = 1,

    /// <summary>Nothing was credited: bad amount, unknown currency, or the balance would overflow.</summary>
    Rejected = 2,
}
