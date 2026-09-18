using Orleans;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Primitives.Players.Snapshots;

[GenerateSerializer, Immutable]
public sealed record WalletCurrencyUpdateSnapshot
{
    [Id(0)]
    public required CurrencyKind CurrencyKind { get; init; }

    [Id(1)]
    /// <summary>Signed delta: negative for a debit, positive for a credit.</summary>
    public required int ChangedBy { get; init; }

    [Id(2)]
    public required int Amount { get; init; }
}
